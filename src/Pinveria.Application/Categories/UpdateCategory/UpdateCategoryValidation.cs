using FluentValidation;
using Pinveria.Application.Common.Validation;
using Pinveria.Domain;
using Pinveria.Domain.Categories;

namespace Pinveria.Application.Categories.UpdateCategory;

public sealed class UpdateCategoryValidation : AbstractValidator<UpdateCategory>
{
	public UpdateCategoryValidation()
	{
		RuleFor(command => command).Must(command => Optional.AnySpecified(
				command.Name, command.IconAssetId, command.Color, command.BeforeId, command.CategoryGroupId, command.Status))
			.WithErrorCode(UpdateCategoryErrors.EmptyPatch.Code)
			.WithMessage(UpdateCategoryErrors.EmptyPatch.Message).OverridePropertyName(string.Empty);

		this.OptionalRuleFor(command => command.Name,
			rule => rule.NotEmpty().MaximumLength(CategoryLimits.NameMaxLength),
			value => value == null ? null : value.Trim());

		this.OptionalRuleFor(command => command.Color,
			rule => rule.Length(4, 7).Matches(Category.ColorPattern));

		this.OptionalRuleFor(command => command.CategoryGroupId, rule => rule.NotNull());

		this.OptionalRuleFor(command => command.Status, rule => rule.NotNull().IsInEnum());
	}
}
