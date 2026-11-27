namespace Pinveria.Application.Media.Optimization.Caching;

public interface IImageVariantCache
{
	string CacheRoot { get; }

	ImageCacheLookup TryGet(ImageCacheKey key);
	Task<ImageCacheLookup> StoreAsync(ImageCacheKey key, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
	Task StorePassthroughSentinelAsync(ImageCacheKey key, CancellationToken cancellationToken);
	void InvalidateAsset(Guid assetId);
	void CleanOrphanedTempFiles();
	Task SweepAsync(CancellationToken cancellationToken);
}
