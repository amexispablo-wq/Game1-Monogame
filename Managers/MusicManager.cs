#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Media;

namespace ColorBlocks;

/// <summary>
/// Menu/game and level-editor BGM playlists (folder-scanned, shuffled).
/// DesktopGL MediaPlayer breaks after Stop/Pause then Play (scratched restart).
/// Level silence = Volume 0 on the same stream. Track changes = Play(next) only, never Stop/Pause.
/// Playlist tracks: IsRepeating=false; advance on Stopped or estimated end (never both auto-repeat + Play).
/// Level tracks: IsRepeating=true (single looped song, no playlist timer).
/// </summary>
public sealed class MusicManager
{
    public const string MenuMusicId = "MainMenu";
    public const string EditorMusicId = "levelEditor";

    private const float MinTrustedDurationSeconds = 90f;
    private const string MusicRootRelative = "Audio/Music";
    private const string EditorFolderName = "level editor";

    private readonly List<string> _menuTrackIds = new();
    private readonly List<string> _editorTrackIds = new();
    private readonly List<string> _menuBag = new();
    private readonly List<string> _editorBag = new();
    private readonly Dictionary<string, Song> _songCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _trackAssetPaths = new(StringComparer.Ordinal);
    private readonly Random _random = new();
    private string? _currentMusicId;
    private bool _menuPlaylistActive;
    private bool _editorPlaylistActive;
    private bool _menuAudibleSuppressed;
    private float _playlistTrackPlayingSeconds;
    private float _playlistTrackDurationSeconds;
    private bool _loggedBadDuration;
    private bool _catalogLoaded;

    public float Volume { get; private set; } = 0.75f;
    public bool IsPlaying => MediaPlayer.State == MediaState.Playing;
    public string? CurrentMusicId => _currentMusicId;
    public bool IsMenuPlaylistActive => _menuPlaylistActive;

    public void ApplyVolume(float volume)
    {
        Volume = Math.Clamp(volume, 0f, 1f);
        ApplyEffectiveVolume();
    }

    public void Update(float dt)
    {
        if ((!_menuPlaylistActive && !_editorPlaylistActive) || _currentMusicId is null || dt <= 0f)
        {
            return;
        }

        // Count Playing time only (incl. muted-in-level). Never use PlayPosition.
        if (MediaPlayer.State == MediaState.Playing)
        {
            _playlistTrackPlayingSeconds += dt;
        }

        // Playlist tracks use IsRepeating=false. Advance on natural Stopped or near estimated end.
        // IsRepeating=true + Play(next) raced: song ended → auto-restart → timer Play → cut,
        // and DesktopGL MediaPlayer then scratched on menu return.
        bool trustedPlayback = _playlistTrackDurationSeconds >= MinTrustedDurationSeconds
            && _playlistTrackPlayingSeconds >= MinTrustedDurationSeconds;
        bool nearEstimatedEnd = trustedPlayback
            && _playlistTrackPlayingSeconds >= _playlistTrackDurationSeconds - 0.35f;
        bool naturallyEnded = MediaPlayer.State == MediaState.Stopped
            && _playlistTrackPlayingSeconds >= MinTrustedDurationSeconds;

        if (!nearEstimatedEnd && !naturallyEnded)
        {
            return;
        }

        // Switch with Play(next) only — no Stop/Pause.
        if (_menuPlaylistActive)
        {
            PlayNextMenuTrack();
        }
        else if (_editorPlaylistActive)
        {
            PlayNextEditorTrack();
        }
    }

    public void PlayMenuMusic()
    {
        EnsureCatalog();
        bool leavingEditor = _editorPlaylistActive;
        _menuAudibleSuppressed = false;
        _editorPlaylistActive = false;
        ApplyEffectiveVolume();

        // Same unbroken stream only when already on a real menu track.
        // Editor theme id can collide with stale root copies — never keep that as "menu".
        bool alreadyOnMenuStream = !leavingEditor
            && _menuPlaylistActive
            && _currentMusicId is not null
            && MediaPlayer.State == MediaState.Playing
            && _menuTrackIds.Contains(_currentMusicId);

        if (alreadyOnMenuStream)
        {
            return;
        }

        _menuPlaylistActive = true;
        PlayNextMenuTrack();
    }

    public void PlayEditorMusic()
    {
        EnsureCatalog();
        _menuAudibleSuppressed = false;
        ApplyEffectiveVolume();

        // EditorScene re-entry (e.g. after test play): keep stream.
        if (_editorPlaylistActive
            && _currentMusicId is not null
            && MediaPlayer.State == MediaState.Playing
            && _editorTrackIds.Contains(_currentMusicId))
        {
            _menuPlaylistActive = false;
            return;
        }

        _menuPlaylistActive = false;
        _editorPlaylistActive = true;
        _playlistTrackPlayingSeconds = 0f;
        _playlistTrackDurationSeconds = 0f;
        PlayNextEditorTrack();
    }

    public void PlayLevelMusic(string musicId)
    {
        if (string.IsNullOrWhiteSpace(musicId))
        {
            musicId = LevelMusicLibrary.DefaultMusicId;
        }

        if (_menuPlaylistActive && _currentMusicId is not null && MediaPlayer.State == MediaState.Playing)
        {
            if (TryLoadSongOnly(musicId, $"Music/{musicId}", out Song? levelSong) && levelSong is not null)
            {
                _menuPlaylistActive = false;
                _editorPlaylistActive = false;
                _menuAudibleSuppressed = false;
                _playlistTrackPlayingSeconds = 0f;
                _playlistTrackDurationSeconds = 0f;
                StartSong(musicId, levelSong, $"Music/{musicId}", repeating: true, trackPlaylistDuration: false);
                return;
            }

            _menuAudibleSuppressed = true;
            ApplyEffectiveVolume();
            return;
        }

        _menuPlaylistActive = false;
        _editorPlaylistActive = false;
        _menuAudibleSuppressed = false;
        _playlistTrackPlayingSeconds = 0f;
        _playlistTrackDurationSeconds = 0f;
        if (!PlayTrack(musicId, $"Music/{musicId}", repeating: true, trackPlaylistDuration: false))
        {
            MuteOnly();
        }
    }

    public void KeepMenuMusicThroughGameplay()
    {
        _menuAudibleSuppressed = false;
        ApplyEffectiveVolume();

        if (!_menuPlaylistActive)
        {
            PlayMenuMusic();
        }
    }

    public void Stop()
    {
        _menuPlaylistActive = false;
        _editorPlaylistActive = false;
        _menuAudibleSuppressed = true;
        ApplyEffectiveVolume();
    }

    private void MuteOnly()
    {
        _menuAudibleSuppressed = true;
        _currentMusicId = null;
        ApplyEffectiveVolume();
    }

    private void ApplyEffectiveVolume()
    {
        MediaPlayer.Volume = _menuAudibleSuppressed ? 0f : Volume;
    }

    private void EnsureCatalog()
    {
        if (_catalogLoaded)
        {
            return;
        }

        _catalogLoaded = true;
        _menuTrackIds.Clear();
        _editorTrackIds.Clear();
        _trackAssetPaths.Clear();

        string? musicRoot = ResolveMusicRootDirectory();
        if (musicRoot is null)
        {
            Console.WriteLine("Music catalog: Audio/Music folder not found.");
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(musicRoot, "*.ogg", SearchOption.TopDirectoryOnly))
        {
            RegisterTrack(filePath, MusicRootRelative, _menuTrackIds);
        }

        string? editorDir = FindEditorMusicDirectory(musicRoot);
        if (editorDir is not null)
        {
            string editorRelative = MusicRootRelative + "/" + Path.GetFileName(editorDir);
            foreach (string filePath in Directory.EnumerateFiles(editorDir, "*.ogg", SearchOption.TopDirectoryOnly))
            {
                // Editor folder owns the id → path map (overwrite stale root copies).
                RegisterTrack(filePath, editorRelative, _editorTrackIds, overwritePath: true);
            }
        }

        // Stale root copies (e.g. bin/.../levelEditor.ogg) must not sit in menu bag.
        for (int i = _menuTrackIds.Count - 1; i >= 0; i--)
        {
            if (_editorTrackIds.Contains(_menuTrackIds[i]))
            {
                _menuTrackIds.RemoveAt(i);
            }
        }

        Console.WriteLine(
            $"Music catalog: {_menuTrackIds.Count} menu track(s), {_editorTrackIds.Count} editor track(s).");
    }

    private void RegisterTrack(string filePath, string relativeFolder, List<string> trackIds, bool overwritePath = false)
    {
        string id = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        string assetPath = relativeFolder.Replace('\\', '/') + "/" + id;
        if (overwritePath || !_trackAssetPaths.ContainsKey(id))
        {
            _trackAssetPaths[id] = assetPath;
        }

        if (!trackIds.Contains(id))
        {
            trackIds.Add(id);
        }
    }

    private static string? ResolveMusicRootDirectory()
    {
        string? fileHint = ContentResolver.TryResolveContentFilePath(MusicRootRelative + "/.keep");
        // Prefer resolving any known ogg, else probe directories.
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

        _ = fileHint;
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

    private string ResolveAssetPath(string musicId, string fallbackPath)
    {
        if (_trackAssetPaths.TryGetValue(musicId, out string? mapped))
        {
            return mapped;
        }

        return fallbackPath;
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
                MuteOnly();
                return;
            }

            string musicId = _menuBag[^1];
            _menuBag.RemoveAt(_menuBag.Count - 1);
            string assetPath = ResolveAssetPath(musicId, $"{MusicRootRelative}/{musicId}");
            // Playlist: never IsRepeating — manual Play(next) owns the loop.
            if (PlayTrack(musicId, assetPath, repeating: false, trackPlaylistDuration: true))
            {
                return;
            }
        }

        MuteOnly();
    }

    private void PlayNextEditorTrack()
    {
        EnsureCatalog();
        const int maxAttempts = 16;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (_editorBag.Count == 0)
            {
                RefillBag(_editorBag, _editorTrackIds);
            }

            if (_editorBag.Count == 0)
            {
                MuteOnly();
                return;
            }

            string musicId = _editorBag[^1];
            _editorBag.RemoveAt(_editorBag.Count - 1);

            // Folder path first; root fallback for publish/bin layouts + Content.Load space issues.
            string folderPath = $"{MusicRootRelative}/{EditorFolderName}/{musicId}";
            string rootPath = $"{MusicRootRelative}/{musicId}";
            if (PlayTrack(musicId, folderPath, repeating: false, trackPlaylistDuration: true)
                || PlayTrack(musicId, rootPath, repeating: false, trackPlaylistDuration: true))
            {
                return;
            }
        }

        MuteOnly();
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

    private bool TryLoadSongOnly(string musicId, string assetPath, out Song? song)
    {
        song = null;
        // Cache by asset path — same filename in menu root vs editor folder must not share Song.
        string cacheKey = assetPath.Replace('\\', '/');
        if (_songCache.TryGetValue(cacheKey, out song))
        {
            return true;
        }

        try
        {
            song = ContentResolver.TryLoadSong(assetPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Music unavailable for '{assetPath}': {ex.Message}");
            return false;
        }

        if (song is null)
        {
            return false;
        }

        _songCache[cacheKey] = song;
        return true;
    }

    private bool PlayTrack(string musicId, string assetPath, bool repeating, bool trackPlaylistDuration = true)
    {
        if (!TryLoadSongOnly(musicId, assetPath, out Song? song) || song is null)
        {
            return false;
        }

        return StartSong(musicId, song, assetPath, repeating, trackPlaylistDuration);
    }

    private bool StartSong(string musicId, Song song, string assetPath, bool repeating, bool trackPlaylistDuration)
    {
        try
        {
            // Never Stop/Pause — Play replaces current stream.
            MediaPlayer.IsRepeating = repeating;
            ApplyEffectiveVolume();
            MediaPlayer.Play(song);
            _currentMusicId = musicId;
            _playlistTrackPlayingSeconds = 0f;
            _playlistTrackDurationSeconds = trackPlaylistDuration
                ? ResolveTrackDurationSeconds(song, assetPath)
                : 0f;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Music play failed for '{musicId}': {ex.Message}");
            _currentMusicId = null;
            _playlistTrackDurationSeconds = 0f;
            return false;
        }
    }

    private float ResolveTrackDurationSeconds(Song song, string assetPath)
    {
        float reported = (float)song.Duration.TotalSeconds;
        if (reported >= MinTrustedDurationSeconds)
        {
            return reported;
        }

        float estimated = EstimateDurationFromOggFile(assetPath);
        if (estimated >= MinTrustedDurationSeconds)
        {
            if (!_loggedBadDuration)
            {
                _loggedBadDuration = true;
                Console.WriteLine(
                    $"Music Song.Duration unreliable ({reported:0.##}s); using file estimate {estimated:0.#}s");
            }

            return estimated;
        }

        if (!_loggedBadDuration)
        {
            _loggedBadDuration = true;
            Console.WriteLine(
                $"Music duration unknown ({reported:0.##}s); using 3min fallback for playlist advance");
        }

        return 180f;
    }

    private static float EstimateDurationFromOggFile(string assetPath)
    {
        string relative = assetPath.Replace('\\', '/');
        if (!relative.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            relative += ".ogg";
        }

        string? fullPath = ContentResolver.TryResolveContentFilePath(relative);
        if (fullPath is null)
        {
            return 0f;
        }

        try
        {
            long bytes = new FileInfo(fullPath).Length;
            float seconds = bytes * 8f / 128_000f;
            return Math.Clamp(seconds, MinTrustedDurationSeconds, 20f * 60f);
        }
        catch
        {
            return 0f;
        }
    }
}
