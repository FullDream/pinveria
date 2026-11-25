using Pinveria.Application.Games.CreateGame;
using Pinveria.Application.Games.ListGames;
using Pinveria.Application.Games.UpdateGame;

namespace Pinveria.Api.Games;

internal static class GameMappingExtensions
{
	internal static CreateGame ToCommand(this CreateGameRequest request) =>
		new(request.Name, request.Slug, request.CoverId, request.BannerId, request.ReleaseDate, request.Developer, request.Description);

	internal static ListGames ToQuery(this ListGamesRequest request) =>
		new(request.Offset ?? 0, request.Limit ?? 20, request.Search, request.SortBy, request.SortDirection);

	internal static UpdateGame ToCommand(this UpdateGameRequest request, Guid id) =>
		new(id, request.Name, request.Slug, request.CoverId, request.Status, request.BannerId, request.ReleaseDate, request.Developer, request.Description);
}
