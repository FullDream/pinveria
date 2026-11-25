using Pinveria.Application.Media;
using Pinveria.Domain.Games;

namespace Pinveria.Application.Games;

internal static class PublicGameMappingExtensions
{
	internal static PublicGameCardDto ToPublicCardDto(this Game game, ImageAssetDto cover, ImageAssetDto? banner,
		int mapsCount, int markersCount, int categoriesCount) =>
		new(game.Id, game.Name, game.Slug, cover, banner, mapsCount, markersCount, categoriesCount);

	internal static PublicGameDto ToPublicDto(this Game game, ImageAssetDto cover, ImageAssetDto? banner,
		int markersCount, int categoriesCount, IReadOnlyList<PublicGameMapDto> maps) =>
		new(game.Id, game.Name, game.Slug, cover, banner, game.ReleaseDate, game.Developer,
			game.Description, maps.Count, markersCount, categoriesCount, maps);
}
