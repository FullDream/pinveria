using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.PublishMap;

public sealed record PublishMap(Guid GameId, Guid MapId) : ICommand<Result<MapDto>>;
