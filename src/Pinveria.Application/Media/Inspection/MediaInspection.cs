namespace Pinveria.Application.Media.Inspection;

public sealed record MediaInspection(string ContentType, double? Width = null, double? Height = null,
	string? Placeholder = null);
