using Pinveria.Application.Media;

namespace Pinveria.Application.Categories;

public sealed record PublicCategoryDto(Guid Id, Guid CategoryGroupId, string Name, ImageAssetDto? Icon,
	string? Color, int MarkersCount);
