using Pinveria.Api.Maps.Events;
using Pinveria.Application.Maps.Processing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinveria.Api.Maps;

public static class DependencyInjection
{
	public static IServiceCollection AddMapsApi(this IServiceCollection services)
	{
		services.AddSignalR().AddJsonProtocol(options =>
		{
			options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
		});
		services.AddSingleton<IMapNotifications, MapNotifications>();

		return services;
	}
}
