using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Maps.GetPublicMap;

public sealed record GetPublicMap(string GameSlug, string MapSlug,
	ContentVisibility Visibility = ContentVisibility.PublishedOnly) : IQuery<Result<PublicMapDetailsDto>>;
