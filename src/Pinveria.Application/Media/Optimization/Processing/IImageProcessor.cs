namespace Pinveria.Application.Media.Optimization.Processing;

public interface IImageProcessor
{
	int? ReadIntrinsicWidth(string sourcePath);
	Task<ImageProcessResult> ProcessAsync(ImageProcessRequest request, CancellationToken cancellationToken);
}
