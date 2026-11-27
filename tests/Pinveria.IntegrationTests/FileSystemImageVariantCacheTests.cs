using Pinveria.Application.Media.Optimization.Caching;
using Pinveria.Infrastructure.Media.Optimization;
using Microsoft.Extensions.Options;

namespace Pinveria.IntegrationTests;

public sealed class FileSystemImageVariantCacheTests : IDisposable
{
	private readonly string root = Path.Combine(Path.GetTempPath(), $"image-variant-cache-{Guid.NewGuid():N}");

	[Fact]
	public async Task StoresAndRetrievesVariantsAtomicallyWithDeterministicETagAndThrottledTouch()
	{
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
		var cache = CreateCache(clock, touchIntervalMinutes: 60);
		var key = new ImageCacheKey(Guid.CreateVersion7(), Width: 640, Extension: "webp");
		var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

		Assert.Equal(ImageCacheLookupOutcome.Miss, cache.TryGet(key).Outcome);
		var stored = await cache.StoreAsync(key, payload, CancellationToken.None);

		Assert.Equal(ImageCacheLookupOutcome.Hit, stored.Outcome);
		Assert.Equal(payload.Length, stored.ByteLength);
		Assert.NotNull(stored.FilePath);
		Assert.True(File.Exists(stored.FilePath));
		Assert.Empty(Directory.EnumerateFiles(cache.CacheRoot, "*.tmp", SearchOption.AllDirectories));
		var initialWrite = File.GetLastWriteTimeUtc(stored.FilePath);

		clock.Advance(TimeSpan.FromMinutes(30));
		var hitBeforeInterval = cache.TryGet(key);

		Assert.Equal(ImageCacheLookupOutcome.Hit, hitBeforeInterval.Outcome);
		Assert.Equal(stored.ETag, hitBeforeInterval.ETag);
		Assert.Equal(initialWrite, File.GetLastWriteTimeUtc(stored.FilePath));

		clock.Advance(TimeSpan.FromMinutes(31));
		var hitAfterInterval = cache.TryGet(key);

		Assert.Equal(ImageCacheLookupOutcome.Hit, hitAfterInterval.Outcome);
		Assert.Equal(clock.GetUtcNow().UtcDateTime, File.GetLastWriteTimeUtc(stored.FilePath));
	}

	[Fact]
	public async Task StoresAndTouchesPassthroughSentinelsAndInvalidatesAssetDirectory()
	{
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));
		var cache = CreateCache(clock);
		var assetId = Guid.CreateVersion7();
		var otherId = Guid.CreateVersion7();
		var sentinelKey = new ImageCacheKey(assetId, Width: null, Extension: "avif");
		var variantKey = new ImageCacheKey(assetId, Width: 320, Extension: "webp");
		var otherKey = new ImageCacheKey(otherId, Width: 320, Extension: "webp");

		await cache.StorePassthroughSentinelAsync(sentinelKey, CancellationToken.None);
		await cache.StoreAsync(variantKey, new byte[32], CancellationToken.None);
		await cache.StoreAsync(otherKey, new byte[32], CancellationToken.None);

		Assert.Equal(ImageCacheLookupOutcome.PassthroughSentinel, cache.TryGet(sentinelKey).Outcome);
		Assert.Equal(ImageCacheLookupOutcome.Hit, cache.TryGet(variantKey).Outcome);

		cache.InvalidateAsset(assetId);

		Assert.Equal(ImageCacheLookupOutcome.Miss, cache.TryGet(sentinelKey).Outcome);
		Assert.Equal(ImageCacheLookupOutcome.Miss, cache.TryGet(variantKey).Outcome);
		Assert.Equal(ImageCacheLookupOutcome.Hit, cache.TryGet(otherKey).Outcome);
	}

	[Fact]
	public async Task SweepEvictsIdleEntriesBeyondMaxIdleDaysAndRemovesEmptyAssetFolders()
	{
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
		var cache = CreateCache(clock, maxIdleDays: 30);
		var staleAssetId = Guid.CreateVersion7();
		var activeAssetId = Guid.CreateVersion7();
		var staleKey = new ImageCacheKey(staleAssetId, Width: 640, Extension: "avif");
		var activeKey = new ImageCacheKey(activeAssetId, Width: 640, Extension: "avif");

		await cache.StoreAsync(staleKey, new byte[128], CancellationToken.None);
		await cache.StoreAsync(activeKey, new byte[128], CancellationToken.None);

		clock.Advance(TimeSpan.FromDays(20));
		Assert.Equal(ImageCacheLookupOutcome.Hit, cache.TryGet(activeKey).Outcome);

		clock.Advance(TimeSpan.FromDays(15));
		await cache.SweepAsync(CancellationToken.None);

		Assert.Equal(ImageCacheLookupOutcome.Miss, cache.TryGet(staleKey).Outcome);
		Assert.False(Directory.Exists(Path.Combine(cache.CacheRoot, staleAssetId.ToString("N"))));
		Assert.Equal(ImageCacheLookupOutcome.Hit, cache.TryGet(activeKey).Outcome);
	}

	[Fact]
	public async Task SweepEvictsOldestEntriesDownTo85PercentWhenQuotaIsExceeded()
	{
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero));
		var cache = CreateCache(clock, maxBytes: 1000, evictFraction: 0.15, touchIntervalMinutes: 1);
		var keys = new List<ImageCacheKey>();

		for (var index = 0; index < 11; index++)
		{
			var key = new ImageCacheKey(Guid.CreateVersion7(), Width: 320, Extension: "webp");

			keys.Add(key);
			await cache.StoreAsync(key, new byte[100], CancellationToken.None);
			clock.Advance(TimeSpan.FromMinutes(5));
		}

		// Touch the oldest entry (index 0) so it becomes the freshest and survives LRU eviction.
		Assert.Equal(ImageCacheLookupOutcome.Hit, cache.TryGet(keys[0]).Outcome);
		await cache.SweepAsync(CancellationToken.None);

		// Total was 1100 bytes (> 1000). Target is <= 850 bytes (85%), so 3 oldest untouched entries (keys[1..3]) are evicted.
		Assert.Equal(ImageCacheLookupOutcome.Hit, cache.TryGet(keys[0]).Outcome);
		Assert.Equal(ImageCacheLookupOutcome.Miss, cache.TryGet(keys[1]).Outcome);
		Assert.Equal(ImageCacheLookupOutcome.Miss, cache.TryGet(keys[2]).Outcome);
		Assert.Equal(ImageCacheLookupOutcome.Miss, cache.TryGet(keys[3]).Outcome);

		for (var index = 4; index < 11; index++)
			Assert.Equal(ImageCacheLookupOutcome.Hit, cache.TryGet(keys[index]).Outcome);
	}

	[Fact]
	public async Task StartupCleanRemovesOnlyOrphanedTempFilesInsideCacheRoot()
	{
		var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 27, 0, 0, 0, TimeSpan.Zero));
		var cache = CreateCache(clock);
		var validKey = new ImageCacheKey(Guid.CreateVersion7(), Width: 320, Extension: "webp");
		var stored = await cache.StoreAsync(validKey, new byte[64], CancellationToken.None);
		var orphanTmp = $"{stored.FilePath}.orphan.tmp";

		await File.WriteAllBytesAsync(orphanTmp, [9, 9, 9]);
		cache.CleanOrphanedTempFiles();

		Assert.False(File.Exists(orphanTmp));
		Assert.True(File.Exists(stored.FilePath));
	}

	private FileSystemImageVariantCache CreateCache(
		TimeProvider timeProvider,
		long maxBytes = 2L * 1024 * 1024 * 1024,
		double evictFraction = 0.15,
		int maxIdleDays = 30,
		int touchIntervalMinutes = 60) =>
		new(
			Options.Create(new ImageCacheOptions
			{
				CacheRoot = root,
				MaxBytes = maxBytes,
				EvictFraction = evictFraction,
				MaxIdleDays = maxIdleDays,
				TouchIntervalMinutes = touchIntervalMinutes,
				SweepIntervalMinutes = 30
			}),
			timeProvider: timeProvider);

	public void Dispose()
	{
		if (Directory.Exists(root))
			Directory.Delete(root, recursive: true);
	}

	private sealed class FakeTimeProvider(DateTimeOffset initialUtc) : TimeProvider
	{
		private DateTimeOffset currentUtc = initialUtc;

		public override DateTimeOffset GetUtcNow() => currentUtc;

		public void Advance(TimeSpan delta) => currentUtc = currentUtc.Add(delta);
	}
}
