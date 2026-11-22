using GameAtlas.Domain.Result;
using Mediator;

namespace GameAtlas.Application.PublicIndex.GetPublicIndex;

public sealed record GetPublicIndex : IQuery<Result<PublicIndexDto>>;