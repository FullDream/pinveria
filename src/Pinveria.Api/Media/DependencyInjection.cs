using Pinveria.Api.Media.Uploads;

namespace Pinveria.Api.Media;

public static class DependencyInjection
{
	public static IServiceCollection AddMediaApi(this IServiceCollection services)
	{
		services.AddScoped<MediaUploadWorkflow>();

		return services;
	}
}
