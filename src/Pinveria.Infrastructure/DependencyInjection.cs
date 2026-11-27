using Pinveria.Application.Abstractions.Persistence;
using Pinveria.Application.Maps.Images;
using Pinveria.Application.Maps.Processing;
using Pinveria.Application.Maps.Storage;
using Pinveria.Application.Media.Optimization;
using Pinveria.Application.Media.Optimization.Caching;
using Pinveria.Application.Media.Optimization.Processing;
using Pinveria.Application.Media.Storage;
using Pinveria.Application.Media.Inspection;
using Pinveria.Infrastructure.Media.Inspection;
using Pinveria.Infrastructure.Media.Optimization;
using Pinveria.Infrastructure.Media.Storage;
using Pinveria.Infrastructure.Maps.Images;
using Pinveria.Infrastructure.Maps.Persistence;
using Pinveria.Infrastructure.Maps.Processing;
using Pinveria.Infrastructure.Maps.Storage;
using Pinveria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Pinveria.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var connectionString = configuration.GetConnectionString("Pinveria");

		if (string.IsNullOrWhiteSpace(connectionString))
			throw new InvalidOperationException("Connection string 'Pinveria' is required.");

		services.AddDbContext<ApplicationDbContext>(options => options
			.UseNpgsql(connectionString, npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
			.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

		services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
		services.AddSingleton<IPersistenceFailureClassifier, PostgresPersistenceFailureClassifier>();
		services.AddOptions<MediaStorageOptions>().Bind(configuration.GetSection("Media")).ValidateOnStart();
		services.AddOptions<MediaLimits>().Bind(configuration.GetSection("Media:Limits")).ValidateOnStart();
		services.AddOptions<ImageOptimizationOptions>().Bind(configuration.GetSection("Media:Optimization")).ValidateOnStart();
		services.AddOptions<ImageCacheOptions>().Bind(configuration.GetSection("Media:ImageCache")).ValidateOnStart();
		services.AddSingleton<IValidateOptions<MediaStorageOptions>, MediaOptionsValidation>();
		services.AddSingleton<IValidateOptions<MediaLimits>, MediaOptionsValidation>();
		services.AddSingleton<IValidateOptions<ImageOptimizationOptions>, ImageOptimizationOptionsValidation>();
		services.AddSingleton<IValidateOptions<ImageCacheOptions>, ImageOptimizationOptionsValidation>();
		services.AddSingleton<IMediaStorage, LocalMediaStorage>();
		services.AddSingleton<IImageProcessor, VipsImageProcessor>();
		services.AddSingleton<IImageVariantCache, FileSystemImageVariantCache>();
		services.AddSingleton<IImageVariantService, ImageVariantService>();
		services.AddHostedService<ImageCacheSweepService>();
		services.AddSingleton<RasterInspector>();
		services.AddSingleton<SvgInspector>();
		services.AddSingleton<VideoInspector>();
		services.AddSingleton<IMediaInspector, MediaInspector>();
		services.AddOptions<MapStorageOptions>().Bind(configuration.GetSection("Maps")).ValidateOnStart();
		services.AddSingleton<IValidateOptions<MapStorageOptions>, MapStorageOptionsValidation>();
		services.AddSingleton<IMapStorage, LocalMapStorage>();
		services.AddSingleton<IMapImageProcessor, VipsMapImageProcessor>();
		services.AddScoped<IMapCleanup, MapCleanup>();
		services.AddSingleton<MapProcessor>();
		services.AddSingleton<MapProgressStore>();
		services.AddSingleton<IMapProgress>(provider => provider.GetRequiredService<MapProgressStore>());
		services.AddSingleton<MapWorkQueue>();
		services.AddSingleton<IMapProcessingQueue>(provider => provider.GetRequiredService<MapWorkQueue>());
		services.AddHostedService<MapProcessingService>();

		return services;
	}
}
