using Pinveria.Domain;

namespace Pinveria.Application.Maps;

public sealed record MapDto(
	Guid Id,
	Guid GameId,
	string Name,
	string Slug,
	string? Description,
	Guid? DefaultStyleId,
	IReadOnlyCollection<MapStyleDto> Styles,
	DateTimeOffset CreatedAt,
	DateTimeOffset UpdatedAt,
	PublicationStatus Status);