using Pinveria.Application.Media.Optimization;
using Pinveria.Application.Media.Optimization.Caching;
using Microsoft.Extensions.Options;

namespace Pinveria.Infrastructure.Media.Optimization;

public sealed class ImageOptimizationOptionsValidation :
	IValidateOptions<ImageOptimizationOptions>,
	IValidateOptions<ImageCacheOptions>
{
	public ValidateOptionsResult Validate(string? name, ImageOptimizationOptions options) =>
		options.AllowedWidths is { Length: > 0 } &&
		options.AllowedWidths.All(width => width is >= 16 and <= 16384) &&
		options.AvifQuality is >= 1 and <= 100 &&
		options.AvifEffort is >= 0 and <= 9 &&
		options.WebpQuality is >= 1 and <= 100 &&
		options.WebpEffort is >= 0 and <= 6 &&
		options.JpegQuality is >= 1 and <= 100 &&
		options.MaxConcurrency is >= 1 and <= 32 &&
		options.QueueTimeoutSeconds is >= 0 and <= 120
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail("Image optimization options must be positive and within supported bounds.");

	public ValidateOptionsResult Validate(string? name, ImageCacheOptions options) =>
		!string.IsNullOrWhiteSpace(options.CacheRoot) &&
		options.MaxBytes >= 65536 &&
		options.EvictFraction is > 0.0 and < 1.0 &&
		options.MaxIdleDays is >= 1 and <= 3650 &&
		options.TouchIntervalMinutes is >= 1 and <= 10080 &&
		options.SweepIntervalMinutes is >= 1 and <= 10080
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail("Image cache options must specify a valid cache root and positive bounds.");
}
