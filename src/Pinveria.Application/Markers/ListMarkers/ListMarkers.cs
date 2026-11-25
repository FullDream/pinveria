using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Markers.ListMarkers;

public sealed record ListMarkers(Guid GameId, Guid MapId) : IQuery<Result<IReadOnlyCollection<MarkerDto>>>;
