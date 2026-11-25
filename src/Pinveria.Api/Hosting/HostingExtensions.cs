using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Pinveria.Api.Hosting;

public static class HostingExtensions
{
	public static IServiceCollection AddHosting(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddOptions<HostingOptions>().Bind(configuration.GetSection("Hosting")).ValidateOnStart();
		services.AddSingleton<IValidateOptions<HostingOptions>, HostingOptionsValidation>();

		var dataProtection = services.AddDataProtection().SetApplicationName("Pinveria");
		var keysPath = configuration["Hosting:DataProtectionKeysPath"];

		if (!string.IsNullOrWhiteSpace(keysPath))
		{
			var directory = Directory.CreateDirectory(keysPath);

			dataProtection.PersistKeysToFileSystem(directory);
		}

		services
			.AddOptions<ForwardedHeadersOptions>()
			.Configure<IOptions<HostingOptions>>((forwarded, hosting) =>
			{
				forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

				foreach (var proxy in hosting.Value.KnownProxies)
					forwarded.KnownProxies.Add(IPAddress.Parse(proxy));

				foreach (var network in hosting.Value.KnownNetworks)
					forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
			});

		return services;
	}
}
