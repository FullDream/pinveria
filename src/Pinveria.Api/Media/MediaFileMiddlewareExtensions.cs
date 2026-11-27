using Pinveria.Api.Media.Optimization;
using Pinveria.Application.Media.Storage;
using Microsoft.Extensions.FileProviders;

namespace Pinveria.Api.Media;

public static class MediaFileMiddlewareExtensions
{
	public static IApplicationBuilder UseMediaFiles(this WebApplication app)
	{
		var storage = app.Services.GetRequiredService<IMediaStorage>();
		var files = new PhysicalFileProvider(storage.PublicAssetsRoot);

		app.Lifetime.ApplicationStopped.Register(files.Dispose);

		return app.Map("/media/files", branch =>
		{
			branch.Use(async (context, next) =>
			{
				var value = context.Request.Path.Value;

				if (value is null || value.Length < 2 ||
					!Guid.TryParseExact(value.AsSpan(1), "D", out var id) || id == Guid.Empty)
				{
					context.Response.StatusCode = StatusCodes.Status404NotFound;

					return;
				}

				var originalPath = context.Request.Path;

				context.Items[ImageOptimizationMiddleware.AssetIdItemKey] = id;
				context.Request.Path = $"/{id:N}";

				try
				{
					await next(context);
				}
				finally
				{
					context.Request.Path = originalPath;
				}
			});

			branch.UseMiddleware<ImageOptimizationMiddleware>();

			branch.UseStaticFiles(new StaticFileOptions
			{
				FileProvider = files,
				ContentTypeProvider = new MediaFileContentTypeProvider(storage),
				OnPrepareResponse = context =>
				{
					var httpContext = context.Context;
					var headers = httpContext.Response.Headers;

					headers.XContentTypeOptions = "nosniff";
					headers.CacheControl = httpContext.Items.ContainsKey(ImageOptimizationMiddleware.BusyFallbackItemKey)
						? "no-store"
						: "public,max-age=31536000,immutable";

					if (httpContext.Items.ContainsKey(ImageOptimizationMiddleware.VaryByAcceptItemKey))
						headers.Vary = "Accept";

					if (httpContext.Response.ContentType == "image/svg+xml")
						headers.ContentSecurityPolicy =
							"sandbox; default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
				}
			});
		});
	}
}
