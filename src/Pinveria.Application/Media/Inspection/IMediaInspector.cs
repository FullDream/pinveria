using Pinveria.Domain.Result;

namespace Pinveria.Application.Media.Inspection;

public interface IMediaInspector
{
	Task<Result<MediaInspection>> InspectAsync(string path, CancellationToken cancellationToken);
}
