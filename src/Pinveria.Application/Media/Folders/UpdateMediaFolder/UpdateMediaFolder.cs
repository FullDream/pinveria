using Pinveria.Domain;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Media.Folders.UpdateMediaFolder;

public sealed record UpdateMediaFolder(Guid Id, Optional<string?> Name = default, Optional<Guid?> ParentId = default)
	: ICommand<Result<MediaFolderDto>>;
