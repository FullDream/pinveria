using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.AddMapStyle;

public sealed record AddMapStyle(Guid GameId, Guid MapId, string? Name, string? BackgroundColor, Guid? SourceAssetId)
	: ICommand<Result<MapDto>>;
