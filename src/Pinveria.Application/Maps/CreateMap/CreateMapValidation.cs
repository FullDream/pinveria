using Pinveria.Application.Common.Validation;
using FluentValidation;
using Pinveria.Domain.Maps;

namespace Pinveria.Application.Maps.CreateMap;

public sealed class CreateMapValidation : AbstractValidator<CreateMap>
{
	public CreateMapValidation()
	{
		RuleFor(command => command.Slug == null ? null : command.Slug.Trim()).ValidSlug()
			.OverridePropertyName(nameof(CreateMap.Slug));

		RuleFor(command => (command.Name ?? string.Empty).Trim()).NotEmpty().MaximumLength(Map.NameMaxLength)
			.OverridePropertyName(nameof(CreateMap.Name));

		this.OptionalRuleFor(command => command.Status, rule => rule.NotNull().IsInEnum());
	}
}
