namespace Game1_Monogame;

public interface ILocalPlayerInputSource
{
    PlayerInputState GetPlayerInput(PlayerId playerId);
}
