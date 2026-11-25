using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.DeleteMapStyle;

public sealed record DeleteMapStyle(Guid GameId, Guid MapId, Guid StyleId) : ICommand<Result>;
