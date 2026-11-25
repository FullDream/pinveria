using System.Text.Json.Serialization;
using Pinveria.Domain;

namespace Pinveria.Api.Markers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PositionPatchRequest
{
	public Optional<double?> X { get; init; }
	public Optional<double?> Y { get; init; }
}
