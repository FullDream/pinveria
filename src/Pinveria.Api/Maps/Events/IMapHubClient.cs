using Pinveria.Application.Maps;
using Pinveria.Application.Maps.Processing;

namespace Pinveria.Api.Maps.Events;

public interface IMapHubClient
{
	Task StyleUpdated(MapStyleDto style);
	Task ProcessingUpdated(MapProcessingDto processing);
}
