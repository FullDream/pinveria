using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Application.Maps;
using Pinveria.Domain.Maps;
using Microsoft.AspNetCore.Diagnostics;

namespace Pinveria.Api.Errors;

public sealed class MapConflictExceptionHandler(IEnumerable<IPersistenceFailureClassifier> classifiers)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
		CancellationToken cancellationToken)
	{
		var slugConflict = classifiers.Any(classifier => classifier.IsMapSlugConflict(exception));
		var mapConflict = classifiers.Any(classifier => classifier.IsMapNameConflict(exception));
		var nameConflict = classifiers.Any(classifier => classifier.IsMapStyleNameConflict(exception));

		if (!slugConflict && !mapConflict && !nameConflict)
			return false;

		var error = slugConflict ? MapOperationErrors.SlugConflict : mapConflict ? MapOperationErrors.NameConflict : MapErrors.NameConflict;

		await TypedResults.Problem(statusCode: 409, title: "Conflict", detail: error.Message).ExecuteAsync(context);

		return true;
	}
}
