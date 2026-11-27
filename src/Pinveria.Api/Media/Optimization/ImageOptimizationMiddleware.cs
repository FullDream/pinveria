using Pinveria.Application.Media.Optimization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Pinveria.Api.Media.Optimization;

public sealed class ImageOptimizationMiddleware(
	RequestDelegate next,
	IImageVariantService variantService,
	IOptions<ImageOptimizationOptions> options)
{
	public const string AssetIdItemKey = "Pinveria.Media.AssetId";
	public const string BusyFallbackItemKey = "Pinveria.Media.BusyFallback";
	public const string VaryByAcceptItemKey = "Pinveria.Media.VaryByAccept";

	private const string ImmutableCacheControl = "public,max-age=31536000,immutable";

	private readonly ImageOptimizationOptions optimizationOptions = options.Value;

	public async Task InvokeAsync(HttpContext context)
	{
		var method = context.Request.Method;

		if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
		{
			await next(context);

			return;
		}

		if (context.Items[AssetIdItemKey] is not Guid assetId)
		{
			await next(context);

			return;
		}

		var parseResult = ImageCommandParser.Parse(
			assetId,
			context.Request.Query,
			context.Request.Headers.Accept,
			optimizationOptions);

		if (parseResult.Outcome == ImageCommandParseOutcome.None)
		{
			await next(context);

			return;
		}

		if (parseResult.Outcome == ImageCommandParseOutcome.Invalid || parseResult.Command is null)
		{
			context.Response.StatusCode = StatusCodes.Status400BadRequest;

			return;
		}

		var result = await variantService.GetOrCreateAsync(parseResult.Command, context.RequestAborted);

		switch (result.Outcome)
		{
			case ImageVariantOutcome.NotFound:
				context.Response.StatusCode = StatusCodes.Status404NotFound;

				return;

			case ImageVariantOutcome.Passthrough:
				if (result.VaryByAccept)
					context.Items[VaryByAcceptItemKey] = true;

				await next(context);

				return;

			case ImageVariantOutcome.BusyFallback:
				context.Items[BusyFallbackItemKey] = true;

				if (result.VaryByAccept)
					context.Items[VaryByAcceptItemKey] = true;

				await next(context);

				return;

			case ImageVariantOutcome.Variant when result.Payload is not null:
				await ServeVariantAsync(context, result.Payload);

				return;

			default:
				await next(context);

				return;
		}
	}

	private static async Task ServeVariantAsync(HttpContext context, ImageVariantPayload payload)
	{
		var headers = context.Response.Headers;

		headers.XContentTypeOptions = "nosniff";
		headers.CacheControl = ImmutableCacheControl;
		headers.ETag = payload.ETag;

		if (payload.VaryByAccept)
			headers.Vary = "Accept";

		if (MatchesIfNoneMatch(context.Request.Headers.IfNoneMatch, payload.ETag))
		{
			context.Response.StatusCode = StatusCodes.Status304NotModified;

			return;
		}

		context.Response.StatusCode = StatusCodes.Status200OK;
		context.Response.ContentType = payload.ContentType;
		context.Response.ContentLength = payload.ByteLength;

		if (HttpMethods.IsGet(context.Request.Method))
			await context.Response.SendFileAsync(payload.FilePath, 0, payload.ByteLength, context.RequestAborted);
	}

	private static bool MatchesIfNoneMatch(StringValues ifNoneMatchValues, string expectedETag)
	{
		if (StringValues.IsNullOrEmpty(ifNoneMatchValues))
			return false;

		foreach (var headerValue in ifNoneMatchValues)
		{
			if (string.IsNullOrWhiteSpace(headerValue))
				continue;

			foreach (var candidate in headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			{
				if (candidate == "*" || string.Equals(candidate, expectedETag, StringComparison.Ordinal))
					return true;
			}
		}

		return false;
	}
}
