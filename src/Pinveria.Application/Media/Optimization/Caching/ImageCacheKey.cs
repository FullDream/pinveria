namespace Pinveria.Application.Media.Optimization.Caching;

public sealed record ImageCacheKey(
	Guid AssetId,
	int? Width,
	string Extension)
{
	public string WidthSegment => Width.HasValue ? Width.Value.ToString() : "orig";
}
