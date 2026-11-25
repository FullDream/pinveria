using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.ListMaps;

public sealed record ListMaps(Guid GameId) : IQuery<Result<IReadOnlyCollection<MapDto>>>;
