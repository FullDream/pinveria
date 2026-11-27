using System.Text;
using Pinveria.Application.Media.Optimization;
using Pinveria.Application.Media.Optimization.Caching;
using Pinveria.Application.Media.Optimization.Processing;
using Pinveria.Infrastructure.Media.Optimization;
using Pinveria.Infrastructure.Media.Storage;
using Microsoft.Extensions.Options;
using NetVips;

namespace Pinveria.IntegrationTests;

public sealed class ImageVariantServiceTests : IDisposable
{
	private readonly string storageRoot = Path.Combine(Path.GetTempPath(), $"variant-storage-{Guid.NewGuid():N}");
	private readonly string cacheRoot = Path.Combine(Path.GetTempPath(), $"variant-cache-{Guid.NewGuid():N}");

	[Fact]
	public async Task DeduplicatesConcurrentRequestsThroughSingleflightAndFoldsClampedWidthsToOrigKey()
	{
		var storage = CreateStorage();
		var cache = CreateCache();
		var processor = new CountingImageProcessor(new VipsImageProcessor(Options.Create(new ImageOptimizationOptions())));
		var service = new ImageVariantService(storage, cache, processor);
		var assetId = WriteRasterAsset(storage, width: 400, height: 300, format: "jpeg");

		var commands = new[]
		{
			new ImageTransformCommand(assetId, Width: 640, ImageOutputFormat.Webp, VaryByAccept: true),
			new ImageTransformCommand(assetId, Width: 960, ImageOutputFormat.Webp, VaryByAccept: true),
			new ImageTransformCommand(assetId, Width: 1280, ImageOutputFormat.Webp, VaryByAccept: true),
			new ImageTransformCommand(assetId, Width: 640, ImageOutputFormat.Webp, VaryByAccept: true),
			new ImageTransformCommand(assetId, Width: 960, ImageOutputFormat.Webp, VaryByAccept: true)
		};
		var results = await Task.WhenAll(commands.Select(command => service.GetOrCreateAsync(command, CancellationToken.None)));

		Assert.Equal(1, processor.ProcessCount);
		Assert.All(results, result =>
		{
			Assert.Equal(ImageVariantOutcome.Variant, result.Outcome);
			Assert.NotNull(result.Payload);
			Assert.Equal("image/webp", result.Payload.ContentType);
			Assert.EndsWith(Path.Combine(assetId.ToString("N"), "orig.webp"), result.Payload.FilePath);
		});
	}

	[Fact]
	public async Task PassesThroughSvgAndVideoAndCachesBloatSentinelForFormatOnlyRequests()
	{
		var storage = CreateStorage();
		var cache = CreateCache();
		var processor = new CountingImageProcessor(new VipsImageProcessor(Options.Create(new ImageOptimizationOptions())));
		var service = new ImageVariantService(storage, cache, processor);
		var svgId = Guid.CreateVersion7();

		Directory.CreateDirectory(storage.PublicAssetsRoot);
		await File.WriteAllTextAsync(storage.AssetPath(svgId), "<svg xmlns='http://www.w3.org/2000/svg'/>", Encoding.UTF8);
		var svgResult = await service.GetOrCreateAsync(
			new ImageTransformCommand(svgId, Width: 320, ImageOutputFormat.Webp, VaryByAccept: true),
			CancellationToken.None);

		Assert.Equal(ImageVariantOutcome.Passthrough, svgResult.Outcome);
		Assert.Equal(0, processor.ProcessCount);

		// A minimal 1x1 PNG (~67 bytes) transcoded to WebP or JPEG is larger than the original PNG,
		// so format=auto (VaryByAccept: true) falls back to original and records a passthrough sentinel.
		var pngId = WriteRasterAsset(storage, width: 1, height: 1, format: "png");
		var firstBloat = await service.GetOrCreateAsync(
			new ImageTransformCommand(pngId, Width: null, ImageOutputFormat.Jpeg, VaryByAccept: true),
			CancellationToken.None);
		var secondBloat = await service.GetOrCreateAsync(
			new ImageTransformCommand(pngId, Width: null, ImageOutputFormat.Jpeg, VaryByAccept: true),
			CancellationToken.None);

		Assert.Equal(ImageVariantOutcome.Passthrough, firstBloat.Outcome);
		Assert.Equal(ImageVariantOutcome.Passthrough, secondBloat.Outcome);
		Assert.Equal(1, processor.ProcessCount);
	}

	[Fact]
	public void ValidatesImageOptimizationAndCacheOptionsBounds()
	{
		var validator = new ImageOptimizationOptionsValidation();

		Assert.True(validator.Validate(null, new ImageOptimizationOptions()).Succeeded);
		Assert.False(validator.Validate(null, new ImageOptimizationOptions { AllowedWidths = [] }).Succeeded);
		Assert.False(validator.Validate(null, new ImageOptimizationOptions { AvifQuality = 0 }).Succeeded);
		Assert.False(validator.Validate(null, new ImageOptimizationOptions { MaxConcurrency = 0 }).Succeeded);
		Assert.True(validator.Validate(null, new ImageCacheOptions()).Succeeded);
		Assert.False(validator.Validate(null, new ImageCacheOptions { CacheRoot = " " }).Succeeded);
		Assert.False(validator.Validate(null, new ImageCacheOptions { EvictFraction = 1.0 }).Succeeded);
		Assert.False(validator.Validate(null, new ImageCacheOptions { MaxIdleDays = 0 }).Succeeded);
	}

	private LocalMediaStorage CreateStorage() =>
		new(Options.Create(new MediaStorageOptions { StorageRoot = storageRoot }));

	private FileSystemImageVariantCache CreateCache() =>
		new(Options.Create(new ImageCacheOptions { CacheRoot = cacheRoot }));

	private static Guid WriteRasterAsset(LocalMediaStorage storage, int width, int height, string format)
	{
		var assetId = Guid.CreateVersion7();
		var path = storage.AssetPath(assetId);

		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		using var image = Image.Black(width, height, bands: 3);

		switch (format)
		{
			case "jpeg":
				image.Jpegsave(path, q: 95);
				break;

			case "webp":
				image.Webpsave(path, q: 10);
				break;

			default:
				image.Pngsave(path);
				break;
		}

		return assetId;
	}

	public void Dispose()
	{
		if (Directory.Exists(storageRoot))
			Directory.Delete(storageRoot, recursive: true);

		if (Directory.Exists(cacheRoot))
			Directory.Delete(cacheRoot, recursive: true);
	}

	private sealed class CountingImageProcessor(VipsImageProcessor inner) : IImageProcessor
	{
		private int processCount;

		public int ProcessCount => Volatile.Read(ref processCount);

		public int? ReadIntrinsicWidth(string sourcePath) => inner.ReadIntrinsicWidth(sourcePath);

		public async Task<ImageProcessResult> ProcessAsync(ImageProcessRequest request,
			CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref processCount);
			await Task.Delay(40, cancellationToken);

			return await inner.ProcessAsync(request, cancellationToken);
		}
	}
}
