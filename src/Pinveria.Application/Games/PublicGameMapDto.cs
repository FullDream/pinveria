using Pinveria.Application.Media;

namespace Pinveria.Application.Games;

public sealed record PublicGameMapDto(Guid Id, string Name, string Slug, ImageAssetDto? Preview,
	int MarkersCount, int CategoriesCount);
