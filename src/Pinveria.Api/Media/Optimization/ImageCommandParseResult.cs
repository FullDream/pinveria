using Pinveria.Application.Media.Optimization;

namespace Pinveria.Api.Media.Optimization;

public sealed record ImageCommandParseResult(
	ImageCommandParseOutcome Outcome,
	ImageTransformCommand? Command = null)
{
	public static ImageCommandParseResult None() =>
		new(ImageCommandParseOutcome.None);

	public static ImageCommandParseResult Invalid() =>
		new(ImageCommandParseOutcome.Invalid);

	public static ImageCommandParseResult Parsed(ImageTransformCommand command) =>
		new(ImageCommandParseOutcome.Command, command);
}
