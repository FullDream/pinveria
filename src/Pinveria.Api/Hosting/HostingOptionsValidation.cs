using System.Net;
using Microsoft.Extensions.Options;

namespace Pinveria.Api.Hosting;

public sealed class HostingOptionsValidation : IValidateOptions<HostingOptions>
{
	public ValidateOptionsResult Validate(string? name, HostingOptions options)
	{
		var failures = new List<string>();

		if (options.DataProtectionKeysPath is not null && string.IsNullOrWhiteSpace(options.DataProtectionKeysPath))
			failures.Add("Hosting:DataProtectionKeysPath must not be whitespace when specified.");

		foreach (var proxy in options.KnownProxies)
		{
			if (string.IsNullOrWhiteSpace(proxy) || !IPAddress.TryParse(proxy, out _))
				failures.Add($"Hosting:KnownProxies contains an invalid IP address: '{proxy}'.");
		}

		foreach (var network in options.KnownNetworks)
		{
			if (string.IsNullOrWhiteSpace(network) || !IPNetwork.TryParse(network, out _))
				failures.Add($"Hosting:KnownNetworks contains an invalid CIDR network: '{network}'.");
		}

		return failures.Count == 0
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(failures);
	}
}
