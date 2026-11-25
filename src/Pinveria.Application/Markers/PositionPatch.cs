using Pinveria.Domain;

namespace Pinveria.Application.Markers;

public sealed record PositionPatch(Optional<double?> X = default, Optional<double?> Y = default);
