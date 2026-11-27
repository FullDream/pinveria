using Pinveria.Application.Media.Storage;
using Pinveria.Infrastructure.Media.Optimization;
using Microsoft.AspNetCore.StaticFiles;

namespace Pinveria.Api.Media;

internal sealed class MediaFileContentTypeProvider(IMediaStorage storage) : IContentTypeProvider
{
	public bool TryGetContentType(string subpath, out string contentType)
	{
		contentType = "application/octet-stream";

		if (subpath.Length < 2 || !Guid.TryParseExact(subpath.AsSpan(1), "N", out var id))
			return false;

		var path = storage.AssetPath(id);

		if (!File.Exists(path))
			return false;

		contentType = MediaSignatureDetector.DetectFile(path);

		return true;
	}
}
