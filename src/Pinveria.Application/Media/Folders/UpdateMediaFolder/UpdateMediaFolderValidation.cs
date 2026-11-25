using FluentValidation;
using Pinveria.Application.Common.Validation;
using Pinveria.Domain;
using Pinveria.Domain.Media;

namespace Pinveria.Application.Media.Folders.UpdateMediaFolder;

public sealed class UpdateMediaFolderValidation : AbstractValidator<UpdateMediaFolder>
{
	public UpdateMediaFolderValidation()
	{
		RuleFor(command => command).Must(command => Optional.AnySpecified(command.Name, command.ParentId))
			.WithMessage(MediaOperationErrors.EmptyPatch.Message).OverridePropertyName(string.Empty);

		this.OptionalRuleFor(command => command.Name,
			rule => rule.NotEmpty().MaximumLength(MediaFolder.NameMaxLength), value => value == null ? null : value.Trim());
	}
}
