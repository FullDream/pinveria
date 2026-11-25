using Pinveria.Application.Media;
using Pinveria.Domain;

namespace Pinveria.Application.Games;

public sealed record GameMapDto(Guid Id, string Name, string Slug, ImageAssetDto? Preview, PublicationStatus Status);
