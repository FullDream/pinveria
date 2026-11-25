using Pinveria.Application.Common.Pagination;
using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Media.ListMedia;

public sealed record ListMedia(Guid? FolderId = null, int Offset = 0, int Limit = 24, string? Search = null)
	: IQuery<Result<PaginatedResult<MediaItemDto>>>;
