using Pinveria.Application.Media;
using Pinveria.Domain;

namespace Pinveria.Application.Categories;

public sealed record CategoryDto(
	Guid Id,
	Guid CategoryGroupId,
	string Name,
	ImageAssetDto? Icon,
	string? Color,
	int MarkersCount,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	PublicationStatus Status);
