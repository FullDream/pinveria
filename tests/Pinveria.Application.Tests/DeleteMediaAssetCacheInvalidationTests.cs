using System.Collections;
using System.Linq.Expressions;
using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Application.Media;
using Pinveria.Application.Media.Assets.DeleteMediaAsset;
using Pinveria.Application.Media.Optimization.Caching;
using Pinveria.Application.Media.Storage;
using Pinveria.Domain.Categories;
using Pinveria.Domain.Games;
using Pinveria.Domain.Maps;
using Pinveria.Domain.Markers;
using Pinveria.Domain.Media;
using Pinveria.Domain.Result;
using Pinveria.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace Pinveria.Application.Tests;

public class DeleteMediaAssetCacheInvalidationTests
{
	[Fact]
	public async Task InvalidatesCacheOnSuccessfulDeletionAndToleratesCacheThrow()
	{
		var assetId = Guid.CreateVersion7();
		var image = ImageAsset.Create(assetId, "Cover", null, "image/png", 1024, 100, 100, "", null).Value!;
		var storage = new RecordingMediaStorage();
		var cache = new RecordingImageVariantCache { ThrowOnInvalidate = true };
		var context = new FakeApplicationDbContext(mediaAssets: [image]);
		var handler = new DeleteMediaAssetHandler(context, storage, cache);
		var result = await handler.Handle(new DeleteMediaAsset(assetId), CancellationToken.None);

		Assert.True(result.IsSuccess);
		Assert.Equal([assetId], storage.DeletedAssets);
		Assert.Equal([assetId], cache.InvalidatedAssets);
		Assert.True(context.TransactionCommitted);
	}

	[Fact]
	public async Task DoesNotInvalidateCacheWhenAssetIsMissing()
	{
		var assetId = Guid.CreateVersion7();
		var storage = new RecordingMediaStorage();
		var cache = new RecordingImageVariantCache();
		var context = new FakeApplicationDbContext();
		var handler = new DeleteMediaAssetHandler(context, storage, cache);
		var result = await handler.Handle(new DeleteMediaAsset(assetId), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(MediaOperationErrors.AssetNotFound, Assert.Single(result.Errors));
		Assert.Empty(storage.DeletedAssets);
		Assert.Empty(cache.InvalidatedAssets);
	}

	[Fact]
	public async Task DoesNotInvalidateCacheWhenAssetIsInUse()
	{
		var assetId = Guid.CreateVersion7();
		var image = ImageAsset.Create(assetId, "Cover", null, "image/png", 1024, 100, 100, "", null).Value!;
		var game = Game.Create("Game", Slug.Create("game").Value!, assetId).Value!;
		var storage = new RecordingMediaStorage();
		var cache = new RecordingImageVariantCache();
		var context = new FakeApplicationDbContext(games: [game], mediaAssets: [image]);
		var handler = new DeleteMediaAssetHandler(context, storage, cache);
		var result = await handler.Handle(new DeleteMediaAsset(assetId), CancellationToken.None);

		Assert.True(result.IsFailure);
		Assert.Equal(MediaOperationErrors.AssetInUse, Assert.Single(result.Errors));
		Assert.Empty(storage.DeletedAssets);
		Assert.Empty(cache.InvalidatedAssets);
	}

	private sealed class RecordingMediaStorage : IMediaStorage
	{
		public List<Guid> DeletedAssets { get; } = [];
		public string PublicAssetsRoot => "assets";

		public string AssetPath(Guid assetId) => assetId.ToString("N");

		public string StagingPath(Guid uploadId) => uploadId.ToString("N");

		public Task<Result<long>> WriteAsync(Guid uploadId, Stream source, long maximumBytes,
			CancellationToken cancellationToken) =>
			Task.FromResult<Result<long>>(0L);

		public void Promote(Guid uploadId, Guid assetId)
		{
		}

		public void DeleteStaging(Guid uploadId)
		{
		}

		public void DeleteAsset(Guid assetId) => DeletedAssets.Add(assetId);
	}

	private sealed class RecordingImageVariantCache : IImageVariantCache
	{
		public List<Guid> InvalidatedAssets { get; } = [];
		public bool ThrowOnInvalidate { get; init; }
		public string CacheRoot => "cache";

		public ImageCacheLookup TryGet(ImageCacheKey key) => ImageCacheLookup.Miss();

		public Task<ImageCacheLookup> StoreAsync(ImageCacheKey key, ReadOnlyMemory<byte> bytes,
			CancellationToken cancellationToken) =>
			Task.FromResult(ImageCacheLookup.Miss());

		public Task StorePassthroughSentinelAsync(ImageCacheKey key, CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public void InvalidateAsset(Guid assetId)
		{
			InvalidatedAssets.Add(assetId);

			if (ThrowOnInvalidate)
				throw new IOException("Simulated cache directory removal failure.");
		}

		public void CleanOrphanedTempFiles()
		{
		}

		public Task SweepAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class FakeApplicationDbContext(
		IReadOnlyList<Game>? games = null,
		IReadOnlyList<MediaAsset>? mediaAssets = null) : IApplicationDbContext
	{
		public bool TransactionCommitted { get; private set; }
		public DbSet<Game> Games { get; } = new FakeDbSet<Game>(games ?? []);
		public DbSet<CategoryGroup> CategoryGroups { get; } = new FakeDbSet<CategoryGroup>([]);
		public DbSet<Category> Categories { get; } = new FakeDbSet<Category>([]);
		public DbSet<Marker> Markers { get; } = new FakeDbSet<Marker>([]);
		public DbSet<Map> Maps { get; } = new FakeDbSet<Map>([]);
		public DbSet<MapStyle> MapStyles { get; } = new FakeDbSet<MapStyle>([]);
		public DbSet<MediaFolder> MediaFolders { get; } = new FakeDbSet<MediaFolder>([]);
		public DbSet<MediaAsset> MediaAssets { get; } = new FakeDbSet<MediaAsset>(mediaAssets ?? []);

		public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
			Task.FromResult<IApplicationTransaction>(new FakeTransaction(() => TransactionCommitted = true));

		public Task LockMapAsync(Guid mapId, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task LockMarkerAsync(Guid markerId, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task LockMediaFoldersAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task LockMediaAssetAsync(Guid assetId, bool exclusive = false,
			CancellationToken cancellationToken = default) =>
			Task.CompletedTask;

		public Task LockMediaAssetsAsync(IReadOnlyCollection<Guid> assetIds,
			CancellationToken cancellationToken = default) =>
			Task.CompletedTask;

		public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
	}

	private sealed class FakeTransaction(Action onCommit) : IApplicationTransaction
	{
		public Task CommitAsync(CancellationToken cancellationToken = default)
		{
			onCommit();

			return Task.CompletedTask;
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class FakeDbSet<TEntity>(IReadOnlyList<TEntity> items) : DbSet<TEntity>, IQueryable<TEntity>,
		IAsyncEnumerable<TEntity>
		where TEntity : class
	{
		private readonly IQueryable<TEntity> queryable = items.AsQueryable();

		public override IEntityType EntityType => throw new NotSupportedException();

		Type IQueryable.ElementType => queryable.ElementType;

		Expression IQueryable.Expression => queryable.Expression;

		IQueryProvider IQueryable.Provider => new FakeAsyncQueryProvider<TEntity>(queryable.Provider);

		IEnumerator<TEntity> IEnumerable<TEntity>.GetEnumerator() => queryable.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => queryable.GetEnumerator();

		IAsyncEnumerator<TEntity> IAsyncEnumerable<TEntity>.GetAsyncEnumerator(
			CancellationToken cancellationToken) =>
			new FakeAsyncEnumerator<TEntity>(queryable.GetEnumerator());
	}

	private sealed class FakeAsyncQueryProvider<TEntity>(IQueryProvider inner) : IAsyncQueryProvider
	{
		public IQueryable CreateQuery(Expression expression) =>
			new FakeAsyncQueryable<TEntity>(inner.CreateQuery<TEntity>(expression));

		public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
			new FakeAsyncQueryable<TElement>(inner.CreateQuery<TElement>(expression));

		public object? Execute(Expression expression) => inner.Execute(expression);

		public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

		public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
		{
			if (expression is MethodCallExpression { Method.Name: "ExecuteDelete" })
				return (TResult)(object)Task.FromResult(1);

			var resultType = typeof(TResult).GetGenericArguments()[0];
			var executeMethod = typeof(IQueryProvider)
				.GetMethods()
				.Single(method => method is { Name: nameof(IQueryProvider.Execute), IsGenericMethod: true })
				.MakeGenericMethod(resultType);
			var value = executeMethod.Invoke(inner, [expression]);

			return (TResult)typeof(Task)
				.GetMethod(nameof(Task.FromResult))!
				.MakeGenericMethod(resultType)
				.Invoke(null, [value])!;
		}
	}

	private sealed class FakeAsyncQueryable<TElement>(IQueryable<TElement> inner) : IOrderedQueryable<TElement>,
		IAsyncEnumerable<TElement>
	{
		public Type ElementType => inner.ElementType;
		public Expression Expression => inner.Expression;
		public IQueryProvider Provider => new FakeAsyncQueryProvider<TElement>(inner.Provider);

		public IEnumerator<TElement> GetEnumerator() => inner.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => inner.GetEnumerator();

		public IAsyncEnumerator<TElement> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
			new FakeAsyncEnumerator<TElement>(inner.GetEnumerator());
	}

	private sealed class FakeAsyncEnumerator<TElement>(IEnumerator<TElement> inner) : IAsyncEnumerator<TElement>
	{
		public TElement Current => inner.Current;

		public ValueTask<bool> MoveNextAsync() => new(inner.MoveNext());

		public ValueTask DisposeAsync()
		{
			inner.Dispose();

			return ValueTask.CompletedTask;
		}
	}
}
