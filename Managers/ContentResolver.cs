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

        // Raw .ogg first — Content.Load breaks on paths with spaces (level editor/).
        Song? fromFile = TryLoadSongFromFile(assetPath);
        if (fromFile is not null)
        {
            return fromFile;
        }

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
                // Fall through.
            }
        }

        return null;
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
            // Unique name per relative path — same filename in folder vs root must not collide.
            string name = StripExtension(relativePath).Replace('/', '_').Replace(' ', '_');
            string absolute = Path.GetFullPath(fullPath);

            // Song.FromUri passes uri.OriginalString into OggStream/NVorbis.
            // Absolute System.Uri becomes "file:///C:/..." which NVorbis cannot open.
            // Always use a path relative to BaseDirectory (cwd pinned in Program.cs).
            Uri uri = BuildSongUri(absolute);
            return Song.FromUri(name, uri);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Song file unavailable '{fullPath}': {ex.Message}");
            DiagnosticsLog.Info("Music", $"Song.FromUri failed '{fullPath}': {ex.Message}");
            return null;
        }
    }

    private static Uri BuildSongUri(string absolutePath)
    {
        string baseDir = Path.GetFullPath(AppContext.BaseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string full = Path.GetFullPath(absolutePath);

        string relative = Path.GetRelativePath(baseDir, full).Replace('\\', '/');
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            // Last resort: 8.3 path still becomes file:// via Absolute Uri — prefer copying
            // is out of scope; content must live under the install folder.
            throw new InvalidOperationException(
                $"Song path outside BaseDirectory: '{full}' (base='{baseDir}')");
        }

        return new Uri(relative, UriKind.Relative);
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

    /// <summary>Expose content file resolution for music duration fallback.</summary>
    public static string? TryResolveContentFilePath(string relativePath) => ResolveContentPath(relativePath);

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
