#nullable enable
using System;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace ColorBlocks;

internal static class ContentResolver
{
    private static Microsoft.Xna.Framework.Game? _game;

    public static void Bind(Microsoft.Xna.Framework.Game game)
    {
        _game = game;
    }

    public static Song? TryLoadSong(string assetPath)
    {
        assetPath = StripExtension(assetPath.Replace('\\', '/'));

        if (_game is not null)
        {
            try
            {
                return _game.Content.Load<Song>(assetPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Content song load failed '{assetPath}': {ex.Message}");
            }

            // Some publish layouts nest under an extra Content folder.
            try
            {
                return _game.Content.Load<Song>("Content/" + assetPath);
            }
            catch
            {
                // Fall through to raw file load.
            }
        }

        return TryLoadSongFromFile(assetPath);
    }

    public static SoundEffect? TryLoadSoundEffect(string relativePath)
    {
        // Prefer raw wav files — Content.mgcb currently only builds music songs.
        SoundEffect? fromFile = TryLoadSoundEffectFromFile(relativePath);
        if (fromFile is not null)
        {
            return fromFile;
        }

        if (_game is null)
        {
            return null;
        }

        try
        {
            string assetName = StripExtension(relativePath);
            return _game.Content.Load<SoundEffect>(assetName);
        }
        catch
        {
            return null;
        }
    }

    private static Song? TryLoadSongFromFile(string relativePath)
    {
        string? fullPath = ResolveContentPath(EnsureExtension(relativePath, ".ogg"));
        if (fullPath is null)
        {
            return null;
        }

        try
        {
            string name = Path.GetFileNameWithoutExtension(fullPath);
            return Song.FromUri(name, new Uri(fullPath));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Song file unavailable '{fullPath}': {ex.Message}");
            return null;
        }
    }

    private static SoundEffect? TryLoadSoundEffectFromFile(string relativePath)
    {
        string? fullPath = ResolveContentPath(EnsureExtension(relativePath, ".wav"));
        if (fullPath is null)
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(fullPath);
            return SoundEffect.FromStream(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SFX file unavailable '{fullPath}': {ex.Message}");
            return null;
        }
    }

    private static string? ResolveContentPath(string relativePath)
    {
        relativePath = relativePath.Replace('\\', '/').TrimStart('/');
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "Content", relativePath),
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), "Content", relativePath),
            Path.GetFullPath(Path.Combine("Content", relativePath))
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string StripExtension(string path)
    {
        path = path.Replace('\\', '/');
        int dot = path.LastIndexOf('.');
        int slash = path.LastIndexOf('/');
        return dot > slash ? path[..dot] : path;
    }

    private static string EnsureExtension(string path, string extension)
    {
        path = path.Replace('\\', '/');
        return path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? path
            : StripExtension(path) + extension;
    }
}
