#nullable enable
namespace ColorBlocks;

public enum HapticEvent
{
    SmallImpact,
    MediumImpact,
    LargeImpact,
    Checkpoint,
    Goal,
    PullRope
}

public interface IHaptics
{
    void Play(HapticEvent hapticEvent, int localPlayerSlot = 0);
    void Stop(int localPlayerSlot = 0);
}
