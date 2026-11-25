using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Categories.Groups.DeleteCategoryGroup;

public sealed record DeleteCategoryGroup(Guid GameId, Guid MapId, Guid Id) : ICommand<Result>;
