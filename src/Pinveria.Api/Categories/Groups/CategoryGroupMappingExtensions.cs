using Pinveria.Application.Categories.Groups.CreateCategoryGroup;
using Pinveria.Application.Categories.Groups.UpdateCategoryGroup;

namespace Pinveria.Api.Categories.Groups;

internal static class CategoryGroupMappingExtensions
{
	internal static CreateCategoryGroup ToCommand(this CreateCategoryGroupRequest request, Guid gameId, Guid mapId) =>
		new(gameId, mapId, request.Name, request.BeforeId, request.IconAssetId, request.Color, request.Status);

	internal static UpdateCategoryGroup ToCommand(this UpdateCategoryGroupRequest request, Guid gameId,
		Guid mapId, Guid id) =>
		new(gameId, mapId, id, request.Name, request.BeforeId, request.IconAssetId, request.Color, request.Status);
}
