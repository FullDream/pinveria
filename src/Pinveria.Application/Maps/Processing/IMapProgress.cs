using Pinveria.Domain.Maps;

namespace Pinveria.Application.Maps.Processing;

public interface IMapProgress
{
	int Read(MapStyle style);
}
