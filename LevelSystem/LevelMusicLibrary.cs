using System.Collections.Generic;

namespace ColorBlocks;

public static class LevelMusicLibrary
{
    public static readonly IReadOnlyList<string> AvailableMusicIds = new List<string>
    {
        "default_track_01",
        "default_track_02"
    };

    public static string DefaultMusicId => AvailableMusicIds.Count > 0 ? AvailableMusicIds[0] : "default_track_01";

    public static string GetDisplayName(string musicId)
    {
        return musicId switch
        {
            "default_track_01" => "Default Track 1",
            "default_track_02" => "Default Track 2",
            _ => musicId
        };
    }
}
