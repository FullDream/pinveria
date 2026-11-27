namespace Pinveria.Application.Media.Optimization;

public sealed record ImageTransformCommand(
	Guid AssetId,
	int? Width,
	ImageOutputFormat OutputFormat,
	bool VaryByAccept);
