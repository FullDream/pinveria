using Pinveria.Application.Media.Inspection;
using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Media.Assets.AdmitMediaAsset;

public sealed record AdmitMediaAsset(Guid UploadId, Guid AssetId, string? Name, Guid? FolderId,
	Optional<string?> Alt, Optional<Guid?> PreviewAssetId, long ByteLength, MediaInspection Inspection)
	: ICommand<Result<MediaAssetDto>>;
