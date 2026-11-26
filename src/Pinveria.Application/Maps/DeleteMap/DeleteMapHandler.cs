using Pinveria.Application.Maps.Processing;
using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Application.Maps.Storage;
using Pinveria.Domain.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Pinveria.Application.Maps.DeleteMap;

public sealed class DeleteMapHandler(IApplicationDbContext context, IMapCleanup cleanup,
	IMapNotifications notifications)
	: ICommandHandler<DeleteMap, Result>
{
	public async ValueTask<Result> Handle(DeleteMap command, CancellationToken cancellationToken)
	{
		await using var transaction = await context.BeginTransactionAsync(cancellationToken);
		await context.LockMapAsync(command.MapId, cancellationToken);
		var mapId = await context.Maps.ForMap(command.GameId, command.MapId).Select(map => (Guid?)map.Id)
			.SingleOrDefaultAsync(cancellationToken);

		if (mapId is null)
			return MapOperationErrors.NotFound;

		cleanup.Schedule(mapId.Value);
		await context.Maps.ForMap(command.GameId, command.MapId).ExecuteDeleteAsync(cancellationToken);
		await context.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);

		cleanup.Dispatch();
		await notifications.ProcessingUpdatedAsync(new MapProcessingDto(mapId.Value, null, null, null, 0, null,
			Deleted: true));

		return Result.Success();
	}
}
