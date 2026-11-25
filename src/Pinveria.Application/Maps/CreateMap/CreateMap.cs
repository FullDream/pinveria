using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.CreateMap;

public sealed record CreateMap(Guid GameId, string? Name, string? Slug,
	Optional<PublicationStatus?> Status = default, string? Description = null) : ICommand<Result<MapDto>>;