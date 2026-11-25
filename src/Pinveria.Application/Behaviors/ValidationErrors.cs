using FluentValidation.Results;
using Pinveria.Domain.Result;

namespace Pinveria.Application.Behaviors;

internal static class ValidationErrors
{
	internal static Error FromFailure(ValidationFailure failure) =>
		new(failure.ErrorCode, failure.ErrorMessage, failure.PropertyName, ErrorType.Validation);
}
