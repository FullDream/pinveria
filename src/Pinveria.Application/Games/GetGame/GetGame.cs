using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Games.GetGame;

public sealed record GetGame(Guid Id) : IQuery<Result<GameDto>>;
