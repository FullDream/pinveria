using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.GetMap;

public sealed record GetMap(Guid GameId, Guid MapId) : IQuery<Result<MapDto>>;
