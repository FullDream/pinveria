using Pinveria.Application.Categories.Groups;
using Pinveria.Application.Markers;

namespace Pinveria.Application.Maps;

public sealed record PublicMapDetailsDto(Guid Id, Guid GameId, string Name, string Slug, string? Description,
	Guid? DefaultStyleId, IReadOnlyCollection<PublicMapStyleDto> Styles,
	IReadOnlyCollection<PublicCategoryGroupDto> CategoryGroups, IReadOnlyCollection<PublicMarkerDto> Markers);