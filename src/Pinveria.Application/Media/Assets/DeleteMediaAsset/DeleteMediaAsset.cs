using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Media.Assets.DeleteMediaAsset;

public sealed record DeleteMediaAsset(Guid Id) : ICommand<Result>;
