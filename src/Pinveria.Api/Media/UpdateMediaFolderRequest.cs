using System.Text.Json.Serialization;
using Pinveria.Domain;

namespace Pinveria.Api.Media;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateMediaFolderRequest
{
	public Optional<string?> Name { get; init; }
	public Optional<Guid?> ParentId { get; init; }
}
