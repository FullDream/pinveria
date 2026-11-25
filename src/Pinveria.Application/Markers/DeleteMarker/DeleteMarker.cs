using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Markers.DeleteMarker;

public sealed record DeleteMarker(Guid GameId, Guid MapId, Guid Id) : ICommand<Result>;
