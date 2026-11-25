using FluentValidation;
using Pinveria.Application.Common.Validation;
using Pinveria.Domain.Categories;

namespace Pinveria.Application.Categories.CreateCategory;

public sealed class CreateCategoryValidation : AbstractValidator<CreateCategory>
{
	public CreateCategoryValidation()
	{
		RuleFor(command => command.Name == null ? null : command.Name.Trim())
			.NotEmpty().MaximumLength(CategoryLimits.NameMaxLength).OverridePropertyName(nameof(CreateCategory.Name));

		RuleFor(command => command.Color).Length(4, 7).Matches(Category.ColorPattern);

		this.OptionalRuleFor(command => command.Status, rule => rule.NotNull().IsInEnum());
	}
}
