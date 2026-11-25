namespace Pinveria.Api.Hosting;

public sealed class HostingOptions
{
	public string? DataProtectionKeysPath { get; init; }
	public bool UseHttpsRedirection { get; init; } = true;
	public string[] KnownProxies { get; init; } = [];
	public string[] KnownNetworks { get; init; } = [];
}
