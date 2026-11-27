namespace Pinveria.Application.Media.Optimization.Caching;

public sealed record ImageCacheLookup(
	ImageCacheLookupOutcome Outcome,
	string? FilePath = null,
	long ByteLength = 0,
	string? ETag = null)
{
	public static ImageCacheLookup Miss() => new(ImageCacheLookupOutcome.Miss);

	public static ImageCacheLookup Hit(string filePath, long byteLength, string eTag) =>
		new(ImageCacheLookupOutcome.Hit, filePath, byteLength, eTag);

	public static ImageCacheLookup PassthroughSentinel() =>
		new(ImageCacheLookupOutcome.PassthroughSentinel);
}
