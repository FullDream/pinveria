using Pinveria.Application.Media;

namespace Pinveria.Application.Maps;

public sealed record PublicMapStyleDto(Guid Id, string Name, string BackgroundColor,
	MapTileGeometryDto TileGeometry, string TilesUrlTemplate, ImageAssetDto Preview);
