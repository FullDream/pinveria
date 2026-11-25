using FluentValidation;
using Pinveria.Application.Common.Validation;

namespace Pinveria.Application.Markers;

public sealed class PositionPatchValidation : AbstractValidator<PositionPatch>
{
	public PositionPatchValidation()
	{
		this.OptionalRuleFor(patch => patch.X,
			rule => rule.NotNull().Must(value => value is null || double.IsFinite(value.Value)));

		this.OptionalRuleFor(patch => patch.Y,
			rule => rule.NotNull().Must(value => value is null || double.IsFinite(value.Value)));
	}
}
