namespace Pinveria.Application.Media.Optimization.Processing;

public sealed record ImageProcessResult(
	ImageProcessStatus Status,
	byte[]? Bytes = null,
	string? ContentType = null,
	string? Extension = null,
	bool ClampedToOriginalWidth = false)
{
	public static ImageProcessResult Encoded(byte[] bytes, string contentType, string extension,
		bool clampedToOriginalWidth) =>
		new(ImageProcessStatus.Encoded, bytes, contentType, extension, clampedToOriginalWidth);

	public static ImageProcessResult UseOriginal(bool clampedToOriginalWidth = true) =>
		new(ImageProcessStatus.UseOriginal, ClampedToOriginalWidth: clampedToOriginalWidth);

	public static ImageProcessResult BusyTimeout() =>
		new(ImageProcessStatus.BusyTimeout);
}
