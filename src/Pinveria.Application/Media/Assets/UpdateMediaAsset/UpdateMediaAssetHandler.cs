using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Domain.Media;
using Pinveria.Domain.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Pinveria.Application.Media.Assets.UpdateMediaAsset;

public sealed class UpdateMediaAssetHandler(IApplicationDbContext context)
	: ICommandHandler<UpdateMediaAsset, Result<MediaAssetDto>>
{
	public async ValueTask<Result<MediaAssetDto>> Handle(UpdateMediaAsset command, CancellationToken cancellationToken)
	{
		await using var transaction = await context.BeginTransactionAsync(cancellationToken);

		if (command.FolderId.IsSpecified && command.FolderId.Value is not null)
			await context.LockMediaFoldersAsync(cancellationToken);

		var asset = await context.MediaAssets.AsTracking().SingleOrDefaultAsync(item => item.Id == command.Id,
			cancellationToken);

		if (asset is null)
			return MediaOperationErrors.AssetNotFound;

		if (command.FolderId.IsSpecified && command.FolderId.Value is { } folderId &&
			!await context.MediaFolders.AnyAsync(folder => folder.Id == folderId, cancellationToken))
			return MediaOperationErrors.FolderNotFound;

		if (command.PreviewAssetId.IsSpecified && command.PreviewAssetId.Value is { } previewId)
		{
			await context.LockMediaAssetAsync(previewId, exclusive: false, cancellationToken);
			var preview = await context.MediaAssets.SingleOrDefaultAsync(item => item.Id == previewId,
				cancellationToken);

			if (preview is null)
				return await context.MediaFolders.AnyAsync(folder => folder.Id == previewId, cancellationToken)
					? MediaOperationErrors.IncompatibleAsset
					: MediaOperationErrors.AssetNotFound;

			if (preview is not ImageAsset)
				return MediaOperationErrors.IncompatibleAsset;
		}

		var updated = asset.Update(command.Name, command.FolderId, command.Alt, command.PreviewAssetId);

		if (updated.IsFailure)
			return updated.Errors;

		if (updated.Value)
			await context.SaveChangesAsync(cancellationToken);

		var response = await context.MediaAssets.AsNoTracking().WithPreview()
			.SingleAsync(item => item.Id == command.Id, cancellationToken);

		await transaction.CommitAsync(cancellationToken);

		return response.ToDto();
	}
}
