#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks.Replay;

/// <summary>Deterministic replay playback by applying stored frame snapshots at fixed sim tick rate.</summary>
public sealed class ReplayPlayer
{
  private ReplayData? _data;
  private ReplayWorld? _world;
  private int _frameIndex;
  private int _loopCount;
  private bool _isPlaying = true;
  private float _tickAccumulator;
  private float _playbackSpeed = 1f;
  private ReplayCameraMode _cameraMode = ReplayCameraMode.GameplayFollow;
  private ReplayPlaybackEndMode _endMode = ReplayPlaybackEndMode.Loop;

  public ReplayWorld? World => _world;
  public bool IsLoaded => _data is not null && _world is not null;
  public bool IsPlaying => _isPlaying;
  public int FrameIndex => _frameIndex;
  public int FrameCount => _data?.Frames.Length ?? 0;
  public int LoopCount => _loopCount;
  public float PlaybackSpeed
  {
    get => _playbackSpeed;
    set => _playbackSpeed = MathF.Max(0.1f, value);
  }

  public ReplayCameraMode CameraMode
  {
    get => _cameraMode;
    set => _cameraMode = value;
  }

  public ReplayPlaybackEndMode EndMode
  {
    get => _endMode;
    set => _endMode = value;
  }

  public int TicksPerSecond => _data?.Header.TicksPerSecond ?? ReplayConstants.DefaultTicksPerSecond;
  public float ReplayLengthSeconds => FrameCount / (float)Math.Max(1, TicksPerSecond);
  public long CurrentSimulationTick => GetCurrentFrame()?.Tick ?? 0;
  public Vector2 CurrentCameraTarget => _world is null
    ? Vector2.Zero
    : GameplayCameraHelper.GetPlayersCenter(_world.Players, _world.Level.PlayerStart);

  public void Load(ReplayData data, ReplayCameraMode cameraMode, ReplayPlaybackEndMode endMode)
  {
    Unload();
    _data = data;
    _world = ReplayWorld.Create(data);
    _frameIndex = 0;
    _loopCount = 0;
    _tickAccumulator = 0f;
    _isPlaying = true;
    _cameraMode = cameraMode;
    _endMode = endMode;
    _playbackSpeed = 1f;
    ApplyCurrentFrame();
  }

  public void Load(ReplayData data)
  {
    Load(data, ReplayCameraMode.GameplayFollow, ReplayPlaybackEndMode.Loop);
  }

  public void Unload()
  {
    _data = null;
    _world = null;
    _frameIndex = 0;
    _loopCount = 0;
    _tickAccumulator = 0f;
    _isPlaying = false;
  }

  public void ResetPlayback()
  {
    if (_data is null)
    {
      return;
    }

    _frameIndex = 0;
    _loopCount = 0;
    _tickAccumulator = 0f;
    _isPlaying = true;
    ApplyCurrentFrame();
  }

  public bool HasFinishedPlayback =>
    _endMode == ReplayPlaybackEndMode.Stop
    && !_isPlaying
    && _data is not null
    && _data.Frames.Length > 0
    && _frameIndex >= _data.Frames.Length - 1;

  public void Play() => _isPlaying = true;

  public void Pause() => _isPlaying = false;

  public void Restart() => ResetPlayback();

  public void SeekToFrame(int frameIndex)
  {
    if (_data is null || _data.Frames.Length == 0)
    {
      return;
    }

    _frameIndex = Math.Clamp(frameIndex, 0, _data.Frames.Length - 1);
    ApplyCurrentFrame();
  }

  public void AdvanceTick()
  {
    if (_data is null || _data.Frames.Length == 0)
    {
      return;
    }

    if (_frameIndex + 1 >= _data.Frames.Length)
    {
      if (_endMode == ReplayPlaybackEndMode.Stop)
      {
        _isPlaying = false;
        return;
      }

      _frameIndex = 0;
      _loopCount++;
    }
    else
    {
      _frameIndex++;
    }

    ApplyCurrentFrame();
  }

  public void Loop() => Restart();

  public void Update(GameTime gameTime, Camera camera, Viewport viewport)
  {
    if (!_isPlaying || _data is null || _data.Frames.Length == 0 || _world is null)
    {
      return;
    }

    float fixedDelta = 1f / MathF.Max(1, _data.Header.TicksPerSecond);
    _tickAccumulator += (float)gameTime.ElapsedGameTime.TotalSeconds * _playbackSpeed;

    while (_tickAccumulator >= fixedDelta)
    {
      _tickAccumulator -= fixedDelta;
      AdvanceTick();
      if (!_isPlaying)
      {
        break;
      }
    }

    UpdateCamera(gameTime, camera, viewport);
  }

  public ReplayFrameSnapshot? GetCurrentFrame()
  {
    if (_data is null || _data.Frames.Length == 0)
    {
      return null;
    }

    return _data.Frames[_frameIndex];
  }

  private void ApplyCurrentFrame()
  {
    ReplayFrameSnapshot? frame = GetCurrentFrame();
    if (frame is null || _world is null)
    {
      return;
    }

    _world.ApplyFrame(frame);
  }

  private void UpdateCamera(GameTime gameTime, Camera camera, Viewport viewport)
  {
    ReplayFrameSnapshot? frame = GetCurrentFrame();
    if (frame is null || _world is null)
    {
      return;
    }

    if (_cameraMode == ReplayCameraMode.Recorded)
    {
      camera.Position = frame.CameraPosition.ToVector2();
      camera.SetZoom(frame.CameraZoom);
      return;
    }

    Vector2 targetCenter = GameplayCameraHelper.GetPlayersCenter(_world.Players, _world.Level.PlayerStart);
    float targetZoom = GameplayCameraHelper.GetTargetCameraZoom(_world.Players, _world.Ropes, viewport);
    GameplayCameraHelper.UpdateSmoothFollow(camera, gameTime, targetCenter, targetZoom);
  }
}
