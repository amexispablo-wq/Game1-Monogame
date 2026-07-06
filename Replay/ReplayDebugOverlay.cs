#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks.Replay;

public static class ReplayDebugOverlay
{
  public static void Draw(
    SpriteBatch spriteBatch,
    Texture2D pixel,
    Viewport viewport)
  {
    if (!ReplayDiagnostics.DebugOverlayVisible)
    {
      return;
    }

    if (!ReplayManager.HasReplay() && ReplayDiagnostics.ActiveRecorder is null)
    {
      return;
    }

    ReplayRecorder? recorder = ReplayDiagnostics.ActiveRecorder;
    ReplayPlayer? player = ReplayDiagnostics.ActivePlayer;

    int margin = Math.Max(8, viewport.Width / 80);
    int scale = Math.Clamp(viewport.Height / 280, 1, 2);
    int lineHeight = SimpleTextRenderer.MeasureString("A", scale).Y + 2;
    int y = margin;

    void Line(string text)
    {
      SimpleTextRenderer.DrawString(spriteBatch, pixel, text, new Vector2(margin, y), scale, Color.Cyan);
      y += lineHeight;
    }

    Line("REPLAY DEBUG");
    Line($"Recording: {(recorder?.IsRecording == true ? "YES" : "NO")}");
    Line($"Playing: {(player?.IsPlaying == true && player.IsLoaded ? "YES" : "NO")}");

    if (recorder is not null)
    {
      Line($"Frames Recorded: {recorder.RecordedFrameCount}");
      Line($"Buffer Capacity: {recorder.BufferCapacity}");
      Line($"Buffer Fill: {recorder.BufferFillPercent:0.0}%");
      Line($"First Tick: {recorder.FirstRecordedTick}");
      Line($"Last Tick: {recorder.LastRecordedTick}");
    }

    if (player is not null && player.IsLoaded)
    {
      Line($"Current Replay Frame: {player.FrameIndex + 1}/{player.FrameCount}");
      Line($"Replay Length: {player.ReplayLengthSeconds:0.00}s");
      Line($"Loop Count: {player.LoopCount}");
      Line($"Current Simulation Tick: {player.CurrentSimulationTick}");
      Line($"Current Replay Tick: {player.FrameIndex}");
      Line($"Camera Target: {FormatVector(player.CurrentCameraTarget)}");
    }

    if (ReplayManager.HasReplay() && recorder is null)
    {
      ReplayData? replay = ReplayManager.GetReplay();
      if (replay is not null)
      {
        Line($"Stored Frames: {replay.Frames.Length}");
        Line($"Stored Length: {replay.Frames.Length / (float)Math.Max(1, replay.Header.TicksPerSecond):0.00}s");
      }
    }

    Line($"Menu Background: {(ReplayManager.MenuBackgroundEnabled ? "ON" : "OFF")}");
  }

  private static string FormatVector(Vector2 value) => $"{value.X:0},{value.Y:0}";
}
