using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Markers.CreateMarker;

public sealed record CreateMarker(Guid GameId, Guid MapId, Guid? CategoryId, string? Name, PositionInput? Position,
	string? Description, IReadOnlyCollection<Guid?>? Media, Optional<PublicationStatus?> Status = default)
	: ICommand<Result<MarkerDto>>;
