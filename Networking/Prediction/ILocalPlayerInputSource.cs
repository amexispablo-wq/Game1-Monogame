namespace ColorBlocks;

public interface ILocalPlayerInputSource
{
    PlayerInputState GetPlayerInput(PlayerId playerId);
}
