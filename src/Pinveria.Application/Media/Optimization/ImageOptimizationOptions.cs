namespace Pinveria.Application.Media.Optimization;

public sealed class ImageOptimizationOptions
{
	public int[] AllowedWidths { get; set; } = [160, 320, 480, 640, 960, 1280, 1600, 1920];
	public int AvifQuality { get; set; } = 50;
	public int AvifEffort { get; set; } = 4;
	public int WebpQuality { get; set; } = 80;
	public int WebpEffort { get; set; } = 4;
	public int JpegQuality { get; set; } = 82;
	public int MaxConcurrency { get; set; } = 1;
	public int QueueTimeoutSeconds { get; set; } = 8;
}
