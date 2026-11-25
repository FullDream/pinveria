using Pinveria.Api.Results;
using Pinveria.Application.PublicIndex;
using Pinveria.Application.PublicIndex.GetPublicIndex;
using Mediator;

namespace Pinveria.Api.PublicIndex;

public static class Endpoints
{
	public static RouteGroupBuilder MapPublicIndex(this RouteGroupBuilder group)
	{
		var index = group.MapGroup("/public/index").WithTags("Public Index").AllowAnonymous();

		index.MapGet("", GetAsync).WithName("GetPublicIndex")
			.WithSummary("Get published games and maps index with last-modified timestamps");

		return index;
	}

	private static async Task<OutcomeResult<PublicIndexDto>> GetAsync(
		IMediator mediator, CancellationToken cancellationToken) =>
		await mediator.Send(new GetPublicIndex(), cancellationToken);
}