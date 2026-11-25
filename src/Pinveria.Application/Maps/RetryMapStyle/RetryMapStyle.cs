using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.RetryMapStyle;

public sealed record RetryMapStyle(Guid GameId, Guid MapId, Guid StyleId) : ICommand<Result<MapStyleDto>>;
