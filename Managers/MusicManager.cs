#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Media;

namespace ColorBlocks;

/// <summary>
/// Simple BGM:
/// - Menu shuffle playlist runs in menus (and in gameplay if "continue menu music" is on).
/// - Gameplay with option off = same playlist, volume 0 (never Stop — DesktopGL scratches).
/// - Level editor is the ONLY place that leaves the menu playlist; leaving editor restarts it.
/// Fresh Song every Play — reusing an EOF Song scratches the first seconds.
/// </summary>
public sealed class MusicManager
{
    public const string MenuMusicId = "MainMenu";
    public const string EditorMusicId = "levelEditor";

    private const float MinAdvanceElapsedSeconds = 2f;
    private const float EndAdvanceLeadSeconds = 0.75f;
    private const float AdvanceCooldownSeconds = 0.3f;
    private const string MusicRootRelative = "Audio/Music";
    private const string EditorFolderName = "level editor";

    private enum Mode
    {
        Menu,
        Editor
    }

    private readonly List<string> _menuTrackIds = new();
    private readonly List<string> _menuBag = new();
    private readonly Dictionary<string, string> _trackAssetPaths = new(StringComparer.Ordinal);
    private readonly Random _random = new();

    private Mode _mode = Mode.Menu;
    private Song? _currentSong;
    private string? _currentMusicId;
    private string? _editorAssetPath;
    private bool _audible = true;
    private float _trackElapsed;
    private float _trackDuration;
    private float _advanceCooldown;
    private bool _catalogLoaded;

    public float Volume { get; private set; } = 0.75f;
    public bool IsPlaying => MediaPlayer.State == MediaState.Playing;
    public string? CurrentMusicId => _currentMusicId;
    /// <summary>True while menu shuffle owns the stream (even if muted in gameplay).</summary>
    public bool IsMenuPlaylistActive => _mode == Mode.Menu;

    public void ApplyVolume(float volume)
    {
        Volume = Math.Clamp(volume, 0f, 1f);
        ApplyEffectiveVolume();
    }

    public void Update(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        if (_advanceCooldown > 0f)
        {
            _advanceCooldown = Math.Max(0f, _advanceCooldown - dt);
        }

        if (_currentMusicId is null)
        {
            return;
        }

        if (MediaPlayer.State == MediaState.Playing)
        {
            _trackElapsed += dt;
        }

        bool nearEnd = _trackDuration > MinAdvanceElapsedSeconds
            && MediaPlayer.State == MediaState.Playing
            && _trackElapsed >= MinAdvanceElapsedSeconds
            && _trackElapsed >= _trackDuration - EndAdvanceLeadSeconds;
        bool stopped = MediaPlayer.State == MediaState.Stopped
            && _trackElapsed >= MinAdvanceElapsedSeconds;

        if ((!nearEnd && !stopped) || _advanceCooldown > 0f)
        {
            return;
        }

        _advanceCooldown = AdvanceCooldownSeconds;

        if (_mode == Mode.Menu)
        {
            PlayNextMenuTrack();
        }
        else
        {
            // Single editor loop via fresh Play (IsRepeating is unreliable after EOF on DesktopGL).
            PlayEditorTrack();
        }
    }

    /// <summary>Menus: audible menu shuffle. Restarts only if not already playing a menu track.</summary>
    public void PlayMenuMusic()
    {
        _audible = true;
        ApplyEffectiveVolume();

        if (_mode == Mode.Menu
            && _currentMusicId is not null
            && MediaPlayer.State == MediaState.Playing
            && IsMenuTrack(_currentMusicId))
        {
            return;
        }

        _mode = Mode.Menu;
        PlayNextMenuTrack();
    }

    /// <summary>Gameplay with continue-menu-music: keep shuffle, volume on.</summary>
    public void KeepMenuMusicThroughGameplay()
    {
        _audible = true;
        _mode = Mode.Menu;
        ApplyEffectiveVolume();

        if (_currentMusicId is null || MediaPlayer.State != MediaState.Playing || !IsMenuTrack(_currentMusicId))
        {
            PlayNextMenuTrack();
        }
    }

    /// <summary>
    /// Gameplay without continue-menu-music (and sandbox/replay): mute only.
    /// Playlist keeps running so return to menu never restarts a dead/EOF Song.
    /// </summary>
    public void MuteMenuMusic()
    {
        _audible = false;
        _mode = Mode.Menu;
        ApplyEffectiveVolume();

        if (_currentMusicId is null || MediaPlayer.State != MediaState.Playing || !IsMenuTrack(_currentMusicId))
        {
            PlayNextMenuTrack();
        }
    }

    /// <summary>Legacy name — level BGM is menu shuffle (muted or not), not per-level tracks.</summary>
    public void PlayLevelMusic(string musicId) => MuteMenuMusic();

    /// <summary>Only interruption of the menu playlist.</summary>
    public void PlayEditorMusic()
    {
        EnsureCatalog();
        _audible = true;
        _mode = Mode.Editor;
        ApplyEffectiveVolume();
        PlayEditorTrack();
    }

    /// <summary>Mute helper used by sandbox/replay — same as MuteMenuMusic.</summary>
    public void Stop() => MuteMenuMusic();

    private void ApplyEffectiveVolume()
    {
        MediaPlayer.IsMuted = false;
        MediaPlayer.Volume = _audible ? Volume : 0f;
    }

    private bool IsMenuTrack(string musicId) => _menuTrackIds.Contains(musicId);

    private void EnsureCatalog()
    {
        if (_catalogLoaded)
        {
            return;
        }

        _catalogLoaded = true;
        _menuTrackIds.Clear();
        _trackAssetPaths.Clear();
        _editorAssetPath = null;

        string? musicRoot = ResolveMusicRootDirectory();
        if (musicRoot is null)
        {
            DiagnosticsLog.Info("Music", "Audio/Music folder not found - BGM silent");
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(musicRoot, "*.ogg", SearchOption.TopDirectoryOnly))
        {
            string id = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            // Root levelEditor.ogg is a stale publish leftover — editor track lives in subfolder.
            if (string.Equals(id, EditorMusicId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _trackAssetPaths[id] = $"{MusicRootRelative}/{id}";
            _menuTrackIds.Add(id);
        }

        string? editorDir = FindEditorMusicDirectory(musicRoot);
        if (editorDir is not null)
        {
            foreach (string filePath in Directory.EnumerateFiles(editorDir, "*.ogg", SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                _editorAssetPath = $"{MusicRootRelative}/{EditorFolderName}/{id}";
                break;
            }
        }

        _editorAssetPath ??= $"{MusicRootRelative}/{EditorFolderName}/{EditorMusicId}";

        DiagnosticsLog.Info(
            "Music",
            $"Catalog menu={_menuTrackIds.Count} editor='{_editorAssetPath}' root='{musicRoot}'");
    }

    private void PlayNextMenuTrack()
    {
        EnsureCatalog();
        const int maxAttempts = 16;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_menuBag.Count == 0)
            {
                RefillBag(_menuBag, _menuTrackIds);
            }

            if (_menuBag.Count == 0)
            {
                _currentMusicId = null;
                return;
            }

            string musicId = _menuBag[^1];
            _menuBag.RemoveAt(_menuBag.Count - 1);
            string assetPath = _trackAssetPaths.TryGetValue(musicId, out string? mapped)
                ? mapped
                : $"{MusicRootRelative}/{musicId}";

            if (PlayFresh(musicId, assetPath, trackDuration: true))
            {
                return;
            }
        }

        _currentMusicId = null;
    }

    private void PlayEditorTrack()
    {
        EnsureCatalog();
        string assetPath = _editorAssetPath ?? $"{MusicRootRelative}/{EditorFolderName}/{EditorMusicId}";
        if (!PlayFresh(EditorMusicId, assetPath, trackDuration: true))
        {
            // Fallback root copy from old publishes.
            PlayFresh(EditorMusicId, $"{MusicRootRelative}/{EditorMusicId}", trackDuration: true);
        }
    }

    private void RefillBag(List<string> bag, List<string> sourceIds)
    {
        bag.Clear();
        bag.AddRange(sourceIds);
        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }
    }

    private bool PlayFresh(string musicId, string assetPath, bool trackDuration)
    {
        Song? song;
        try
        {
            song = ContentResolver.TryLoadSong(assetPath);
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Music", $"Load throw '{assetPath}': {ex.Message}");
            return false;
        }

        if (song is null)
        {
            DiagnosticsLog.Info("Music", $"Load null '{assetPath}'");
            return false;
        }

        try
        {
            MediaPlayer.IsRepeating = false;
            ApplyEffectiveVolume();
            MediaPlayer.Play(song);

            DisposeCurrentSong();
            _currentSong = song;
            _currentMusicId = musicId;
            _trackElapsed = 0f;
            _trackDuration = trackDuration ? ResolveDuration(song, assetPath) : 0f;

            DiagnosticsLog.Info(
                "Music",
                $"Play id='{musicId}' mode={_mode} audible={_audible} state={MediaPlayer.State} vol={MediaPlayer.Volume:0.##} dur={_trackDuration:0.#}s");

            return MediaPlayer.State == MediaState.Playing;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Music", $"Play failed '{musicId}': {ex.Message}");
            try { song.Dispose(); } catch { /* ignore */ }
            return false;
        }
    }

    private void DisposeCurrentSong()
    {
        if (_currentSong is null)
        {
            return;
        }

        Song old = _currentSong;
        _currentSong = null;
        try { old.Dispose(); }
        catch { /* DesktopGL teardown race */ }
    }

    private static float ResolveDuration(Song song, string assetPath)
    {
        float reported = (float)song.Duration.TotalSeconds;
        if (reported >= MinAdvanceElapsedSeconds)
        {
            return reported;
        }

        string relative = assetPath.Replace('\\', '/');
        if (!relative.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            relative += ".ogg";
        }

        string? fullPath = ContentResolver.TryResolveContentFilePath(relative);
        if (fullPath is null)
        {
            return 180f;
        }

        try
        {
            long bytes = new FileInfo(fullPath).Length;
            return Math.Clamp(bytes * 8f / 160_000f, MinAdvanceElapsedSeconds, 20f * 60f);
        }
        catch
        {
            return 180f;
        }
    }

    private static string? ResolveMusicRootDirectory()
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "Content", MusicRootRelative),
            Path.Combine(AppContext.BaseDirectory, MusicRootRelative),
            Path.Combine(Directory.GetCurrentDirectory(), "Content", MusicRootRelative),
            Path.GetFullPath(Path.Combine("Content", MusicRootRelative))
        };

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindEditorMusicDirectory(string musicRoot)
    {
        foreach (string dir in Directory.EnumerateDirectories(musicRoot))
        {
            if (string.Equals(Path.GetFileName(dir), EditorFolderName, StringComparison.OrdinalIgnoreCase))
            {
                return dir;
            }
        }

        return null;
    }
}
