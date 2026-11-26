using Pinveria.Domain.Categories;
using Pinveria.Domain.Games;
using Pinveria.Domain.Maps;
using Pinveria.Domain.Markers;
using Pinveria.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace Pinveria.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
	DbSet<Game> Games { get; }
	DbSet<CategoryGroup> CategoryGroups { get; }
	DbSet<Category> Categories { get; }
	DbSet<Marker> Markers { get; }
	DbSet<Map> Maps { get; }
	DbSet<MapStyle> MapStyles { get; }
	DbSet<MediaFolder> MediaFolders { get; }
	DbSet<MediaAsset> MediaAssets { get; }

	Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

	Task LockMapAsync(Guid mapId, CancellationToken cancellationToken = default);

	Task LockMarkerAsync(Guid markerId, CancellationToken cancellationToken = default);

	Task LockMediaFoldersAsync(CancellationToken cancellationToken = default);

	Task LockMediaAssetAsync(Guid assetId, bool exclusive = false, CancellationToken cancellationToken = default);

	Task LockMediaAssetsAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken = default);

	Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
