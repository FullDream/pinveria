using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Games.DeleteGame;

public sealed record DeleteGame(Guid Id) : ICommand<Result>;
