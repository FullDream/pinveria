using Pinveria.Domain.Result;

namespace Pinveria.Application.Games.GetGame;

public static class GetGameErrors
{
	public static readonly Error NotFound = new(
		"Game.NotFound", "The game was not found.", nameof(GetGame.Id), ErrorType.NotFound);
}
