using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.PublicIndex.GetPublicIndex;

public sealed record GetPublicIndex : IQuery<Result<PublicIndexDto>>;