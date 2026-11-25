using Pinveria.Domain.Result;
using Mediator;

namespace Pinveria.Application.Games.GetPublicGame;

public sealed record GetPublicGame(string Slug) : IQuery<Result<PublicGameDto>>;
