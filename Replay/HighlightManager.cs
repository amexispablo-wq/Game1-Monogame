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

  private static readonly Dictionary<LevelSource, HighlightReplayFile> _cachedBySource = new();
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
    var merged = new List<HighlightClipEntry>();
    foreach (LevelSource source in Enum.GetValues<LevelSource>())
    {
      HighlightReplayFile? current = LoadFromDisk(source);
      if (current is null || current.Clips.Length == 0)
      {
        continue;
      }

      merged.AddRange(current.Clips);
    }

    merged = merged
      .OrderByDescending(entry => entry.Score)
      .Take(ReplayConstants.MaxHighlightClips)
      .ToList();

    file = new HighlightReplayFile
    {
      FormatVersion = HighlightReplayFile.CurrentFormatVersion,
      Clips = merged.ToArray()
    };

    return file.Clips.Length > 0;
  }

  public static void ProcessSession(ReplayData session)
  {
    if (session.Frames.Length == 0 || string.IsNullOrWhiteSpace(session.Header.LevelId))
    {
      return;
    }

    LevelSource source = LevelIdentity.GetSource(session.Header.LevelId);
    var detector = new HighlightEventDetector();
    List<HighlightEvent> events = detector.AnalyzeSession(session);
    List<ReplayClip> clips = HighlightClipBuilder.BuildClips(session, events);
    if (clips.Count == 0)
    {
      return;
    }

    HighlightReplayFile current = LoadFromDisk(source) ?? new HighlightReplayFile();
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

    SaveToDisk(source, updated);
    _cachedBySource[source] = updated;
    _version++;
    ReplayManager.NotifyHighlightsChanged();
  }

  public static void InvalidateClipsForLevel(string levelId)
  {
    LevelSource source = LevelIdentity.GetSource(levelId);
    HighlightReplayFile? current = LoadFromDisk(source);
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

    SaveToDisk(source, updated);
    _cachedBySource[source] = updated;
    _version++;
    ReplayManager.NotifyHighlightsChanged();
  }

  private static HighlightReplayFile? LoadFromDisk(LevelSource source)
  {
    if (_cachedBySource.TryGetValue(source, out HighlightReplayFile? cached))
    {
      return cached;
    }

    string path = ReplayStorage.GetHighlightsPath(source);
    if (!File.Exists(path))
    {
      return null;
    }

    try
    {
      string json = File.ReadAllText(path);
      HighlightReplayFile? file = JsonSerializer.Deserialize<HighlightReplayFile>(json, JsonOptions);
      if (file is not null)
      {
        _cachedBySource[source] = file;
      }

      return file;
    }
    catch
    {
      return null;
    }
  }

  private static void SaveToDisk(LevelSource source, HighlightReplayFile file)
  {
    string path = ReplayStorage.GetHighlightsPath(source);
    string? directory = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    string json = JsonSerializer.Serialize(file, JsonOptions);
    File.WriteAllText(path, json);
  }
}
