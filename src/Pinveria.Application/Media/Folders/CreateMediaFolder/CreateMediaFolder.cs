using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Media.Folders.CreateMediaFolder;

public sealed record CreateMediaFolder(string? Name, Guid? ParentId = null) : ICommand<Result<MediaFolderDto>>;
