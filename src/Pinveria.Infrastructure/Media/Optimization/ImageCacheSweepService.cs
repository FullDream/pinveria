using Pinveria.Application.Media.Optimization.Caching;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Pinveria.Infrastructure.Media.Optimization;

public sealed class ImageCacheSweepService(
	IImageVariantCache cache,
	IOptions<ImageCacheOptions> options,
	ILogger<ImageCacheSweepService> logger) : BackgroundService
{
	public override async Task StartAsync(CancellationToken cancellationToken)
	{
		try
		{
			cache.CleanOrphanedTempFiles();
			await cache.SweepAsync(cancellationToken);
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			logger.LogWarning(exception, "Failed to initialize or sweep the derived image variant cache on startup.");
		}

		await base.StartAsync(cancellationToken);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.SweepIntervalMinutes));
		using var timer = new PeriodicTimer(interval);
		var fsCache = cache as FileSystemImageVariantCache;

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				if (fsCache is not null)
				{
					var timerTask = timer.WaitForNextTickAsync(stoppingToken).AsTask();
					var thresholdTask = fsCache.ThresholdSignals.WaitToReadAsync(stoppingToken).AsTask();

					await Task.WhenAny(timerTask, thresholdTask);

					while (fsCache.ThresholdSignals.TryRead(out _))
					{
					}
				}
				else if (!await timer.WaitForNextTickAsync(stoppingToken))
				{
					break;
				}

				await cache.SweepAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				logger.LogWarning(exception, "Derived image variant cache sweep failed.");
			}
		}
	}
}
