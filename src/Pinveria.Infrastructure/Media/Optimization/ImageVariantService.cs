using System.Collections.Concurrent;
using Pinveria.Application.Media.Optimization;
using Pinveria.Application.Media.Optimization.Caching;
using Pinveria.Application.Media.Optimization.Processing;
using Pinveria.Application.Media.Storage;

namespace Pinveria.Infrastructure.Media.Optimization;

public sealed class ImageVariantService(
	IMediaStorage storage,
	IImageVariantCache cache,
	IImageProcessor processor) : IImageVariantService
{
	private readonly ConcurrentDictionary<ImageCacheKey, Lazy<Task<ImageVariantResult>>> inflight = new();

	public async Task<ImageVariantResult> GetOrCreateAsync(ImageTransformCommand command,
		CancellationToken cancellationToken)
	{
		var sourcePath = storage.AssetPath(command.AssetId);

		if (!File.Exists(sourcePath))
			return ImageVariantResult.NotFound();

		var sourceContentType = MediaSignatureDetector.DetectFile(sourcePath);

		if (!MediaSignatureDetector.IsRasterContentType(sourceContentType))
			return ImageVariantResult.Passthrough();

		var resolvedFormat = ResolveInitialOutputFormat(command.OutputFormat, sourceContentType);
		var targetContentType = VipsImageProcessor.ToContentType(resolvedFormat);
		var targetExtension = VipsImageProcessor.ToExtension(resolvedFormat);

		if (!command.Width.HasValue &&
			string.Equals(targetContentType, sourceContentType, StringComparison.Ordinal))
			return ImageVariantResult.Passthrough(command.VaryByAccept);

		var requestedKey = new ImageCacheKey(command.AssetId, command.Width, targetExtension);

		if (TryResolveFromCache(requestedKey, targetContentType, command.OutputFormat, command.VaryByAccept,
			out var cachedResult))
			return cachedResult;

		var effectiveWidth = command.Width;

		if (effectiveWidth.HasValue)
		{
			var intrinsicWidth = processor.ReadIntrinsicWidth(sourcePath);

			if (!intrinsicWidth.HasValue)
				return ImageVariantResult.Passthrough(command.VaryByAccept);

			if (effectiveWidth.Value >= intrinsicWidth.Value)
			{
				effectiveWidth = null;

				if (string.Equals(targetContentType, sourceContentType, StringComparison.Ordinal))
					return ImageVariantResult.Passthrough(command.VaryByAccept);
			}
		}

		var canonicalKey = new ImageCacheKey(command.AssetId, effectiveWidth, targetExtension);

		if (canonicalKey != requestedKey &&
			TryResolveFromCache(canonicalKey, targetContentType, command.OutputFormat, command.VaryByAccept,
				out var foldedCachedResult))
			return foldedCachedResult;

		var lazyTask = inflight.GetOrAdd(canonicalKey,
			key => new Lazy<Task<ImageVariantResult>>(
				() => ProduceVariantAsync(
					key,
					sourcePath,
					sourceContentType,
					command.OutputFormat,
					command.VaryByAccept),
				LazyThreadSafetyMode.ExecutionAndPublication));

		try
		{
			return await lazyTask.Value.WaitAsync(cancellationToken);
		}
		finally
		{
			inflight.TryRemove(new KeyValuePair<ImageCacheKey, Lazy<Task<ImageVariantResult>>>(canonicalKey, lazyTask));
		}
	}

	private async Task<ImageVariantResult> ProduceVariantAsync(
		ImageCacheKey key,
		string sourcePath,
		string sourceContentType,
		ImageOutputFormat requestedFormat,
		bool varyByAccept)
	{
		if (TryResolveFromCache(key, ToContentTypeFromExtension(key.Extension), requestedFormat, varyByAccept,
			out var existing))
			return existing;

		var processResult = await processor.ProcessAsync(
			new ImageProcessRequest(sourcePath, sourceContentType, key.Width, requestedFormat),
			CancellationToken.None);

		switch (processResult.Status)
		{
			case ImageProcessStatus.BusyTimeout:
				return ImageVariantResult.BusyFallback(varyByAccept);

			case ImageProcessStatus.UseOriginal:
				return ImageVariantResult.Passthrough(varyByAccept);

			case ImageProcessStatus.Encoded:
				var encodedBytes = processResult.Bytes!;
				var encodedContentType = processResult.ContentType!;
				var actualKey = processResult.ClampedToOriginalWidth
					? new ImageCacheKey(key.AssetId, null, processResult.Extension!)
					: new ImageCacheKey(key.AssetId, key.Width, processResult.Extension!);

				if (!actualKey.Width.HasValue && varyByAccept)
				{
					var originalLength = new FileInfo(sourcePath).Length;

					if (encodedBytes.Length >= originalLength)
					{
						await cache.StorePassthroughSentinelAsync(actualKey, CancellationToken.None);

						return ImageVariantResult.Passthrough(varyByAccept);
					}
				}

				var stored = await cache.StoreAsync(actualKey, encodedBytes, CancellationToken.None);

				return ImageVariantResult.Variant(new ImageVariantPayload(
					stored.FilePath!,
					encodedContentType,
					stored.ByteLength,
					stored.ETag!,
					varyByAccept));

			default:
				return ImageVariantResult.Passthrough(varyByAccept);
		}
	}

	private bool TryResolveFromCache(
		ImageCacheKey key,
		string contentType,
		ImageOutputFormat requestedFormat,
		bool varyByAccept,
		out ImageVariantResult result)
	{
		if (TryResolveExactCacheKey(key, contentType, varyByAccept, out result))
			return true;

		if (requestedFormat == ImageOutputFormat.LegacyFallback && key.Extension == "png")
		{
			var jpegKey = key with { Extension = "jpeg" };

			if (TryResolveExactCacheKey(jpegKey, "image/jpeg", varyByAccept, out result))
				return true;
		}

		result = null!;

		return false;
	}

	private bool TryResolveExactCacheKey(ImageCacheKey key, string contentType, bool varyByAccept,
		out ImageVariantResult result)
	{
		var lookup = cache.TryGet(key);

		switch (lookup.Outcome)
		{
			case ImageCacheLookupOutcome.Hit:
				result = ImageVariantResult.Variant(new ImageVariantPayload(
					lookup.FilePath!,
					contentType,
					lookup.ByteLength,
					lookup.ETag!,
					varyByAccept));
				return true;

			case ImageCacheLookupOutcome.PassthroughSentinel:
				result = ImageVariantResult.Passthrough(varyByAccept);
				return true;

			default:
				result = null!;
				return false;
		}
	}

	private static ImageOutputFormat ResolveInitialOutputFormat(ImageOutputFormat requestedFormat,
		string sourceContentType) =>
		requestedFormat switch
		{
			ImageOutputFormat.Original => VipsImageProcessor.ResolveFormat(ImageOutputFormat.Original, sourceContentType),
			ImageOutputFormat.LegacyFallback => sourceContentType is "image/jpeg"
				? ImageOutputFormat.Jpeg
				: ImageOutputFormat.Png,
			_ => requestedFormat
		};

	private static string ToContentTypeFromExtension(string extension) => extension switch
	{
		"avif" => "image/avif",
		"webp" => "image/webp",
		"jpeg" => "image/jpeg",
		"png" => "image/png",
		_ => "application/octet-stream"
	};
}
