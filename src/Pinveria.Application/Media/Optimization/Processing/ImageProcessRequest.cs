namespace Pinveria.Application.Media.Optimization.Processing;

public sealed record ImageProcessRequest(
	string SourcePath,
	string SourceContentType,
	int? TargetWidth,
	ImageOutputFormat TargetFormat);
