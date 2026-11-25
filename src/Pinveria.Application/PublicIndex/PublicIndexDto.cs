namespace Pinveria.Application.PublicIndex;

public sealed record PublicIndexDto(IReadOnlyCollection<PublicIndexGameDto> Games);

public sealed record PublicIndexGameDto(string Slug, DateTimeOffset UpdatedAt,
	IReadOnlyCollection<PublicIndexMapDto> Maps);

public sealed record PublicIndexMapDto(string Slug, DateTimeOffset UpdatedAt);