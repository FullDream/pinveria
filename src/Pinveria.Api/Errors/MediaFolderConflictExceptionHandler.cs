using Pinveria.Api.Results;
using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Application.Media;
using Microsoft.AspNetCore.Diagnostics;

namespace Pinveria.Api.Errors;

public sealed class MediaFolderConflictExceptionHandler(IEnumerable<IPersistenceFailureClassifier> classifiers)
	: IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
		CancellationToken cancellationToken)
	{
		if (!classifiers.Any(classifier => classifier.IsMediaFolderNameConflict(exception)))
			return false;

		await new OutcomeResult(MediaOperationErrors.FolderNameConflict).ExecuteAsync(context);

		return true;
	}
}
