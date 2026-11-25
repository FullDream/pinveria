using Pinveria.Application.Media;
using Pinveria.Domain;

namespace Pinveria.Application.Categories.Groups;

public sealed record CategoryGroupDto(
	Guid Id,
	Guid MapId,
	string Name,
	string Color,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	ImageAssetDto? Icon,
	int MarkersCount,
	IReadOnlyCollection<CategoryDto> Categories,
	PublicationStatus Status);
