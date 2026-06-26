namespace ColorBlocks;

public interface ILocalPlayerInputSource
{
    PlayerInputState GetPlayerInput(int networkId);
}
