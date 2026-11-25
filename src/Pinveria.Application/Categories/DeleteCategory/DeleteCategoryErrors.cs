using Pinveria.Domain.Result;

namespace Pinveria.Application.Categories.DeleteCategory;

public static class DeleteCategoryErrors
{
	public static readonly Error NotFound = new(
		"Category.NotFound", "The category was not found.", nameof(DeleteCategory.Id), ErrorType.NotFound);
}
