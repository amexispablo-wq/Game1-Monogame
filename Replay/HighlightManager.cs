#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorBlocks.Replay;

public static class HighlightManager
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = false,
    PropertyNameCaseInsensitive = true
  };

  private static HighlightReplayFile? _cached;
  private static int _version;

  static HighlightManager()
  {
    JsonOptions.Converters.Add(new JsonStringEnumConverter());
  }

  public static int Version => _version;
  public static bool HasHighlights
  {
    get
    {
      HighlightReplayFile file;
      return TryGetHighlightReplay(out file) && file.Clips.Length > 0;
    }
  }

  public static bool TryGetHighlightReplay(out HighlightReplayFile file)
  {
    file = _cached ?? LoadFromDisk() ?? new HighlightReplayFile();
    _cached = file;
    return file.Clips.Length > 0;
  }

  public static void ProcessSession(ReplayData session)
  {
    if (session.Frames.Length == 0)
    {
      return;
    }

    var detector = new HighlightEventDetector();
    List<HighlightEvent> events = detector.AnalyzeSession(session);
    List<ReplayClip> clips = HighlightClipBuilder.BuildClips(session, events);
    if (clips.Count == 0)
    {
      return;
    }

    HighlightReplayFile current = LoadFromDisk() ?? new HighlightReplayFile();
    var merged = new List<HighlightClipEntry>(current.Clips);

    foreach (ReplayClip clip in clips)
    {
      merged.Add(new HighlightClipEntry
      {
        Type = clip.Type,
        Score = clip.Score,
        DurationSeconds = clip.DurationSeconds,
        LevelId = clip.LevelId,
        StartFrameIndex = clip.StartFrameIndex,
        EndFrameIndex = clip.EndFrameIndex,
        ClipData = HighlightClipBuilder.ExtractClipData(session, clip)
      });
    }

    merged = merged
      .OrderByDescending(entry => entry.Score)
      .GroupBy(entry => $"{entry.LevelId}:{entry.StartFrameIndex / 120}")
      .Select(group => group.First())
      .Take(ReplayConstants.MaxHighlightClips)
      .ToList();

    var updated = new HighlightReplayFile
    {
      FormatVersion = HighlightReplayFile.CurrentFormatVersion,
      Clips = merged.ToArray()
    };

    SaveToDisk(updated);
    _cached = updated;
    _version++;
    ReplayManager.NotifyHighlightsChanged();
  }

  public static void InvalidateClipsForLevel(string levelId)
  {
    HighlightReplayFile? current = LoadFromDisk();
    if (current is null || current.Clips.Length == 0)
    {
      return;
    }

    HighlightClipEntry[] remaining = current.Clips
      .Where(clip => !string.Equals(clip.LevelId, levelId, StringComparison.OrdinalIgnoreCase))
      .ToArray();

    if (remaining.Length == current.Clips.Length)
    {
      return;
    }

    var updated = new HighlightReplayFile
    {
      FormatVersion = HighlightReplayFile.CurrentFormatVersion,
      Clips = remaining
    };

    SaveToDisk(updated);
    _cached = updated;
    _version++;
    ReplayManager.NotifyHighlightsChanged();
  }

  private static HighlightReplayFile? LoadFromDisk()
  {
    string path = ReplayStorage.GetHighlightsPath();
    if (!File.Exists(path))
    {
      return null;
    }

    try
    {
      string json = File.ReadAllText(path);
      return JsonSerializer.Deserialize<HighlightReplayFile>(json, JsonOptions);
    }
    catch
    {
      return null;
    }
  }

  private static void SaveToDisk(HighlightReplayFile file)
  {
    string path = ReplayStorage.GetHighlightsPath();
    string? directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    string json = JsonSerializer.Serialize(file, JsonOptions);
    File.WriteAllText(path, json);
  }
}
