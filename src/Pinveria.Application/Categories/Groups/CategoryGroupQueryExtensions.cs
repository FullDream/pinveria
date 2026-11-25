using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Application.Maps;
using Pinveria.Domain.Categories;

namespace Pinveria.Application.Categories.Groups;

internal static class CategoryGroupQueryExtensions
{
	internal static IQueryable<CategoryGroup> ForMap(this IQueryable<CategoryGroup> groups,
		IApplicationDbContext context, Guid gameId, Guid mapId)
	{
		var maps = context.Maps.ForMap(gameId, mapId);

		return groups.Where(group => group.MapId == mapId && maps.Any(map => map.Id == group.MapId));
	}
}
