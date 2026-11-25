using Pinveria.Application.Markers;
using Pinveria.Application.Markers.CreateMarker;
using Pinveria.Application.Markers.UpdateMarker;
using Pinveria.Domain;

namespace Pinveria.Api.Markers;

internal static class MarkerMappingExtensions
{
	internal static CreateMarker ToCommand(this CreateMarkerRequest request, Guid gameId, Guid mapId) =>
		new(gameId, mapId, request.CategoryId, request.Name, request.Position?.ToInput(), request.Description,
			request.Media, request.Status);

	internal static UpdateMarker ToCommand(this UpdateMarkerRequest request, Guid gameId, Guid mapId, Guid id) =>
		new(gameId, mapId, id, request.CategoryId, request.Name, request.Description,
			request.Position.IsSpecified
				? Optional<PositionPatch?>.From(request.Position.Value?.ToInput())
				: default,
			request.Media, request.Status);

	private static PositionInput ToInput(this PositionRequest request) => new(request.X, request.Y);

	private static PositionPatch ToInput(this PositionPatchRequest request) => new(request.X, request.Y);

}
