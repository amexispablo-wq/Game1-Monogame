#nullable enable
using System;
using Microsoft.Xna.Framework.Media;

namespace ColorBlocks;

public sealed class MusicManager
{
    public const string MenuMusicId = "MainMenu";
    public const string EditorMusicId = "levelEditor";

    private string? _currentMusicId;

    public float Volume { get; private set; } = 0.75f;
    public bool IsPlaying => MediaPlayer.State == MediaState.Playing;
    public string? CurrentMusicId => _currentMusicId;

    public void ApplyVolume(float volume)
    {
        Volume = Math.Clamp(volume, 0f, 1f);
        MediaPlayer.Volume = Volume;
    }

    public void PlayMenuMusic() => PlayTrack(MenuMusicId, "Audio/Music/MainMenu");

    public void PlayEditorMusic() => PlayTrack(EditorMusicId, "Audio/Music/levelEditor");

    public void PlayLevelMusic(string musicId)
    {
        if (string.IsNullOrWhiteSpace(musicId))
        {
            musicId = LevelMusicLibrary.DefaultMusicId;
        }

        // Level playlist tracks live under Music/; menu/editor live under Audio/Music/.
        PlayTrack(musicId, $"Music/{musicId}");
    }

    public void Stop()
    {
        if (MediaPlayer.State != MediaState.Stopped)
        {
            MediaPlayer.Stop();
        }

        _currentMusicId = null;
    }

    private void PlayTrack(string musicId, string assetPath)
    {
        if (string.Equals(_currentMusicId, musicId, StringComparison.Ordinal)
            && MediaPlayer.State == MediaState.Playing)
        {
            return;
        }

        Stop();

        try
        {
            Song? song = ContentResolver.TryLoadSong(assetPath);
            if (song is null)
            {
                return;
            }

            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = Volume;
            MediaPlayer.Play(song);
            _currentMusicId = musicId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Music unavailable for '{assetPath}': {ex.Message}");
        }
    }
}
