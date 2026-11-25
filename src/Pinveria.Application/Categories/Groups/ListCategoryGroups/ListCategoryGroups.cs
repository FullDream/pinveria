using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Categories.Groups.ListCategoryGroups;

public sealed record ListCategoryGroups(Guid GameId,
	Guid MapId) : IQuery<Result<IReadOnlyCollection<CategoryGroupDto>>>;
