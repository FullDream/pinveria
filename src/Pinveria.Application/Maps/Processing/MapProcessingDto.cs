using Pinveria.Domain.Maps;

namespace Pinveria.Application.Maps.Processing;

public sealed record MapProcessingDto(Guid MapId, Guid? StyleId, Guid? AttemptId, MapProcessingStatus? Status,
	int Progress, MapProcessingErrorDto? Error, bool Deleted = false);
