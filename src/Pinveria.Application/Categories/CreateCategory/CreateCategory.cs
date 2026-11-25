using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Categories.CreateCategory;

public sealed record CreateCategory(
	Guid GameId,
	Guid MapId, Guid GroupId, string? Name, Guid? IconAssetId, string? Color, Optional<Guid?> BeforeId = default,
	Optional<PublicationStatus?> Status = default)
	: ICommand<Result<CategoryDto>>;
