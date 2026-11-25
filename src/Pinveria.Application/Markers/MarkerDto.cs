using Pinveria.Application.Media;
using Pinveria.Domain;

namespace Pinveria.Application.Markers;

public sealed record MarkerDto(Guid Id, Guid CategoryId, string Name, string? Description, PositionDto Position,
	IReadOnlyCollection<MediaAssetDto> Media, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
	PublicationStatus Status);
