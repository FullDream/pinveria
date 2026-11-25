using System.Text.Json.Serialization;

namespace Pinveria.Api.Markers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PositionRequest(double? X, double? Y);
