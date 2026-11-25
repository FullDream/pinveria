using Pinveria.Api.Results;
using Pinveria.Application.Common.Pagination;
using Pinveria.Application.Games;
using Pinveria.Application.Games.GetPublicGame;
using Pinveria.Application.Games.ListPublicGames;
using Mediator;

namespace Pinveria.Api.Games;

public static class PublicEndpoints
{
	public static RouteGroupBuilder MapPublicGames(this RouteGroupBuilder group)
	{
		var games = group.MapGroup("/public/games").WithTags("Public Games").AllowAnonymous();

		games.MapGet("", ListAsync).WithName("ListPublicGames")
			.WithSummary("List published game cards");

		games.MapGet("/{gameSlug}", GetAsync).WithName("GetPublicGame")
			.ProducesProblem(StatusCodes.Status404NotFound).WithSummary("Get a published game by slug");

		return games;
	}

	private static async Task<OutcomeResult<PaginatedResult<PublicGameCardDto>>> ListAsync(
		[AsParameters] ListGamesRequest request, IMediator mediator, CancellationToken cancellationToken) =>
		await mediator.Send(new ListPublicGames(request.Offset ?? 0, request.Limit ?? 20,
			request.Search, request.SortBy, request.SortDirection), cancellationToken);

	private static async Task<OutcomeResult<PublicGameDto>> GetAsync(
		string gameSlug, IMediator mediator, CancellationToken cancellationToken) =>
		await mediator.Send(new GetPublicGame(gameSlug), cancellationToken);
}
