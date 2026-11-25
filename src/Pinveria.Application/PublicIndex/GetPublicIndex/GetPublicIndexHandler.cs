using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Pinveria.Application.PublicIndex.GetPublicIndex;

public sealed class GetPublicIndexHandler(IApplicationDbContext context)
	: IQueryHandler<GetPublicIndex, Result<PublicIndexDto>>
{
	public async ValueTask<Result<PublicIndexDto>> Handle(GetPublicIndex request, CancellationToken cancellationToken)
	{
		var games = await context.Games.AsNoTracking()
			.Where(game => game.Status == PublicationStatus.Published)
			.OrderBy(game => game.CreatedAt)
			.ThenBy(game => game.Id)
			.Select(game => new
			{
				game.Id,
				Slug = (string)game.Slug,
				game.UpdatedAt
			})
			.ToArrayAsync(cancellationToken);

		if (games.Length == 0)
			return new PublicIndexDto([]);

		var gameIds = games.Select(game => game.Id).ToArray();

		var maps = await context.Maps.AsNoTracking()
			.Where(map => gameIds.Contains(map.GameId) && map.Status == PublicationStatus.Published)
			.OrderBy(map => map.CreatedAt)
			.ThenBy(map => map.Id)
			.Select(map => new
			{
				map.GameId,
				Slug = (string)map.Slug,
				map.UpdatedAt
			})
			.ToArrayAsync(cancellationToken);

		var mapsByGame = maps.ToLookup(map => map.GameId,
			map => new PublicIndexMapDto(map.Slug, map.UpdatedAt));

		var result = games.Select(game => new PublicIndexGameDto(
			game.Slug,
			game.UpdatedAt,
			mapsByGame[game.Id].ToArray())).ToArray();

		return new PublicIndexDto(result);
	}
}