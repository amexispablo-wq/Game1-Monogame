#nullable enable
using System.Diagnostics;

namespace ColorBlocks;

/// <summary>
/// Wall-clock that only accumulates during active gameplay (timer running, unpaused,
/// focused). Used for upload-only speedhack checks — never alters simulation.
/// </summary>
public sealed class ActiveWallClock
{
    private readonly Stopwatch _stopwatch = new();
    private bool _captured;
    private double _capturedSeconds;

    /// <summary>Seconds accumulated for the current (or last captured) attempt.</summary>
    public float ElapsedSeconds =>
        _captured
            ? (float)_capturedSeconds
            : (float)_stopwatch.Elapsed.TotalSeconds;

    public void Reset()
    {
        _stopwatch.Reset();
        _captured = false;
        _capturedSeconds = 0d;
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

        if (shouldAccumulate)
        {
            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
            }

            return;
        }

        if (_stopwatch.IsRunning)
        {
            _stopwatch.Stop();
        }
    }

    /// <summary>Freeze the value at level complete for replay metadata / upload.</summary>
    public void CaptureFinal()
    {
        if (_captured)
        {
            return;
        }

        _stopwatch.Stop();
        _capturedSeconds = _stopwatch.Elapsed.TotalSeconds;
        _captured = true;
    }
}
