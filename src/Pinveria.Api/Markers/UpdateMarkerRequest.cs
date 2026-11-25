using System.Text.Json.Serialization;
using Pinveria.Domain;

namespace Pinveria.Api.Markers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateMarkerRequest
{
	public Optional<Guid?> CategoryId { get; init; }
	public Optional<string?> Name { get; init; }
	public Optional<string?> Description { get; init; }
	public Optional<PositionPatchRequest?> Position { get; init; }
	public Optional<IReadOnlyCollection<Guid?>?> Media { get; init; }
	public Optional<PublicationStatus?> Status { get; init; }
}
