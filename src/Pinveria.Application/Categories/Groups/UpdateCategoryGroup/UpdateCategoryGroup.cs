using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Categories.Groups.UpdateCategoryGroup;

public sealed record UpdateCategoryGroup(
	Guid GameId,
	Guid MapId, Guid Id, Optional<string?> Name = default, Optional<Guid?> BeforeId = default,
	Optional<Guid?> IconAssetId = default, Optional<string?> Color = default,
	Optional<PublicationStatus?> Status = default)
	: ICommand<Result<CategoryGroupDto>>;
