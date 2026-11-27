using Pinveria.Application.Media.Optimization;
using Pinveria.Application.Media.Optimization.Processing;
using Microsoft.Extensions.Options;
using NetVips;

namespace Pinveria.Infrastructure.Media.Optimization;

public sealed class VipsImageProcessor : IImageProcessor, IDisposable
{
	private const int UnboundedThumbnailHeight = 10_000_000;

	private readonly ImageOptimizationOptions options;
	private readonly SemaphoreSlim concurrencyGate;

	static VipsImageProcessor() => Cache.Max = 0;

	public VipsImageProcessor(IOptions<ImageOptimizationOptions> options)
	{
		this.options = options.Value;
		var concurrency = Math.Max(1, this.options.MaxConcurrency);

		concurrencyGate = new SemaphoreSlim(concurrency, concurrency);
	}

	public int? ReadIntrinsicWidth(string sourcePath)
	{
		try
		{
			using var image = Image.NewFromFile(sourcePath, access: Enums.Access.Sequential,
				failOn: Enums.FailOn.Error);

			return image.Width > 0 ? image.Width : null;
		}
		catch (VipsException)
		{
			return null;
		}
	}

	public async Task<ImageProcessResult> ProcessAsync(ImageProcessRequest request,
		CancellationToken cancellationToken)
	{
		var timeout = TimeSpan.FromSeconds(Math.Max(0, options.QueueTimeoutSeconds));

		if (!await concurrencyGate.WaitAsync(timeout, cancellationToken))
			return ImageProcessResult.BusyTimeout();

		try
		{
			return await Task.Run(() => ProcessCore(request, cancellationToken), cancellationToken);
		}
		catch (VipsException) when (cancellationToken.IsCancellationRequested)
		{
			throw new OperationCanceledException(cancellationToken);
		}
		finally
		{
			concurrencyGate.Release();
		}
	}

	private ImageProcessResult ProcessCore(ImageProcessRequest request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		using var header = Image.NewFromFile(request.SourcePath, access: Enums.Access.Sequential,
			failOn: Enums.FailOn.Error);
		var intrinsicWidth = header.Width;
		var hasAlpha = header.HasAlpha();
		var clampedToOriginalWidth = !request.TargetWidth.HasValue || request.TargetWidth.Value >= intrinsicWidth;
		var resolvedFormat = ResolveFormat(request.TargetFormat, request.SourceContentType, hasAlpha);
		var targetContentType = ToContentType(resolvedFormat);

		if (clampedToOriginalWidth && string.Equals(targetContentType, request.SourceContentType, StringComparison.Ordinal))
			return ImageProcessResult.UseOriginal(clampedToOriginalWidth: true);

		using var loaded = clampedToOriginalWidth
			? Image.NewFromFile(request.SourcePath, access: Enums.Access.Sequential, failOn: Enums.FailOn.Error)
			: Image.Thumbnail(request.SourcePath, request.TargetWidth!.Value, height: UnboundedThumbnailHeight,
				size: Enums.Size.Down, failOn: Enums.FailOn.Error);
		using var srgb = loaded.Colourspace(Enums.Interpretation.Srgb);

		cancellationToken.ThrowIfCancellationRequested();
		var bytes = Encode(srgb, resolvedFormat);

		return ImageProcessResult.Encoded(
			bytes,
			targetContentType,
			ToExtension(resolvedFormat),
			clampedToOriginalWidth);
	}

	private byte[] Encode(Image srgb, ImageOutputFormat format)
	{
		switch (format)
		{
			case ImageOutputFormat.Avif:
				return srgb.HeifsaveBuffer(
					q: options.AvifQuality,
					compression: Enums.ForeignHeifCompression.Av1,
					effort: options.AvifEffort,
					keep: Enums.ForeignKeep.None);

			case ImageOutputFormat.Webp:
				return srgb.WebpsaveBuffer(
					q: options.WebpQuality,
					effort: options.WebpEffort,
					keep: Enums.ForeignKeep.None);

			case ImageOutputFormat.Jpeg:
				if (srgb.HasAlpha())
				{
					using var flattened = srgb.Flatten(background: [255, 255, 255]);

					return flattened.JpegsaveBuffer(
						q: options.JpegQuality,
						keep: Enums.ForeignKeep.None);
				}

				return srgb.JpegsaveBuffer(
					q: options.JpegQuality,
					keep: Enums.ForeignKeep.None);

			case ImageOutputFormat.Png:
				return srgb.PngsaveBuffer(keep: Enums.ForeignKeep.None);

			default:
				throw new ArgumentOutOfRangeException(nameof(format), format, "Concrete image output format is required.");
		}
	}

	public static ImageOutputFormat ResolveFormat(ImageOutputFormat requestedFormat, string sourceContentType,
		bool hasAlpha = true)
	{
		return requestedFormat switch
		{
			ImageOutputFormat.Original => sourceContentType switch
			{
				"image/png" => ImageOutputFormat.Png,
				"image/jpeg" => ImageOutputFormat.Jpeg,
				"image/webp" => ImageOutputFormat.Webp,
				"image/avif" => ImageOutputFormat.Avif,
				_ => ImageOutputFormat.Png
			},
			ImageOutputFormat.LegacyFallback => sourceContentType switch
			{
				"image/png" => ImageOutputFormat.Png,
				"image/jpeg" => ImageOutputFormat.Jpeg,
				_ => hasAlpha ? ImageOutputFormat.Png : ImageOutputFormat.Jpeg
			},
			_ => requestedFormat
		};
	}

	public static string ToContentType(ImageOutputFormat format) => format switch
	{
		ImageOutputFormat.Avif => "image/avif",
		ImageOutputFormat.Webp => "image/webp",
		ImageOutputFormat.Jpeg => "image/jpeg",
		ImageOutputFormat.Png => "image/png",
		_ => throw new ArgumentOutOfRangeException(nameof(format), format, "Concrete image output format is required.")
	};

	public static string ToExtension(ImageOutputFormat format) => format switch
	{
		ImageOutputFormat.Avif => "avif",
		ImageOutputFormat.Webp => "webp",
		ImageOutputFormat.Jpeg => "jpeg",
		ImageOutputFormat.Png => "png",
		_ => throw new ArgumentOutOfRangeException(nameof(format), format, "Concrete image output format is required.")
	};

	public void Dispose() => concurrencyGate.Dispose();
}
