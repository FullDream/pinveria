using Pinveria.Application.Media.Optimization;
using Pinveria.Application.Media.Optimization.Processing;
using Pinveria.Infrastructure.Media.Optimization;
using Microsoft.Extensions.Options;
using NetVips;

namespace Pinveria.IntegrationTests;

public sealed class VipsImageProcessorTests : IDisposable
{
	private readonly string root = Path.Combine(Path.GetTempPath(), $"vips-processor-{Guid.NewGuid():N}");

	public VipsImageProcessorTests() => Directory.CreateDirectory(root);

	[Theory]
	[InlineData(ImageOutputFormat.Avif, "image/avif", "avif")]
	[InlineData(ImageOutputFormat.Webp, "image/webp", "webp")]
	[InlineData(ImageOutputFormat.Jpeg, "image/jpeg", "jpeg")]
	[InlineData(ImageOutputFormat.Png, "image/png", "png")]
	public async Task DownscalesAndEncodesToRequestedFormatWhilePreservingAspectRatio(
		ImageOutputFormat targetFormat,
		string expectedContentType,
		string expectedExtension)
	{
		using var processor = new VipsImageProcessor(Options.Create(new ImageOptimizationOptions()));
		var sourcePath = CreateRgbaImage("source.png", width: 800, height: 400, includeTransparency: false);
		var result = await processor.ProcessAsync(
			new ImageProcessRequest(sourcePath, "image/png", TargetWidth: 320, targetFormat),
			CancellationToken.None);

		Assert.Equal(ImageProcessStatus.Encoded, result.Status);
		Assert.False(result.ClampedToOriginalWidth);
		Assert.Equal(expectedContentType, result.ContentType);
		Assert.Equal(expectedExtension, result.Extension);
		Assert.Equal(expectedContentType, MediaSignatureDetector.Detect(result.Bytes));
		using var decoded = Image.NewFromBuffer(result.Bytes!);

		Assert.Equal(320, decoded.Width);
		Assert.Equal(160, decoded.Height);
	}

	[Fact]
	public async Task ClampsTargetWidthAboveOrEqualToIntrinsicDimensionsWithoutUpscaling()
	{
		using var processor = new VipsImageProcessor(Options.Create(new ImageOptimizationOptions()));
		var sourcePath = CreateRgbaImage("small.jpg", width: 400, height: 300, includeTransparency: false, format: "jpeg");
		var transcoded = await processor.ProcessAsync(
			new ImageProcessRequest(sourcePath, "image/jpeg", TargetWidth: 640, ImageOutputFormat.Webp),
			CancellationToken.None);
		var sameFormat = await processor.ProcessAsync(
			new ImageProcessRequest(sourcePath, "image/jpeg", TargetWidth: 640, ImageOutputFormat.Original),
			CancellationToken.None);

		Assert.Equal(ImageProcessStatus.Encoded, transcoded.Status);
		Assert.True(transcoded.ClampedToOriginalWidth);
		using var decoded = Image.NewFromBuffer(transcoded.Bytes!);

		Assert.Equal(400, decoded.Width);
		Assert.Equal(300, decoded.Height);
		Assert.Equal(ImageProcessStatus.UseOriginal, sameFormat.Status);
		Assert.True(sameFormat.ClampedToOriginalWidth);
	}

	[Fact]
	public async Task LegacyFallbackPreservesTransparencyAsPngAndFlattensOnlyExplicitJpeg()
	{
		using var processor = new VipsImageProcessor(Options.Create(new ImageOptimizationOptions()));
		var sourcePath = CreateRgbaImage("alpha.webp", width: 640, height: 480, includeTransparency: true, format: "webp");
		var fallbackResult = await processor.ProcessAsync(
			new ImageProcessRequest(sourcePath, "image/webp", TargetWidth: 320, ImageOutputFormat.LegacyFallback),
			CancellationToken.None);
		var explicitJpegResult = await processor.ProcessAsync(
			new ImageProcessRequest(sourcePath, "image/webp", TargetWidth: 320, ImageOutputFormat.Jpeg),
			CancellationToken.None);

		Assert.Equal(ImageProcessStatus.Encoded, fallbackResult.Status);
		Assert.Equal("image/png", fallbackResult.ContentType);
		using var fallbackImage = Image.NewFromBuffer(fallbackResult.Bytes!);

		Assert.True(fallbackImage.HasAlpha());
		Assert.Equal(0d, fallbackImage[319, 239][3]);
		Assert.Equal(ImageProcessStatus.Encoded, explicitJpegResult.Status);
		Assert.Equal("image/jpeg", explicitJpegResult.ContentType);
	}

	[Fact]
	public async Task ReturnsBusyTimeoutWhenQueueWaitExceedsTimeout()
	{
		var options = new ImageOptimizationOptions
		{
			MaxConcurrency = 1,
			QueueTimeoutSeconds = 0
		};
		using var processor = new VipsImageProcessor(Options.Create(options));
		var largePath = CreateRgbaImage("heavy.png", width: 3200, height: 3200, includeTransparency: true);
		var smallPath = CreateRgbaImage("quick.png", width: 640, height: 480, includeTransparency: false);
		var first = processor.ProcessAsync(
			new ImageProcessRequest(largePath, "image/png", TargetWidth: 1920, ImageOutputFormat.Avif),
			CancellationToken.None);

		var timedOut = false;

		for (var attempt = 0; attempt < 15 && !first.IsCompleted; attempt++)
		{
			var contender = await processor.ProcessAsync(
				new ImageProcessRequest(smallPath, "image/png", TargetWidth: 320, ImageOutputFormat.Webp),
				CancellationToken.None);

			if (contender.Status == ImageProcessStatus.BusyTimeout)
			{
				timedOut = true;
				break;
			}
		}

		var firstResult = await first;

		Assert.Equal(ImageProcessStatus.Encoded, firstResult.Status);
		Assert.True(timedOut);
	}

	private string CreateRgbaImage(string fileName, int width, int height, bool includeTransparency,
		string format = "png")
	{
		var pixels = new byte[width * height * 4];

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var offset = (y * width + x) * 4;

				pixels[offset] = (byte)(x * 255 / Math.Max(1, width - 1));
				pixels[offset + 1] = (byte)(y * 255 / Math.Max(1, height - 1));
				pixels[offset + 2] = 128;
				pixels[offset + 3] = includeTransparency && x >= width / 2 && y >= height / 2 ? (byte)0 : (byte)255;
			}
		}

		using var image = Image.NewFromMemory(pixels, width, height, 4, Enums.BandFormat.Uchar);
		var path = Path.Combine(root, fileName);

		switch (format)
		{
			case "png":
				image.Pngsave(path);
				break;

			case "jpeg":
				using (var rgb = image.ExtractBand(0, n: 3))
					rgb.Jpegsave(path);
				break;

			case "webp":
				image.Webpsave(path, lossless: true);
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(format));
		}

		return path;
	}

	public void Dispose()
	{
		if (Directory.Exists(root))
			Directory.Delete(root, recursive: true);
	}
}
