#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using NVorbis;

namespace ColorBlocks;

/// <summary>
/// Menu shuffle on MediaPlayer. DesktopGL scratches after Stop/EOF reuse — keep
/// IsRepeating=true and cut to next track by elapsed timer (never wait for Stopped at EOF).
/// Cut ~2.5s early so OggStreamer pendingFinish cannot replay the new track from 0.
/// Mute = IsMuted, playlist keeps advancing.
/// Editor: mute menu playlist + loop editor cue on a SoundEffectInstance (second channel).
/// Leaving editor: stop editor SFX + unmute menu — no MediaPlayer restart.
/// </summary>
public sealed class MusicManager
{
    public const string MenuMusicId = "MainMenu";
    public const string EditorMusicId = "levelEditor";

    private const float MinAdvanceElapsedSeconds = 8f;
    private const float AdvanceCooldownSeconds = 2.5f;
    private const float StuckStoppedSeconds = 0.4f;
    // OpenAL OggStreamer: 3 buffers × ~0.5s. Cut inside that window → stale Finished replays the new track.
    private const float EndCutSeconds = 2.5f;
    private const float StaleReplayWatchSeconds = 3f;
    private const float StaleReplayRewindSeconds = 0.2f;
    private const int MaxSameTrackRestarts = 1;
    private const int MaxPlayHistory = 32;
    private const string MusicRootRelative = "Audio/Music";
    private const string EditorFolderName = "level editor";

    private readonly List<string> _menuTrackIds = new();
    private readonly List<string> _menuBag = new();
    private readonly List<string> _playHistory = new();
    private readonly Dictionary<string, string> _trackAssetPaths = new(StringComparer.Ordinal);
    private readonly List<Song> _retiredSongs = new();
    private readonly Random _random = new();

    private Song? _currentSong;
    private string? _currentMusicId;
    private string? _editorAssetPath;
    private SoundEffect? _editorEffect;
    private SoundEffectInstance? _editorInstance;
    private bool _audible = true;
    private bool _inEditor;
    private float _trackElapsed;
    private float _stoppedElapsed;
    private float _trackDuration;
    private float _advanceCooldown;
    private float _staleReplayWatch;
    private float _lastPlayPosition = -1f;
    private bool _staleReplayLogged;
    private int _sameTrackRestarts;
    private bool _catalogLoaded;

    public float Volume { get; private set; } = 0.5f;
    public bool IsPlaying => MediaPlayer.State == MediaState.Playing;
    public string? CurrentMusicId => _currentMusicId;
    /// <summary>Menu shuffle owns MediaPlayer (even when muted / under editor).</summary>
    public bool IsMenuPlaylistActive => true;

    public void ApplyVolume(float volume)
    {
        Volume = Math.Clamp(volume, 0f, 1f);
        ApplyEffectiveVolume();
        ApplyEditorVolume();
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

        if (_staleReplayWatch > 0f)
        {
            _staleReplayWatch = Math.Max(0f, _staleReplayWatch - dt);
        }

        // Menu playlist always advances (muted under gameplay/editor is fine).
        if (_currentMusicId is null)
        {
            EnsureMenuPlaylistPlaying();
            return;
        }

        MediaState state = MediaPlayer.State;
        if (state == MediaState.Playing)
        {
            _trackElapsed += dt;
            _stoppedElapsed = 0f;
        }
        else if (state == MediaState.Stopped)
        {
            _stoppedElapsed += dt;
        }

        WatchStaleReplay(state);

        // Ignore Stopped / advance while post-Play cooldown is active (finished-callback race).
        if (_advanceCooldown > 0f)
        {
            return;
        }

        // Timer cut: IsRepeating keeps MediaPlayer out of EOF→Stopped (DesktopGL scratch).
        // Cut well before true end so OggStreamer last-buffer Finished cannot Play(new) from 0.
        float cutAt = _trackDuration >= MinAdvanceElapsedSeconds
            ? Math.Max(_trackDuration - EndCutSeconds, MinAdvanceElapsedSeconds)
            : float.MaxValue;
        if (_trackElapsed >= cutAt)
        {
            _advanceCooldown = AdvanceCooldownSeconds;
            DiagnosticsLog.Info(
                "Music",
                $"Advance next (timer elapsed={_trackElapsed:0.#}s dur={_trackDuration:0.#}s state={state})");
            PlayNextMenuTrack();
            return;
        }

        // Mid-track Stop (device hiccup / finished-callback killing new Play). Cap restarts
        // so PlayFresh cannot reset intro forever ("scratched disc" loop).
        if (state == MediaState.Stopped
            && _stoppedElapsed >= StuckStoppedSeconds
            && _currentMusicId is not null
            && IsMenuTrack(_currentMusicId))
        {
            if (_sameTrackRestarts < MaxSameTrackRestarts
                && TryRestartCurrentSong())
            {
                return;
            }

            DiagnosticsLog.Info(
                "Music",
                $"Early Stop → skip track (restarts={_sameTrackRestarts} elapsed={_trackElapsed:0.#}s)");
            _advanceCooldown = AdvanceCooldownSeconds;
            PlayNextMenuTrack();
        }
    }

    /// <summary>Menus: audible menu shuffle. Never restarts if already playing.</summary>
    public void PlayMenuMusic()
    {
        LeaveEditorLayer();
        _audible = true;
        ApplyEffectiveVolume();
        EnsureMenuPlaylistPlaying();
    }

    /// <summary>Gameplay with continue-menu-music: keep shuffle, volume on.</summary>
    public void KeepMenuMusicThroughGameplay()
    {
        LeaveEditorLayer();
        _audible = true;
        ApplyEffectiveVolume();
        EnsureMenuPlaylistPlaying();
    }

    /// <summary>Gameplay / sandbox / replay: mute only — playlist keeps running.</summary>
    public void MuteMenuMusic()
    {
        LeaveEditorLayer();
        _audible = false;
        ApplyEffectiveVolume();
        EnsureMenuPlaylistPlaying();
    }

    /// <summary>Legacy — level BGM is muted-or-audible menu shuffle.</summary>
    public void PlayLevelMusic(string musicId) => MuteMenuMusic();

    /// <summary>
    /// Editor: mute menu playlist (still running) + loop editor cue on SoundEffect.
    /// Leaving editor only unmutes — MediaPlayer never Stop/restart.
    /// </summary>
    public void PlayEditorMusic()
    {
        EnsureCatalog();
        _inEditor = true;
        _audible = false;
        ApplyEffectiveVolume();
        EnsureMenuPlaylistPlaying();
        StartEditorLayer();
    }

    public void Stop() => MuteMenuMusic();

    /// <summary>Force next shuffle track (menu boombox skip).</summary>
    public void SkipNextMenuTrack()
    {
        EnsureCatalog();
        if (_menuTrackIds.Count == 0)
        {
            return;
        }

        _advanceCooldown = AdvanceCooldownSeconds;
        _stoppedElapsed = 0f;
        DiagnosticsLog.Info("Music", "Skip next (manual)");
        PlayNextMenuTrack();
    }

    /// <summary>Force previous track from history (menu boombox skip).</summary>
    public void SkipPreviousMenuTrack()
    {
        EnsureCatalog();
        if (_playHistory.Count == 0)
        {
            DiagnosticsLog.Info("Music", "Skip previous — empty history");
            return;
        }

        string previousId = _playHistory[^1];
        _playHistory.RemoveAt(_playHistory.Count - 1);

        if (_currentMusicId is not null
            && !string.Equals(_currentMusicId, previousId, StringComparison.Ordinal))
        {
            _menuBag.Add(_currentMusicId);
        }

        string assetPath = _trackAssetPaths.TryGetValue(previousId, out string? mapped)
            ? mapped
            : $"{MusicRootRelative}/{previousId}";

        _advanceCooldown = AdvanceCooldownSeconds;
        _stoppedElapsed = 0f;
        DiagnosticsLog.Info("Music", $"Skip previous → '{previousId}'");
        if (!PlayFreshMenuTrack(previousId, assetPath, recordHistory: false))
        {
            PlayNextMenuTrack();
        }
    }

    private void EnsureMenuPlaylistPlaying()
    {
        if (_currentMusicId is null || !IsMenuTrack(_currentMusicId))
        {
            PlayNextMenuTrack();
            return;
        }

        MediaState state = MediaPlayer.State;
        if (state == MediaState.Playing)
        {
            return;
        }

        // Mute/volume/device hiccups can briefly Stop/Pause MediaPlayer. Do NOT treat that
        // as "need next track" — that restarts audio from 0 and sounds like intro loop.
        if (state == MediaState.Paused)
        {
            try
            {
                MediaPlayer.Resume();
            }
            catch
            {
                // ignore
            }

            if (MediaPlayer.State == MediaState.Playing)
            {
                return;
            }
        }

        if (_currentSong is not null && (_trackElapsed > 0f || _advanceCooldown > 0f))
        {
            // Let Update() natural-end / stuckPastEnd advance when appropriate.
            return;
        }

        PlayNextMenuTrack();
    }

    private void LeaveEditorLayer()
    {
        if (!_inEditor && _editorInstance is null)
        {
            return;
        }

        _inEditor = false;
        StopEditorLayer();
    }

    private void StartEditorLayer()
    {
        if (!TryEnsureEditorEffect())
        {
            DiagnosticsLog.Info("Music", "Editor cue missing — menu playlist stays muted only");
            return;
        }

        try
        {
            if (_editorInstance is null || _editorInstance.IsDisposed)
            {
                _editorInstance = _editorEffect!.CreateInstance();
                _editorInstance.IsLooped = true;
            }

            ApplyEditorVolume();
            if (_editorInstance.State != SoundState.Playing)
            {
                _editorInstance.Play();
            }

            DiagnosticsLog.Info("Music", "Editor cue playing (menu playlist muted, still running)");
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Music", $"Editor cue failed: {ex.Message}");
        }
    }

    private void StopEditorLayer()
    {
        if (_editorInstance is null)
        {
            return;
        }

        try
        {
            if (!_editorInstance.IsDisposed)
            {
                _editorInstance.Stop();
                _editorInstance.Dispose();
            }
        }
        catch
        {
            // ignore
        }

        _editorInstance = null;
    }

    private void ApplyEffectiveVolume()
    {
        // DesktopGL often ignores Volume=0 (still audible). Use IsMuted for real silence
        // when gameplay mute is on or the slider is at zero.
        bool wantAudible = _audible && Volume > 0.001f;
        MediaPlayer.IsMuted = !wantAudible;
        MediaPlayer.Volume = wantAudible ? Volume : 0f;
    }

    private void ApplyEditorVolume()
    {
        if (_editorInstance is null || _editorInstance.IsDisposed)
        {
            return;
        }

        _editorInstance.Volume = Math.Clamp(Volume, 0f, 1f);
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

    private bool TryEnsureEditorEffect()
    {
        if (_editorEffect is not null)
        {
            return true;
        }

        EnsureCatalog();
        string assetPath = _editorAssetPath ?? $"{MusicRootRelative}/{EditorFolderName}/{EditorMusicId}";
        string relative = assetPath.Replace('\\', '/');
        if (!relative.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            relative += ".ogg";
        }

        string? fullPath = ContentResolver.TryResolveContentFilePath(relative);
        if (fullPath is null)
        {
            fullPath = ContentResolver.TryResolveContentFilePath($"{MusicRootRelative}/{EditorMusicId}.ogg");
        }

        if (fullPath is null)
        {
            return false;
        }

        try
        {
            _editorEffect = LoadOggAsSoundEffect(fullPath);
            return _editorEffect is not null;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Music", $"Editor SoundEffect load failed: {ex.Message}");
            return false;
        }
    }

    private static SoundEffect? LoadOggAsSoundEffect(string fullPath)
    {
        using var reader = new VorbisReader(fullPath);
        int channels = reader.Channels;
        int sampleRate = reader.SampleRate;
        long totalSamples = reader.TotalSamples;
        if (channels <= 0 || sampleRate <= 0 || totalSamples <= 0)
        {
            return null;
        }

        // Cap ~12 min mono/stereo 44.1k to avoid huge RAM (editor loops are short).
        long maxSamples = sampleRate * channels * 60L * 12L;
        int floatCount = (int)Math.Min(totalSamples * channels, maxSamples);
        var floats = new float[floatCount];
        int read = reader.ReadSamples(floats, 0, floatCount);
        if (read <= 0)
        {
            return null;
        }

        var pcm = new byte[read * sizeof(short)];
        for (int i = 0; i < read; i++)
        {
            float sample = Math.Clamp(floats[i], -1f, 1f);
            short value = (short)(sample * short.MaxValue);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        var channel = channels == 1 ? AudioChannels.Mono : AudioChannels.Stereo;
        return new SoundEffect(pcm, sampleRate, channel);
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

            if (PlayFreshMenuTrack(musicId, assetPath, recordHistory: true))
            {
                return;
            }
        }

        _currentMusicId = null;
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

    private bool PlayFreshMenuTrack(string musicId, string assetPath, bool recordHistory = true)
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
            // Repeating avoids EOF→Stopped on DesktopGL (scratch after Stop/reuse).
            // Update() cuts to next track by elapsed before the repeat seam.
            MediaPlayer.IsRepeating = true;
            ApplyEffectiveVolume();
            MediaPlayer.Play(song);

            // Never Dispose playlist Songs — DesktopGL MediaPlayer shares OpenAL state;
            // Dispose(old) ~2s later was killing the new track (double-skip symptom).
            string? previousId = _currentMusicId;
            RetireCurrentSong();
            _currentSong = song;
            _currentMusicId = musicId;
            _trackElapsed = 0f;
            _stoppedElapsed = 0f;
            _sameTrackRestarts = 0;
            _trackDuration = ResolveDuration(song, assetPath);
            _advanceCooldown = AdvanceCooldownSeconds;
            ArmStaleReplayWatch();

            if (recordHistory
                && previousId is not null
                && !string.Equals(previousId, musicId, StringComparison.Ordinal))
            {
                _playHistory.Add(previousId);
                while (_playHistory.Count > MaxPlayHistory)
                {
                    _playHistory.RemoveAt(0);
                }
            }

            DiagnosticsLog.Info(
                "Music",
                $"Play id='{musicId}' audible={_audible} editor={_inEditor} state={MediaPlayer.State} vol={MediaPlayer.Volume:0.##} muted={MediaPlayer.IsMuted} dur={_trackDuration:0.#}s");

            return MediaPlayer.State == MediaState.Playing || MediaPlayer.State == MediaState.Paused;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Music", $"Play failed '{musicId}': {ex.Message}");
            try { song.Dispose(); } catch { /* ignore */ }
            return false;
        }
    }

    /// <summary>
    /// Re-Play the already-loaded Song (no FromUri). Resets audible position but keeps
    /// _trackElapsed so timer advance still fires and intro cannot loop forever.
    /// </summary>
    private bool TryRestartCurrentSong()
    {
        if (_currentSong is null || _currentMusicId is null)
        {
            return false;
        }

        try
        {
            MediaPlayer.IsRepeating = true;
            ApplyEffectiveVolume();
            MediaPlayer.Play(_currentSong);
            _sameTrackRestarts++;
            _stoppedElapsed = 0f;
            _advanceCooldown = AdvanceCooldownSeconds;
            ArmStaleReplayWatch();
            DiagnosticsLog.Info(
                "Music",
                $"Restart same Song (restarts={_sameTrackRestarts} wallElapsed={_trackElapsed:0.#}s id='{_currentMusicId}')");
            return MediaPlayer.State == MediaState.Playing || MediaPlayer.State == MediaState.Paused;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Music", $"Restart failed: {ex.Message}");
            return false;
        }
    }

    private void ArmStaleReplayWatch()
    {
        _staleReplayWatch = StaleReplayWatchSeconds;
        _lastPlayPosition = -1f;
        _staleReplayLogged = false;
    }

    /// <summary>
    /// MediaPlayer.IsRepeating + OggStreamer pendingFinish can Play(current) from 0 after a switch.
    /// Log only — do not Play again (that loops the glitch).
    /// </summary>
    private void WatchStaleReplay(MediaState state)
    {
        if (_staleReplayWatch <= 0f || state != MediaState.Playing)
        {
            return;
        }

        float pos;
        try
        {
            pos = (float)MediaPlayer.PlayPosition.TotalSeconds;
        }
        catch
        {
            return;
        }

        if (_lastPlayPosition < 0f)
        {
            _lastPlayPosition = pos;
            return;
        }

        if (!_staleReplayLogged && _lastPlayPosition - pos >= StaleReplayRewindSeconds)
        {
            _staleReplayLogged = true;
            DiagnosticsLog.Info(
                "Music",
                $"stale Finished replay (pos {_lastPlayPosition:0.##}s → {pos:0.##}s id='{_currentMusicId}')");
        }

        _lastPlayPosition = pos;
    }

    private void RetireCurrentSong()
    {
        if (_currentSong is null)
        {
            return;
        }

        // Keep alive so finalizer/Dispose cannot touch OpenAL under the active stream.
        _retiredSongs.Add(_currentSong);
        _currentSong = null;

        // Soft cap — drop oldest refs only (still no Dispose).
        const int maxRetired = 16;
        if (_retiredSongs.Count > maxRetired)
        {
            _retiredSongs.RemoveAt(0);
        }
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
