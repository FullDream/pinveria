using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.UpdateMap;

public sealed record UpdateMap(Guid GameId, Guid MapId, Optional<string?> Name = default,
	Optional<Guid?> DefaultStyleId = default, Optional<IReadOnlyList<Guid?>?> StyleOrder = default,
	Optional<string?> Slug = default, Optional<PublicationStatus?> Status = default,
	Optional<string?> Description = default) : ICommand<Result<MapDto>>;