using Pinveria.Application.Maps.UpdateMap;
using Pinveria.Application.Maps.CreateMap;
using Pinveria.Application.Maps.UpdateMapStyle;

namespace Pinveria.Api.Maps;

internal static class MapMappingExtensions
{
	internal static Application.Maps.AddMapStyle.AddMapStyle ToCommand(this AddMapStyleRequest request,
		Guid gameId, Guid mapId) => new(gameId, mapId, request.Name, request.BackgroundColor, request.SourceAssetId);

	internal static UpdateMap ToCommand(this UpdateMapRequest request, Guid gameId, Guid mapId) =>
		new(gameId, mapId, request.Name, request.DefaultStyleId, request.StyleOrder, request.Slug, request.Status,
			request.Description);

	internal static CreateMap ToCommand(this CreateMapRequest request, Guid gameId) =>
		new(gameId, request.Name, request.Slug, request.Status, request.Description);

	internal static UpdateMapStyle ToCommand(this UpdateMapStyleRequest request, Guid gameId, Guid mapId,
		Guid styleId) =>
		new(gameId, mapId, styleId, request.Name, request.BackgroundColor);
}