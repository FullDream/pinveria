using Pinveria.Application.Media;

namespace Pinveria.Application.Markers;

public sealed record PublicMarkerDto(Guid Id, Guid CategoryId, string Name, string? Description,
	PositionDto Position, IReadOnlyCollection<MediaAssetDto> Media);
