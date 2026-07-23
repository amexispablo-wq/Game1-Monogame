#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ColorBlocks.Replay;

namespace ColorBlocks;

public sealed class ReplayViewerScene : IScene
{
  private readonly ColorBlocksGame _game;
  private readonly string _levelId;
  private readonly string? _replayPathOverride;
  private readonly ReplayPlayer _player = new();
  private readonly Camera _camera = new(Vector2.Zero);
  private float _speedMultiplier = 1f;

  public ReplayViewerScene(ColorBlocksGame game, string levelId, string? replayPathOverride = null)
  {
    _game = game;
    _levelId = levelId;
    _replayPathOverride = replayPathOverride;
    LoadReplay();
  }

  public void OnExit()
  {
    _player.Unload();
    ReplayDiagnostics.ActivePlayer = null;
  }

  public void Update(GameTime gameTime)
  {
    InputManager input = _game.Input;

    if (input.ReplayViewerExitPressed || input.ExitPressed || input.MenuCancelPressed)
    {
      _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
      return;
    }

    if (input.ReplayViewerPausePressed || input.MenuConfirmPressed)
    {
      if (_player.IsPlaying)
      {
        _player.Pause();
      }
      else
      {
        _player.Play();
      }
    }

    if (input.ReplayViewerRestartPressed)
    {
      _player.Restart();
    }

    if (input.ReplayViewerSpeedUpPressed)
    {
      _speedMultiplier = 2f;
    }
    else if (input.ReplayViewerSpeedDownPressed)
    {
      _speedMultiplier = 0.5f;
    }
    else if (!input.ReplayViewerSpeedUpHeld && !input.ReplayViewerSpeedDownHeld)
    {
      _speedMultiplier = 1f;
    }

    _player.PlaybackSpeed = _speedMultiplier;
    _player.Update(gameTime, _camera, _game.Viewport);
  }

  public void LoadReplay()
  {
    ReplayFile replayFile;
    if (_replayPathOverride is not null)
    {
      ReplayFile? loaded = ReplayFileSerializer.TryLoad(_replayPathOverride, invalidateOnHashMismatch: false);
      if (loaded is null)
      {
        return;
      }

      replayFile = loaded;
    }
    else if (!ReplayStorage.TryLoadBestReplay(_levelId, out replayFile))
    {
      return;
    }

    _player.Load(replayFile.Data);
    _player.CameraMode = ReplayCameraMode.Recorded;
    _player.EndMode = ReplayPlaybackEndMode.Stop;
    _player.Play();
    ReplayDiagnostics.ActivePlayer = _player;

    if (replayFile.Data.Frames.Length > 0)
    {
      _camera.Position = replayFile.Data.Frames[0].CameraPosition.ToVector2();
      _camera.SetZoom(replayFile.Data.Frames[0].CameraZoom);
    }
  }

  public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
  {
    if (!_player.IsLoaded || _player.World is null)
    {
      spriteBatch.Begin(samplerState: SamplerState.PointClamp);
      SimpleTextRenderer.DrawCentered(
        spriteBatch,
        _game.Pixel,
        "NO REPLAY AVAILABLE",
        new Rectangle(0, 0, _game.Viewport.Width, _game.Viewport.Height),
        3,
        Color.White);
      spriteBatch.End();
      return;
    }

    ReplayWorld world = _player.World;
    GameplayWorldRenderer.Draw(
      spriteBatch,
      _game.Pixel,
      _game.Viewport,
      _camera,
      world.Level,
      world.Ropes,
      world.Players,
      world.LavaActive,
      world.LavaSurfaceY,
      world.ElapsedTime,
      gameTime);
  }
}
