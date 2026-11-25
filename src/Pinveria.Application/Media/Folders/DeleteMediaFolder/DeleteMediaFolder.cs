using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Media.Folders.DeleteMediaFolder;

public sealed record DeleteMediaFolder(Guid Id) : ICommand<Result>;
