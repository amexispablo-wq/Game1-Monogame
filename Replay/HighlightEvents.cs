#nullable enable
using System;
using System.Collections.Generic;

namespace ColorBlocks.Replay;

public sealed class HighlightTickContext
{
  public int FrameIndex { get; init; }
  public ReplayFrameSnapshot Frame { get; init; } = null!;
  public ReplayFrameSnapshot? PreviousFrame { get; init; }
  public ReplayHeader Header { get; init; } = null!;
}

public sealed class HighlightEvent
{
  public HighlightEvent(string type, int frameIndex, float score)
  {
    Type = type;
    FrameIndex = frameIndex;
    Score = score;
  }

  public string Type { get; }
  public int FrameIndex { get; }
  public float Score { get; }
}

public interface IHighlightEventRule
{
  string EventType { get; }
  void Evaluate(HighlightTickContext context, ICollection<HighlightEvent> events);
}

internal sealed class CheckpointReachedRule : IHighlightEventRule
{
  public string EventType => "Checkpoint";

  public void Evaluate(HighlightTickContext context, ICollection<HighlightEvent> events)
  {
    int? current = context.Frame.CurrentCheckpointId;
    int? previous = context.PreviousFrame?.CurrentCheckpointId;
    if (current.HasValue && current != previous)
    {
      events.Add(new HighlightEvent(EventType, context.FrameIndex, 72f));
    }
  }
}

internal sealed class LevelCompleteRule : IHighlightEventRule
{
  public string EventType => "LevelComplete";

  public void Evaluate(HighlightTickContext context, ICollection<HighlightEvent> events)
  {
    if (context.Frame.Timer.IsComplete
      && context.PreviousFrame is not null
      && !context.PreviousFrame.Timer.IsComplete)
    {
      events.Add(new HighlightEvent(EventType, context.FrameIndex, 95f));
    }
  }
}

internal static class HighlightFrameHelpers
{
  public static PlayerSnapshot? FindPlayer(ReplayFrameSnapshot frame, int networkId)
  {
    foreach (PlayerSnapshot player in frame.Players)
    {
      if (player.NetworkId == networkId)
      {
        return player;
      }
    }

    return null;
  }
}

internal sealed class LaunchPadRule : IHighlightEventRule
{
  public string EventType => "LaunchPad";

  public void Evaluate(HighlightTickContext context, ICollection<HighlightEvent> events)
  {
    if (context.PreviousFrame is null)
    {
      return;
    }

    foreach (PlayerSnapshot player in context.Frame.Players)
    {
      PlayerSnapshot? previous = HighlightFrameHelpers.FindPlayer(context.PreviousFrame, player.NetworkId);
      if (previous is null)
      {
        continue;
      }

      float verticalDelta = player.Velocity.Y - previous.Value.Velocity.Y;
      if (verticalDelta > 280f && player.Velocity.Y < -200f)
      {
        events.Add(new HighlightEvent(EventType, context.FrameIndex, 68f + MathF.Min(20f, MathF.Abs(player.Velocity.Y) / 40f)));
      }
    }
  }

}

internal sealed class ColorChangeRule : IHighlightEventRule
{
  public string EventType => "ColorChange";

  public void Evaluate(HighlightTickContext context, ICollection<HighlightEvent> events)
  {
    if (context.PreviousFrame is null)
    {
      return;
    }

    foreach (PlayerSnapshot player in context.Frame.Players)
    {
      PlayerSnapshot? previous = HighlightFrameHelpers.FindPlayer(context.PreviousFrame, player.NetworkId);
      if (previous is null || previous.Value.Color == player.Color)
      {
        continue;
      }

      events.Add(new HighlightEvent(EventType, context.FrameIndex, 58f));
    }
  }
}

internal sealed class LongFallRule : IHighlightEventRule
{
  public string EventType => "LongFall";

  public void Evaluate(HighlightTickContext context, ICollection<HighlightEvent> events)
  {
    if (context.PreviousFrame is null)
    {
      return;
    }

    foreach (PlayerSnapshot player in context.Frame.Players)
    {
      if (player.Velocity.Y <= 520f)
      {
        continue;
      }

      events.Add(new HighlightEvent(EventType, context.FrameIndex, 55f + MathF.Min(25f, player.Velocity.Y / 30f)));
      break;
    }
  }
}

internal sealed class FastMovementRule : IHighlightEventRule
{
  public string EventType => "FastMovement";

  public void Evaluate(HighlightTickContext context, ICollection<HighlightEvent> events)
  {
    foreach (PlayerSnapshot player in context.Frame.Players)
    {
      float speed = player.Velocity.ToVector2().Length();
      if (speed < 620f)
      {
        continue;
      }

      events.Add(new HighlightEvent(EventType, context.FrameIndex, 50f + MathF.Min(20f, speed / 40f)));
      break;
    }
  }
}
