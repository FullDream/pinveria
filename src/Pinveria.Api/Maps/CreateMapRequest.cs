using System.Text.Json.Serialization;
using Pinveria.Domain;

namespace Pinveria.Api.Maps;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateMapRequest(string? Name, string? Slug, string? Description = null)
{
	public Optional<PublicationStatus?> Status { get; init; }
}