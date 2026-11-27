namespace Pinveria.Application.Media.Optimization;

public sealed record ImageVariantResult(
	ImageVariantOutcome Outcome,
	ImageVariantPayload? Payload = null,
	bool VaryByAccept = false)
{
	public static ImageVariantResult Variant(ImageVariantPayload payload) =>
		new(ImageVariantOutcome.Variant, payload, payload.VaryByAccept);

	public static ImageVariantResult Passthrough(bool varyByAccept = false) =>
		new(ImageVariantOutcome.Passthrough, VaryByAccept: varyByAccept);

	public static ImageVariantResult BusyFallback(bool varyByAccept = false) =>
		new(ImageVariantOutcome.BusyFallback, VaryByAccept: varyByAccept);

	public static ImageVariantResult NotFound() =>
		new(ImageVariantOutcome.NotFound);
}
