namespace Pinveria.Infrastructure.Media.Optimization;

public static class MediaSignatureDetector
{
	public static string DetectFile(string path)
	{
		Span<byte> header = stackalloc byte[32];
		using var stream = File.OpenRead(path);
		var length = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

		return Detect(header[..length]);
	}

	public static string Detect(ReadOnlySpan<byte> signature)
	{
		if (signature.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
			return "image/png";

		if (signature.StartsWith(new byte[] { 255, 216, 255 }))
			return "image/jpeg";

		if (signature.Length >= 12)
		{
			if (signature[..4].SequenceEqual("RIFF"u8) && signature[8..12].SequenceEqual("WEBP"u8))
				return "image/webp";

			if (signature[4..8].SequenceEqual("ftyp"u8))
				return signature[8..12].SequenceEqual("avif"u8) ? "image/avif" : "video/mp4";
		}

		if (signature.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }))
			return "video/webm";

		// Only inspected originals are promoted into the public assets directory. SVG has no fixed binary signature.
		return "image/svg+xml";
	}

	public static bool IsRasterContentType(string contentType) =>
		contentType is "image/png" or "image/jpeg" or "image/webp" or "image/avif";
}
