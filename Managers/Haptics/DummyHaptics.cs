#nullable enable
namespace ColorBlocks;

public sealed class DummyHaptics : IHaptics
{
    public static DummyHaptics Instance { get; } = new();

    public void Play(HapticEvent hapticEvent, int localPlayerSlot = 0)
    {
    }

    public void Stop(int localPlayerSlot = 0)
    {
    }
}
