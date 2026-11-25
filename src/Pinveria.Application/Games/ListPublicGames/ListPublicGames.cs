using Pinveria.Application.Common.Pagination;
using Pinveria.Application.Common.Sorting;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Games.ListPublicGames;

public sealed record ListPublicGames(
	int Offset = 0,
	int Limit = 20,
	string? Search = null,
	GameSortBy? SortBy = null,
	SortDirection? SortDirection = null) : IQuery<Result<PaginatedResult<PublicGameCardDto>>>;
