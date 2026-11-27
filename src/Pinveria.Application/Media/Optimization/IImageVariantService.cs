namespace Pinveria.Application.Media.Optimization;

public interface IImageVariantService
{
	Task<ImageVariantResult> GetOrCreateAsync(ImageTransformCommand command, CancellationToken cancellationToken);
}
