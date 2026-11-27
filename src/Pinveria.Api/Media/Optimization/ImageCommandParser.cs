using Pinveria.Application.Media.Optimization;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Pinveria.Api.Media.Optimization;

public static class ImageCommandParser
{
	private const string WidthParameter = "width";
	private const string FormatParameter = "format";

	public static ImageCommandParseResult Parse(Guid assetId, IQueryCollection query, StringValues acceptHeader,
		ImageOptimizationOptions options)
	{
		var hasWidth = query.TryGetValue(WidthParameter, out var widthValues);
		var hasFormat = query.TryGetValue(FormatParameter, out var formatValues);

		if (!hasWidth && !hasFormat)
			return ImageCommandParseResult.None();

		int? width = null;

		if (hasWidth)
		{
			if (widthValues.Count != 1 || string.IsNullOrWhiteSpace(widthValues[0]))
				return ImageCommandParseResult.Invalid();

			if (!int.TryParse(widthValues[0], out var parsedWidth) || parsedWidth <= 0 ||
				!options.AllowedWidths.Contains(parsedWidth))
				return ImageCommandParseResult.Invalid();

			width = parsedWidth;
		}

		var outputFormat = ImageOutputFormat.Original;
		var varyByAccept = false;

		if (hasFormat)
		{
			if (formatValues.Count != 1 || string.IsNullOrWhiteSpace(formatValues[0]))
				return ImageCommandParseResult.Invalid();

			var rawFormat = formatValues[0]!.Trim();

			if (rawFormat.Equals("auto", StringComparison.OrdinalIgnoreCase))
			{
				outputFormat = NegotiateAcceptFormat(acceptHeader);
				varyByAccept = true;
			}
			else if (rawFormat.Equals("avif", StringComparison.OrdinalIgnoreCase))
			{
				outputFormat = ImageOutputFormat.Avif;
			}
			else if (rawFormat.Equals("webp", StringComparison.OrdinalIgnoreCase))
			{
				outputFormat = ImageOutputFormat.Webp;
			}
			else if (rawFormat.Equals("jpeg", StringComparison.OrdinalIgnoreCase))
			{
				outputFormat = ImageOutputFormat.Jpeg;
			}
			else if (rawFormat.Equals("png", StringComparison.OrdinalIgnoreCase))
			{
				outputFormat = ImageOutputFormat.Png;
			}
			else
			{
				return ImageCommandParseResult.Invalid();
			}
		}

		return ImageCommandParseResult.Parsed(
			new ImageTransformCommand(assetId, width, outputFormat, varyByAccept));
	}

	private static ImageOutputFormat NegotiateAcceptFormat(StringValues acceptHeader)
	{
		if (StringValues.IsNullOrEmpty(acceptHeader))
			return ImageOutputFormat.LegacyFallback;

		if (MediaTypeHeaderValue.TryParseList(acceptHeader, out var mediaTypes))
		{
			var supportsAvif = false;
			var supportsWebp = false;

			foreach (var mediaType in mediaTypes)
			{
				if (mediaType.Quality is <= 0)
					continue;

				var value = mediaType.MediaType.Value;

				if (string.Equals(value, "image/avif", StringComparison.OrdinalIgnoreCase))
					supportsAvif = true;
				else if (string.Equals(value, "image/webp", StringComparison.OrdinalIgnoreCase))
					supportsWebp = true;
			}

			if (supportsAvif)
				return ImageOutputFormat.Avif;

			if (supportsWebp)
				return ImageOutputFormat.Webp;

			return ImageOutputFormat.LegacyFallback;
		}

		var combined = acceptHeader.ToString();

		if (combined.Contains("image/avif", StringComparison.OrdinalIgnoreCase))
			return ImageOutputFormat.Avif;

		if (combined.Contains("image/webp", StringComparison.OrdinalIgnoreCase))
			return ImageOutputFormat.Webp;

		return ImageOutputFormat.LegacyFallback;
	}
}
