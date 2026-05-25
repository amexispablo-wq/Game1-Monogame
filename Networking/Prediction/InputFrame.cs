using System.Collections.Generic;

namespace Game1_Monogame;

public sealed class InputFrame
{
    public InputFrame()
    {
    }

    public InputFrame(SimulationTick tick, int ownerId)
    {
        Tick = tick.Value;
        OwnerId = ownerId;
    }

    public long Tick { get; set; }
    public int OwnerId { get; set; }
    public List<PlayerInputEntry> PlayerInputs { get; } = new();

    public void AddPlayerInput(int networkId, PlayerInputState input)
    {
        PlayerInputs.Add(new PlayerInputEntry(networkId, input.Sanitized()));
    }

    public Dictionary<int, PlayerInputState> ToInputMap()
    {
        Dictionary<int, PlayerInputState> inputMap = new();
        foreach (PlayerInputEntry entry in PlayerInputs)
        {
            inputMap[entry.NetworkId] = entry.Input.Sanitized();
        }

        return inputMap;
    }
}

public readonly record struct PlayerInputEntry(int NetworkId, PlayerInputState Input);
