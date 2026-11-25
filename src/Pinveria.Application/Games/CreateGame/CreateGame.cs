using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Games.CreateGame;

public sealed record CreateGame(string? Name, string? Slug, Guid? CoverId, Guid? BannerId = null,
	DateOnly? ReleaseDate = null, string? Developer = null, string? Description = null)
	: ICommand<Result<GameDto>>;
