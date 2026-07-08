#nullable enable
using System;

namespace ColorBlocks;

public sealed class LevelMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public LevelSource Source { get; set; } = LevelSource.Local;
    public string Author { get; set; } = string.Empty;
    public string WorkshopId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public int Version { get; set; } = 1;
    public string OwnerSteamId { get; set; } = string.Empty;
    public string DownloadedVersion { get; set; } = string.Empty;
    public DateTime? LastSync { get; set; }

    public bool IsReadOnly
    {
        get
        {
            if (DeveloperSettings.DeveloperMode)
            {
                return false;
            }

            return Source is LevelSource.Official or LevelSource.Workshop;
        }
    }

    public string SourceIcon => LevelSourceVisuals.GetIcon(Source);

    public LevelMetadata()
    {
    }

    public LevelMetadata(string id, string name, string filePath, LevelSource source)
    {
        Id = id;
        Name = name;
        FilePath = filePath;
        Source = source;
    }

    public override string ToString() => $"{Name} ({Id})";
}
