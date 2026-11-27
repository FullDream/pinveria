namespace Pinveria.Application.Media.Optimization;

public sealed record ImageVariantPayload(
	string FilePath,
	string ContentType,
	long ByteLength,
	string ETag,
	bool VaryByAccept);
