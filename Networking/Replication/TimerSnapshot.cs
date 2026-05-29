namespace ColorBlocks;

public readonly record struct TimerSnapshot(
    float ElapsedTime,
    bool IsRunning,
    bool IsComplete,
    float FinalTime,
    bool NewRecord);
