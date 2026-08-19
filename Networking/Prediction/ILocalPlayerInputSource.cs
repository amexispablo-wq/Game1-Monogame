namespace ColorBlocks;

public interface ILocalPlayerInputSource
{
    PlayerInputState GetPlayerInput(int networkId);

    /// <summary>Clear one-shot UI pulses (respawn/restart) after they were latched.</summary>
    void ConsumeEdgePulses()
    {
    }
}
