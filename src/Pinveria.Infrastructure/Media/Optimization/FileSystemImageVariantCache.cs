using System.Threading.Channels;
using Pinveria.Application.Media.Optimization.Caching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Pinveria.Infrastructure.Media.Optimization;

public sealed class FileSystemImageVariantCache : IImageVariantCache
{
	private const string SentinelSuffix = ".pass";
	private const string TempSuffix = ".tmp";

	private readonly string root;
	private readonly ImageCacheOptions options;
	private readonly TimeProvider timeProvider;
	private readonly SemaphoreSlim sweepGate = new(1, 1);
	private readonly Channel<bool> thresholdChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
	{
		FullMode = BoundedChannelFullMode.DropWrite,
		SingleReader = true,
		SingleWriter = false
	});

	private long trackedBytes;

	public FileSystemImageVariantCache(
		IOptions<ImageCacheOptions> options,
		IHostEnvironment? environment = null,
		TimeProvider? timeProvider = null)
	{
		this.options = options.Value;
		this.timeProvider = timeProvider ?? TimeProvider.System;
		root = Path.GetFullPath(this.options.CacheRoot, environment?.ContentRootPath ?? AppContext.BaseDirectory);

		RejectLinks(root);
		Directory.CreateDirectory(root);
	}

	public string CacheRoot => root;

	internal ChannelReader<bool> ThresholdSignals => thresholdChannel.Reader;

	public ImageCacheLookup TryGet(ImageCacheKey key)
	{
		var targetPath = ResolveVariantPath(key);
		var sentinelPath = targetPath + SentinelSuffix;

		if (File.Exists(sentinelPath))
		{
			TouchIfStale(sentinelPath);

			return ImageCacheLookup.PassthroughSentinel();
		}

		var info = new FileInfo(targetPath);

		if (!info.Exists)
			return ImageCacheLookup.Miss();

		TouchIfStale(targetPath, info.LastWriteTimeUtc);

		return ImageCacheLookup.Hit(targetPath, info.Length, BuildETag(key, info.Length));
	}

	public async Task<ImageCacheLookup> StoreAsync(ImageCacheKey key, ReadOnlyMemory<byte> bytes,
		CancellationToken cancellationToken)
	{
		var targetPath = ResolveVariantPath(key);
		var directory = Path.GetDirectoryName(targetPath)!;

		Directory.CreateDirectory(directory);
		RejectLinks(directory);
		var tempPath = $"{targetPath}.{Guid.NewGuid():N}{TempSuffix}";

		try
		{
			await using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
				FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
			{
				await destination.WriteAsync(bytes, cancellationToken);
				await destination.FlushAsync(cancellationToken);
				destination.Flush(flushToDisk: true);
			}

			var now = timeProvider.GetUtcNow().UtcDateTime;

			File.SetLastWriteTimeUtc(tempPath, now);
			File.Move(tempPath, targetPath, overwrite: true);
		}
		catch
		{
			DeleteFileQuietly(tempPath);
			throw;
		}

		var total = Interlocked.Add(ref trackedBytes, bytes.Length);

		if (total > options.MaxBytes)
			thresholdChannel.Writer.TryWrite(true);

		return ImageCacheLookup.Hit(targetPath, bytes.Length, BuildETag(key, bytes.Length));
	}

	public async Task StorePassthroughSentinelAsync(ImageCacheKey key, CancellationToken cancellationToken)
	{
		var sentinelPath = ResolveVariantPath(key) + SentinelSuffix;
		var directory = Path.GetDirectoryName(sentinelPath)!;

		Directory.CreateDirectory(directory);
		RejectLinks(directory);
		var tempPath = $"{sentinelPath}.{Guid.NewGuid():N}{TempSuffix}";

		try
		{
			await File.WriteAllBytesAsync(tempPath, [], cancellationToken);
			File.SetLastWriteTimeUtc(tempPath, timeProvider.GetUtcNow().UtcDateTime);
			File.Move(tempPath, sentinelPath, overwrite: true);
		}
		catch
		{
			DeleteFileQuietly(tempPath);
			throw;
		}
	}

	public void InvalidateAsset(Guid assetId)
	{
		if (assetId == Guid.Empty)
			return;

		var assetDirectory = Path.Combine(root, assetId.ToString("N"));

		RejectLinks(assetDirectory);

		if (!Directory.Exists(assetDirectory))
			return;

		try
		{
			Directory.Delete(assetDirectory, recursive: true);
		}
		catch (DirectoryNotFoundException)
		{
			// Directory was already removed by a concurrent invalidation or sweep.
		}
	}

	public void CleanOrphanedTempFiles()
	{
		if (!Directory.Exists(root))
			return;

		RejectLinks(root);

		foreach (var file in Directory.EnumerateFiles(root, $"*{TempSuffix}", SearchOption.AllDirectories))
			DeleteFileQuietly(file);
	}

	public async Task SweepAsync(CancellationToken cancellationToken)
	{
		await sweepGate.WaitAsync(cancellationToken);

		try
		{
			await Task.Run(() => SweepCore(cancellationToken), cancellationToken);
		}
		finally
		{
			sweepGate.Release();
		}
	}

	private void SweepCore(CancellationToken cancellationToken)
	{
		if (!Directory.Exists(root))
		{
			Interlocked.Exchange(ref trackedBytes, 0);

			return;
		}

		RejectLinks(root);
		var now = timeProvider.GetUtcNow().UtcDateTime;
		var idleCutoff = now - TimeSpan.FromDays(Math.Max(1, options.MaxIdleDays));
		var survivors = new List<CachedEntry>();
		long remainingBytes = 0;

		foreach (var directory in Directory.EnumerateDirectories(root))
		{
			cancellationToken.ThrowIfCancellationRequested();
			RejectLinks(directory);

			foreach (var filePath in Directory.EnumerateFiles(directory))
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (filePath.EndsWith(TempSuffix, StringComparison.OrdinalIgnoreCase))
					continue;

				var info = new FileInfo(filePath);

				if (!info.Exists)
					continue;

				if (info.LastWriteTimeUtc < idleCutoff)
				{
					DeleteFileQuietly(filePath);
					continue;
				}

				survivors.Add(new CachedEntry(filePath, info.Length, info.LastWriteTimeUtc));
				remainingBytes += info.Length;
			}
		}

		if (remainingBytes > options.MaxBytes)
		{
			var targetBytes = (long)Math.Floor(options.MaxBytes * (1.0 - Math.Clamp(options.EvictFraction, 0.01, 0.95)));

			survivors.Sort(static (left, right) =>
			{
				var comparison = left.LastWriteTimeUtc.CompareTo(right.LastWriteTimeUtc);

				return comparison != 0
					? comparison
					: string.Compare(left.Path, right.Path, StringComparison.Ordinal);
			});

			foreach (var entry in survivors)
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (remainingBytes <= targetBytes)
					break;

				if (DeleteFileQuietly(entry.Path))
					remainingBytes = Math.Max(0, remainingBytes - entry.Length);
			}
		}

		foreach (var directory in Directory.EnumerateDirectories(root))
		{
			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				if (!Directory.EnumerateFileSystemEntries(directory).Any())
					Directory.Delete(directory, recursive: false);
			}
			catch (IOException)
			{
				// Ignore transient race when a concurrent request writes into the directory.
			}
		}

		Interlocked.Exchange(ref trackedBytes, remainingBytes);
	}

	private void TouchIfStale(string path, DateTime? knownLastWriteUtc = null)
	{
		try
		{
			var now = timeProvider.GetUtcNow().UtcDateTime;
			var lastWrite = knownLastWriteUtc ?? File.GetLastWriteTimeUtc(path);
			var interval = TimeSpan.FromMinutes(Math.Max(1, options.TouchIntervalMinutes));

			if (now - lastWrite >= interval)
				File.SetLastWriteTimeUtc(path, now);
		}
		catch (IOException)
		{
			// Concurrent readers or sweeps can safely race with a timestamp touch.
		}
	}

	private string ResolveVariantPath(ImageCacheKey key)
	{
		if (key.AssetId == Guid.Empty)
			throw new ArgumentException("A generated nonempty asset identifier is required.", nameof(key));

		var fileName = $"{key.WidthSegment}.{key.Extension}";
		var path = Path.Combine(root, key.AssetId.ToString("N"), fileName);

		RejectLinks(path);

		return path;
	}

	private static string BuildETag(ImageCacheKey key, long byteLength) =>
		$"\"{key.AssetId:N}-{key.WidthSegment}-{key.Extension}-{byteLength:x}\"";

	private static bool DeleteFileQuietly(string path)
	{
		try
		{
			if (!File.Exists(path))
				return false;

			File.Delete(path);

			return true;
		}
		catch (IOException)
		{
			return false;
		}
	}

	internal static void RejectLinks(string path)
	{
		for (var entry = new FileInfo(path) as FileSystemInfo; entry is not null;
			entry = entry is DirectoryInfo directory ? directory.Parent : ((FileInfo)entry).Directory)
		{
			if (entry.LinkTarget is not null || entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
				throw new IOException("Image variant cache cannot contain symbolic links or reparse points.");
		}
	}

	private readonly record struct CachedEntry(string Path, long Length, DateTime LastWriteTimeUtc);
}
