#nullable enable
using System;
using System.Diagnostics;

namespace ColorBlocks;

/// <summary>
/// Gameplay frame dt from multiple OS clocks. Each source is integrated separately;
/// delivery tracks the <b>max of cumulative totals</b>.
/// <para>
/// Do <b>not</b> use per-frame <c>max(dTick, dUtc)</c>: TickCount and UtcNow quantize on
/// staggered edges (~15ms). Taking max each frame double-counts ≈ 2× real time at high FPS.
/// </para>
/// Resists Cheat Engine QPC-only slowdown: when QPC lags, TickCount/Utc totals still pull target forward.
/// </summary>
public sealed class TrustedFrameClock
{
    private bool _hasSample;
    private long _lastQpc;
    private long _lastTickCount;
    private long _lastUtcTicks;
    private double _qpcTotal;
    private double _tickTotal;
    private double _utcTotal;
    private double _delivered;

    public void Reset()
    {
        _hasSample = false;
        _lastQpc = 0;
        _lastTickCount = 0;
        _lastUtcTicks = 0;
        _qpcTotal = 0d;
        _tickTotal = 0d;
        _utcTotal = 0d;
        _delivered = 0d;
    }

    /// <summary>
    /// Sample clocks and return clamped frame delta in seconds.
    /// First call after <see cref="Reset"/> returns 0 (establishes baseline).
    /// </summary>
    public float Tick(float maxFrameSeconds)
    {
        long qpc = Stopwatch.GetTimestamp();
        long tickCount = Environment.TickCount64;
        long utcTicks = DateTime.UtcNow.Ticks;

        if (!_hasSample)
        {
            _lastQpc = qpc;
            _lastTickCount = tickCount;
            _lastUtcTicks = utcTicks;
            _hasSample = true;
            return 0f;
        }

        double dQpc = (qpc - _lastQpc) / (double)Stopwatch.Frequency;
        double dTick = (tickCount - _lastTickCount) / 1000.0;
        double dUtc = (utcTicks - _lastUtcTicks) / (double)TimeSpan.TicksPerSecond;

        _lastQpc = qpc;
        _lastTickCount = tickCount;
        _lastUtcTicks = utcTicks;

        if (dQpc < 0d)
        {
            dQpc = 0d;
        }

        if (dTick < 0d)
        {
            dTick = 0d;
        }

        if (dUtc < 0d)
        {
            dUtc = 0d;
        }

        _qpcTotal += dQpc;
        _tickTotal += dTick;
        _utcTotal += dUtc;

        double target = Math.Max(_qpcTotal, Math.Max(_tickTotal, _utcTotal));
        double delta = target - _delivered;
        if (delta < 0d)
        {
            delta = 0d;
        }

        float max = MathF.Max(0f, maxFrameSeconds);
        delta = Math.Min(delta, max);
        _delivered += delta;
        return (float)delta;
    }
}
