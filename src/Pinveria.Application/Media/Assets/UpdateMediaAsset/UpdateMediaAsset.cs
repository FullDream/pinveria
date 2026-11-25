using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Media.Assets.UpdateMediaAsset;

public sealed record UpdateMediaAsset(Guid Id, Optional<string?> Name, Optional<Guid?> FolderId,
	Optional<string?> Alt, Optional<Guid?> PreviewAssetId) : ICommand<Result<MediaAssetDto>>;
