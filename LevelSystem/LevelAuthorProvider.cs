#nullable enable
using System;

namespace ColorBlocks;

public static class LevelAuthorProvider
{
    public static Func<string> ResolveLocalAuthor { get; set; } = () => Environment.UserName;

    public static string GetLocalAuthor()
    {
        try
        {
            string author = ResolveLocalAuthor();
            return string.IsNullOrWhiteSpace(author) ? "Player" : author.Trim();
        }
        catch
        {
            return "Player";
        }
    }

    public static string GetAuthorForSource(LevelSource source, LevelData? data = null)
    {
        if (data is not null && !string.IsNullOrWhiteSpace(data.Author))
        {
            return data.Author.Trim();
        }

        return source switch
        {
            LevelSource.Official => "Game",
            LevelSource.Workshop => "Steam",
            _ => GetLocalAuthor()
        };
    }
}
