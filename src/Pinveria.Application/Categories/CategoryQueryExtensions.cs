using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Application.Categories.Groups;
using Pinveria.Domain.Categories;

namespace Pinveria.Application.Categories;

internal static class CategoryQueryExtensions
{
	internal static IQueryable<Category> ForMap(this IQueryable<Category> categories, IApplicationDbContext context,
		Guid gameId, Guid mapId)
	{
		var groups = context.CategoryGroups.ForMap(context, gameId, mapId);

		return categories.Where(category => groups.Any(group => group.Id == category.CategoryGroupId));
	}
}
