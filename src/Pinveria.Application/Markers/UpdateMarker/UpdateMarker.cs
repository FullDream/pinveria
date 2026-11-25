using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Markers.UpdateMarker;

public sealed record UpdateMarker(Guid GameId, Guid MapId, Guid Id, Optional<Guid?> CategoryId = default,
	Optional<string?> Name = default, Optional<string?> Description = default,
	Optional<PositionPatch?> Position = default, Optional<IReadOnlyCollection<Guid?>?> Media = default,
	Optional<PublicationStatus?> Status = default)
	: ICommand<Result<MarkerDto>>;
