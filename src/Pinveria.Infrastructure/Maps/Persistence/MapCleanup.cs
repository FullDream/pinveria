using Pinveria.Application.Maps.Storage;
using Pinveria.Infrastructure.Maps.Processing;
using Pinveria.Infrastructure.Persistence;

namespace Pinveria.Infrastructure.Maps.Persistence;

internal sealed class MapCleanup(ApplicationDbContext context, MapWorkQueue queue)
	: IMapCleanup
{
	private readonly List<MapCleanupIntent> pending = [];

	public void Schedule(Guid mapId, Guid? styleId = null)
	{
		var intent = new MapCleanupIntent { MapId = mapId, StyleId = styleId, RetryAt = DateTimeOffset.UtcNow };

		context.Set<MapCleanupIntent>().Add(intent);
		pending.Add(intent);
	}

	public void Dispatch()
	{
		foreach (var intent in pending)
		{
			queue.ScheduleCleanup(intent.Id, intent.MapId!.Value, intent.StyleId, intent.RetryAt);
		}

		pending.Clear();
	}
}
