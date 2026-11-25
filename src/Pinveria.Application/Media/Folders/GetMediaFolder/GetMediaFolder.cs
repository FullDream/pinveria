using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Media.Folders.GetMediaFolder;

public sealed record GetMediaFolder(Guid Id) : IQuery<Result<MediaFolderDetailsDto>>;
