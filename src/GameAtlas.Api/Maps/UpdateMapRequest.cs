using System.ComponentModel;
using System.Text.Json.Serialization;
using GameAtlas.Domain;

namespace GameAtlas.Api.Maps;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[Description("Supply name, slug, description, defaultStyleId, styleOrder, or status. Null is invalid for required fields; geometry is immutable.")]
public sealed record UpdateMapRequest
{
	public Optional<string?> Name { get; init; }
	public Optional<string?> Slug { get; init; }
	public Optional<string?> Description { get; init; }
	public Optional<Guid?> DefaultStyleId { get; init; }
	public Optional<IReadOnlyList<Guid?>?> StyleOrder { get; init; }
	public Optional<PublicationStatus?> Status { get; init; }
}