#nullable enable
using System;

namespace ColorBlocks;

/// <summary>
/// Wall-clock that only accumulates during active gameplay (timer running, unpaused,
/// focused). Fed trusted frame deltas from <see cref="TrustedFrameClock"/> — never
/// alters simulation. Used for upload-only speedhack checks.
/// </summary>
public sealed class ActiveWallClock
{
    private double _elapsedSeconds;
    private bool _accumulating;
    private bool _captured;

    /// <summary>Seconds accumulated for the current (or last captured) attempt.</summary>
    public float ElapsedSeconds => (float)_elapsedSeconds;

    public void Reset()
    {
        _elapsedSeconds = 0d;
        _accumulating = false;
        _captured = false;
    }

    /// <summary>
    /// Start/stop accumulation. No-op after <see cref="CaptureFinal"/> until
    /// <see cref="Reset"/>.
    /// </summary>
    public void SetAccumulating(bool shouldAccumulate)
    {
        if (_captured)
        {
            return;
        }

        _accumulating = shouldAccumulate;
    }

    /// <summary>
    /// Add one frame of trusted wall time when accumulating.
    /// </summary>
    public void AddDelta(float deltaSeconds)
    {
        if (_captured || !_accumulating)
        {
            return;
        }

        if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
        {
            return;
        }

        _elapsedSeconds += deltaSeconds;
    }

    /// <summary>Freeze the value at level complete for replay metadata / upload.</summary>
    public void CaptureFinal()
    {
        if (_captured)
        {
            return;
        }

        _accumulating = false;
        _captured = true;
    }
}
