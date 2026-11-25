using Pinveria.Domain.Result;

namespace Pinveria.Application.Games.CreateGame;

public static class CreateGameErrors
{
	public static readonly Error SlugConflict =
		new("Game.Slug.Conflict", "A game with this slug already exists.", nameof(CreateGame.Slug), ErrorType.Conflict);
}
