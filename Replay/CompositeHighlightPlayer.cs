#nullable enable
using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks.Replay;

/// <summary>Plays ranked highlight clips with crossfade transitions for menu background.</summary>
public sealed class CompositeHighlightPlayer
{
  private readonly ReplayPlayer _clipPlayer = new();
  private readonly Camera _camera = new(Vector2.Zero);
  private HighlightReplayFile? _highlights;
  private int _clipIndex;
  private float _transitionAlpha;
  private bool _inTransition;
  private float _transitionTimer;
  private bool _pendingAdvance;

  public ReplayPlayer ActiveClipPlayer => _clipPlayer;
  public bool IsLoaded => _highlights is not null && _highlights.Clips.Length > 0;
  public int ClipIndex => _clipIndex;
  public int ClipCount => _highlights?.Clips.Length ?? 0;

  public void Load(HighlightReplayFile highlights)
  {
    _highlights = highlights;
    _clipIndex = 0;
    _transitionAlpha = 0f;
    _inTransition = false;
    _transitionTimer = 0f;
    _pendingAdvance = false;
    LogHighlight($"Loaded Highlight Clips: {highlights.Clips.Length}");
    LoadCurrentClip();
  }

  public void Unload()
  {
    _highlights = null;
    _clipPlayer.Unload();
    _clipIndex = 0;
    _inTransition = false;
    _pendingAdvance = false;
  }

  public void Update(GameTime gameTime, Viewport viewport)
  {
    if (!IsLoaded)
    {
      return;
    }

    if (_inTransition)
    {
      _transitionTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
      _transitionAlpha = MathHelper.Clamp(_transitionTimer / ReplayConstants.HighlightTransitionSeconds, 0f, 1f);
      if (_transitionTimer >= ReplayConstants.HighlightTransitionSeconds)
      {
        _inTransition = false;
        _transitionAlpha = 0f;
        _transitionTimer = 0f;
        if (_pendingAdvance)
        {
          _pendingAdvance = false;
          AdvanceClip();
        }
      }

      return;
    }

    _clipPlayer.Update(gameTime, _camera, viewport);
    if (IsCurrentClipComplete())
    {
      BeginTransition();
    }
  }

  private bool IsCurrentClipComplete()
  {
    if (_clipPlayer.FrameCount <= 0)
    {
      return false;
    }

    return _clipPlayer.HasFinishedPlayback
      || (!_clipPlayer.IsPlaying && _clipPlayer.FrameIndex >= _clipPlayer.FrameCount - 1);
  }

  public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, GameTime gameTime)
  {
    if (!IsLoaded || _clipPlayer.World is null)
    {
      return;
    }

    ReplayWorld world = _clipPlayer.World;
    GameplayWorldRenderer.Draw(
      spriteBatch,
      pixel,
      viewport,
      _camera,
      world.Level,
      world.Ropes,
      world.Players,
      world.LavaActive,
      world.LavaSurfaceY,
      world.ElapsedTime,
      gameTime);

    if (_inTransition && _transitionAlpha > 0f)
    {
      spriteBatch.Begin(samplerState: SamplerState.PointClamp);
      spriteBatch.Draw(
        pixel,
        new Rectangle(0, 0, viewport.Width, viewport.Height),
        Color.Black * _transitionAlpha);
      spriteBatch.End();
    }
  }

  private void BeginTransition()
  {
    if (_inTransition)
    {
      return;
    }

    _inTransition = true;
    _transitionTimer = 0f;
    _transitionAlpha = 0f;
    _pendingAdvance = true;
  }

  private void AdvanceClip()
  {
    if (_highlights is null || _highlights.Clips.Length == 0)
    {
      return;
    }

    _clipIndex = (_clipIndex + 1) % _highlights.Clips.Length;
    LoadCurrentClip();
  }

  private void LoadCurrentClip()
  {
    if (_highlights is null || _highlights.Clips.Length == 0)
    {
      return;
    }

    HighlightClipEntry clip = _highlights.Clips[_clipIndex];
    LogHighlight($"Playing Clip {_clipIndex + 1}/{_highlights.Clips.Length} ({clip.Type})");
    _clipPlayer.Load(clip.ClipData, ReplayCameraMode.Recorded, ReplayPlaybackEndMode.Stop);
    _clipPlayer.EndMode = ReplayPlaybackEndMode.Stop;
    _clipPlayer.Play();

    if (clip.ClipData.Frames.Length > 0)
    {
      _camera.Position = clip.ClipData.Frames[0].CameraPosition.ToVector2();
      _camera.SetZoom(clip.ClipData.Frames[0].CameraZoom);
    }
  }

  private static void LogHighlight(string message)
  {
#if DEBUG
    Debug.WriteLine($"[HighlightReplay] {message}");
#endif
  }
}
