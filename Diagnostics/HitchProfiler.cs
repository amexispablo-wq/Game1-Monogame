#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>
/// Bounded FPS / hitch capture around level-select and level-enter.
/// Uses wall-clock timestamps (not GameTime, which is capped and lags one frame).
/// </summary>
public static class HitchProfiler
{
    private const int MaxFrames = 120;
    private const float CaptureSeconds = 2f;
    private const float SlowFrameMs = 1000f / 60f;
    private const float ImmediateMarkMs = 2f;

    private static readonly FrameSample[] Frames = new FrameSample[MaxFrames];
    private static readonly List<MarkSample> Marks = new(64);

    private static bool _capturing;
    private static string _reason = "";
    private static int _partyCount;
    private static string _sceneName = "";
    private static int _frameCount;
    private static long _captureStart;
    private static long _frameStart;
    private static long _updateEnd;
    private static float _lastFps;
    private static string _lastSlowMark = "";
    private static float _lastSlowMarkMs;

    public static bool IsCapturing => _capturing;

    private static bool Enabled => DeveloperSettings.DeveloperMode;

    public static void Begin(string reason, int partyCount = 1, string extra = "")
    {
        if (!Enabled)
        {
            return;
        }
        if (_capturing && string.Equals(_reason, reason, StringComparison.Ordinal))
        {
            return;
        }

        if (_capturing)
        {
            Finish();
        }

        _capturing = true;
        _reason = reason;
        _partyCount = Math.Max(1, partyCount);
        _sceneName = "";
        _frameCount = 0;
        _captureStart = Stopwatch.GetTimestamp();
        _frameStart = 0;
        _updateEnd = 0;
        _lastFps = 0f;
        _lastSlowMark = "";
        _lastSlowMarkMs = 0f;
        Marks.Clear();

        string suffix = string.IsNullOrWhiteSpace(extra) ? "" : $" {extra}";
        DiagnosticsLog.Info("Hitch", $"begin {reason} party={_partyCount}{suffix}");
    }

    public static HitchScope Scope(string name) => Enabled ? new HitchScope(name) : default;

    public static void Mark(string name, double milliseconds)
    {
        if (!_capturing || string.IsNullOrEmpty(name))
        {
            return;
        }

        float ms = (float)milliseconds;
        Marks.Add(new MarkSample(name, ms));
        if (ms >= ImmediateMarkMs)
        {
            if (ms >= _lastSlowMarkMs)
            {
                _lastSlowMark = name;
                _lastSlowMarkMs = ms;
            }

            DiagnosticsLog.Info("Hitch", $"mark {name} {ms:0.0}ms");
        }
    }

    public static void BeginFrame(string sceneName, int partyCount)
    {
        if (!_capturing)
        {
            return;
        }

        _sceneName = sceneName;
        _partyCount = Math.Max(1, partyCount);
        _frameStart = Stopwatch.GetTimestamp();
    }

    public static void EndUpdate()
    {
        if (!_capturing || _frameStart == 0)
        {
            return;
        }

        _updateEnd = Stopwatch.GetTimestamp();
    }

    public static void EndDraw()
    {
        if (!_capturing || _frameStart == 0)
        {
            return;
        }

        long now = Stopwatch.GetTimestamp();
        double dtMs = TicksToMs(now - _frameStart);
        double updateMs = _updateEnd > _frameStart ? TicksToMs(_updateEnd - _frameStart) : 0d;
        double drawMs = _updateEnd > 0 ? TicksToMs(now - _updateEnd) : TicksToMs(now - _frameStart);
        float fps = dtMs > 0.001d ? (float)(1000d / dtMs) : 0f;
        _lastFps = fps;

        if (_frameCount < MaxFrames)
        {
            Frames[_frameCount] = new FrameSample(fps, (float)dtMs, (float)updateMs, (float)drawMs);
            _frameCount++;
        }

        if (dtMs >= SlowFrameMs)
        {
            DiagnosticsLog.Info(
                "Hitch",
                $"frame fps={fps:0} dt={dtMs:0.0}ms update={updateMs:0.0} draw={drawMs:0.0} scene={_sceneName}");
        }

        _frameStart = 0;
        _updateEnd = 0;

        double elapsed = TicksToMs(now - _captureStart) / 1000d;
        if (_frameCount >= MaxFrames || elapsed >= CaptureSeconds)
        {
            Finish();
        }
    }

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (!_capturing)
        {
            return;
        }

        string fpsLine = _lastFps > 0f ? $"HITCH {_reason}  {_lastFps:0} FPS" : $"HITCH {_reason}";
        string markLine = string.IsNullOrEmpty(_lastSlowMark)
            ? $"party={_partyCount}  {_sceneName}"
            : $"{_lastSlowMark} {_lastSlowMarkMs:0.0}ms";

        const int scale = 2;
        const int x = 12;
        int y = 10;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, fpsLine, new Vector2(x, y), scale, Color.Yellow);
        y += SimpleTextRenderer.MeasureString("A", scale).Y + 2;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, markLine, new Vector2(x, y), scale, new Color(255, 210, 90));
    }

    public readonly struct HitchScope : IDisposable
    {
        private readonly string _name;
        private readonly long _start;

        public HitchScope(string name)
        {
            _name = name;
            _start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            if (_start == 0)
            {
                return;
            }

            Mark(_name, TicksToMs(Stopwatch.GetTimestamp() - _start));
        }
    }

    private static void Finish()
    {
        if (!_capturing)
        {
            return;
        }

        _capturing = false;
        if (_frameCount <= 0)
        {
            DiagnosticsLog.Info("Hitch", $"summary {_reason} frames=0 marks={Marks.Count}");
            return;
        }

        float minFps = float.MaxValue;
        float maxFps = 0f;
        double fpsSum = 0d;
        float[] fpsSorted = new float[_frameCount];
        int[] worst = new int[_frameCount];
        for (int i = 0; i < _frameCount; i++)
        {
            float fps = Frames[i].Fps;
            fpsSorted[i] = fps;
            worst[i] = i;
            fpsSum += fps;
            if (fps < minFps)
            {
                minFps = fps;
            }

            if (fps > maxFps)
            {
                maxFps = fps;
            }
        }

        Array.Sort(fpsSorted);
            float p95 = fpsSorted[Math.Clamp((int)MathF.Floor((_frameCount - 1) * 0.95f), 0, _frameCount - 1)];
        Array.Sort(worst, (a, b) => Frames[b].DtMs.CompareTo(Frames[a].DtMs));

        int worstCount = Math.Min(8, _frameCount);
        var worstParts = new List<string>(worstCount);
        for (int i = 0; i < worstCount; i++)
        {
            FrameSample sample = Frames[worst[i]];
            worstParts.Add($"{sample.Fps:0}fps/{sample.DtMs:0.0}ms");
        }

        Marks.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        int markCount = Math.Min(8, Marks.Count);
        var markParts = new List<string>(markCount);
        for (int i = 0; i < markCount; i++)
        {
            markParts.Add($"{Marks[i].Name}:{Marks[i].Ms:0.0}");
        }

        float avg = (float)(fpsSum / _frameCount);
        string marks = markParts.Count == 0 ? "-" : string.Join(" ", markParts);
        DiagnosticsLog.Info(
            "Hitch",
            $"summary {_reason} frames={_frameCount} min={minFps:0} avg={avg:0} p95={p95:0} max={maxFps:0} " +
            $"worst=[{string.Join(" ", worstParts)}] marks=[{marks}] party={_partyCount}");
    }

    private static double TicksToMs(long ticks) =>
        ticks * 1000.0 / Stopwatch.Frequency;

    private readonly record struct FrameSample(float Fps, float DtMs, float UpdateMs, float DrawMs);

    private readonly record struct MarkSample(string Name, float Ms);
}
