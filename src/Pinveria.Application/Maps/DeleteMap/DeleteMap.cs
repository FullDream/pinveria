using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.DeleteMap;

public sealed record DeleteMap(Guid GameId, Guid MapId) : ICommand<Result>;
