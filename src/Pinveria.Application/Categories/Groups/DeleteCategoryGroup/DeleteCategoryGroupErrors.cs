using Pinveria.Domain.Result;

namespace Pinveria.Application.Categories.Groups.DeleteCategoryGroup;

public static class DeleteCategoryGroupErrors
{
	public static readonly Error NotFound = new(
		"CategoryGroup.NotFound", "The category group was not found.", nameof(DeleteCategoryGroup.Id), ErrorType.NotFound);
}
