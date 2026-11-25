using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Domain.Media;
using Pinveria.Domain.Result;
using Microsoft.EntityFrameworkCore;

namespace Pinveria.Application.Media;

public static class MediaReferenceExtensions
{
	public static async Task<Result<ImageAsset?>> FindOptionalImageAsync(this IApplicationDbContext context, Guid? id, CancellationToken cancellationToken)
	{
		if (id is null)
			return Result<ImageAsset?>.Success(null);

		var result = await context.FindImageAsync(id.Value, cancellationToken);

		return result.IsFailure ? Result<ImageAsset?>.Failure(result.Errors) : Result<ImageAsset?>.Success(result.Value);
	}

	public static async Task<Result<ImageAsset>> FindImageAsync(this IApplicationDbContext context, Guid id,
		CancellationToken cancellationToken)
	{
		var asset = await context.MediaAssets.SingleOrDefaultAsync(asset => asset.Id == id, cancellationToken);

		if (asset is ImageAsset image)
			return image;

		if (asset is not null ||
			await context.MediaFolders.AnyAsync(folder => folder.Id == id, cancellationToken))
			return MediaOperationErrors.IncompatibleAsset;

		return MediaOperationErrors.AssetNotFound;
	}
}
