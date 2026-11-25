using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Categories.DeleteCategory;

public sealed record DeleteCategory(Guid GameId, Guid MapId, Guid GroupId, Guid Id) : ICommand<Result>;
