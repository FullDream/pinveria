using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Categories.UpdateCategory;

public sealed record UpdateCategory(
	Guid GameId,
	Guid MapId, Guid GroupId, Guid Id, Optional<string?> Name = default, Optional<Guid?> IconAssetId = default,
	Optional<string?> Color = default, Optional<Guid?> BeforeId = default, Optional<Guid?> CategoryGroupId = default,
	Optional<PublicationStatus?> Status = default)
	: ICommand<Result<CategoryDto>>;
