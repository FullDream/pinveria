using Pinveria.Application.Maps.Processing;
using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Application.Media;
using Pinveria.Domain.Maps;
using Pinveria.Domain.Media;
using Pinveria.Domain.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Pinveria.Application.Maps.AddMapStyle;

public sealed class AddMapStyleHandler(IApplicationDbContext context, IMapProgress progress,
	IMapProcessingQueue queue, IMapNotifications notifications) : ICommandHandler<AddMapStyle, Result<MapDto>>
{
	public async ValueTask<Result<MapDto>> Handle(AddMapStyle command, CancellationToken cancellationToken)
	{
		await using var transaction = await context.BeginTransactionAsync(cancellationToken);
		await context.LockMapAsync(command.MapId, cancellationToken);
		var map = await context.Maps.AsTracking().ForMap(command.GameId, command.MapId).WithStyles()
			.SingleOrDefaultAsync(cancellationToken);

		if (map is null)
			return MapOperationErrors.NotFound;

		if (command.SourceAssetId is { } sourceAssetId)
			await context.LockMediaAssetAsync(sourceAssetId, exclusive: false, cancellationToken);

		var asset = await context.MediaAssets.AsTracking()
			.SingleOrDefaultAsync(asset => asset.Id == command.SourceAssetId, cancellationToken);

		if (asset is null)
			return await context.MediaFolders.AnyAsync(folder => folder.Id == command.SourceAssetId, cancellationToken)
				? MediaOperationErrors.IncompatibleAsset
				: MediaOperationErrors.AssetNotFound;

		if (asset is not ImageAsset image || !image.IsRaster)
			return MediaOperationErrors.IncompatibleAsset;

		var added = map.AddStyle(new MapStyleInput(command.Name, command.BackgroundColor, image));

		if (added.IsFailure)
			return added.Errors;

		await context.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		var style = added.Value;
		var response = map.ToDto(progress);

		queue.Enqueue(style.MapId, style.Id, style.AttemptId);
		await notifications.StyleUpdatedAsync(map.Id, response.Styles.Single(item => item.Id == style.Id));

		return response;
	}
}
