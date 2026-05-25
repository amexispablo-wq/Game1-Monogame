namespace Game1_Monogame;

public readonly record struct TimerSnapshot(
    float ElapsedTime,
    bool IsRunning,
    bool IsComplete,
    float FinalTime,
    bool NewRecord);
