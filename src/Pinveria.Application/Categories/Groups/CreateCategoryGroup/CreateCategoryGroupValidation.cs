using FluentValidation;
using Pinveria.Domain.Categories;
using Pinveria.Application.Common.Validation;

namespace Pinveria.Application.Categories.Groups.CreateCategoryGroup;

public sealed class CreateCategoryGroupValidation : AbstractValidator<CreateCategoryGroup>
{
	public CreateCategoryGroupValidation()
	{
		RuleFor(command => command.Name == null ? null : command.Name.Trim())
			.NotEmpty().MaximumLength(CategoryGroupLimits.NameMaxLength)
			.OverridePropertyName(nameof(CreateCategoryGroup.Name));

		RuleFor(command => command.Color).NotEmpty().Length(4, 7).Matches(Category.ColorPattern);

		this.OptionalRuleFor(command => command.Status, rule => rule.NotNull().IsInEnum());
	}
}
