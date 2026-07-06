#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorBlocks.Replay;

public sealed class HighlightEventDetector
{
  private readonly IHighlightEventRule[] _rules;

  public HighlightEventDetector()
  {
    _rules =
    [
      new CheckpointReachedRule(),
      new LevelCompleteRule(),
      new LaunchPadRule(),
      new ColorChangeRule(),
      new LongFallRule(),
      new FastMovementRule()
    ];
  }

  public List<HighlightEvent> AnalyzeSession(ReplayData session)
  {
    var events = new List<HighlightEvent>();
    for (int i = 0; i < session.Frames.Length; i++)
    {
      var context = new HighlightTickContext
      {
        FrameIndex = i,
        Frame = session.Frames[i],
        PreviousFrame = i > 0 ? session.Frames[i - 1] : null,
        Header = session.Header
      };

      foreach (IHighlightEventRule rule in _rules)
      {
        rule.Evaluate(context, events);
      }
    }

    return events;
  }
}

public static class HighlightClipBuilder
{
  public static List<ReplayClip> BuildClips(ReplayData session, IEnumerable<HighlightEvent> events)
  {
    int ticksPerSecond = Math.Max(1, session.Header.TicksPerSecond);
    int targetFrames = ReplayConstants.HighlightClipTargetSeconds * ticksPerSecond;
    int minFrames = ReplayConstants.HighlightClipMinSeconds * ticksPerSecond;
    int maxFrames = ReplayConstants.HighlightClipMaxSeconds * ticksPerSecond;
    int maxFrame = session.Frames.Length - 1;
    int minCenterDistance = Math.Max(targetFrames / 2, minFrames / 2);

    var clips = new List<ReplayClip>();
    var usedCenters = new List<int>();

    foreach (HighlightEvent highlightEvent in events.OrderByDescending(evt => evt.Score))
    {
      if (usedCenters.Any(center => Math.Abs(center - highlightEvent.FrameIndex) < minCenterDistance))
      {
        continue;
      }

      int half = targetFrames / 2;
      int start = Math.Max(0, highlightEvent.FrameIndex - half);
      int end = Math.Min(maxFrame, start + targetFrames);
      start = Math.Max(0, end - targetFrames);

      int length = end - start + 1;
      if (length < minFrames && maxFrame >= minFrames)
      {
        end = Math.Min(maxFrame, start + minFrames);
      }

      if (end - start + 1 > maxFrames)
      {
        end = start + maxFrames;
      }

      usedCenters.Add(highlightEvent.FrameIndex);
      clips.Add(new ReplayClip
      {
        Type = highlightEvent.Type,
        Score = highlightEvent.Score,
        StartFrameIndex = start,
        EndFrameIndex = end,
        DurationSeconds = (end - start + 1) / (float)ticksPerSecond,
        LevelId = session.Header.LevelId
      });
    }

    return clips;
  }

  public static ReplayData ExtractClipData(ReplayData session, ReplayClip clip)
  {
    int length = clip.EndFrameIndex - clip.StartFrameIndex + 1;
    var frames = new ReplayFrameSnapshot[length];
    Array.Copy(session.Frames, clip.StartFrameIndex, frames, 0, length);
    return new ReplayData
    {
      Header = session.Header,
      Frames = frames
    };
  }
}
