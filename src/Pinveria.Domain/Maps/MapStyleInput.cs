using Pinveria.Domain.Media;

namespace Pinveria.Domain.Maps;

public sealed record MapStyleInput(string? Name, string? BackgroundColor, ImageAsset? Source);
