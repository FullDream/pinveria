namespace Pinveria.Application.Media.Optimization.Caching;

public sealed class ImageCacheOptions
{
	public string CacheRoot { get; set; } = "storage/media-cache";
	public long MaxBytes { get; set; } = 2L * 1024 * 1024 * 1024;
	public double EvictFraction { get; set; } = 0.15;
	public int MaxIdleDays { get; set; } = 30;
	public int TouchIntervalMinutes { get; set; } = 60;
	public int SweepIntervalMinutes { get; set; } = 30;
}
