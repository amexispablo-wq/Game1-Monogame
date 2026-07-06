#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks.Replay;

/// <summary>Semi-transparent best-run overlay synced to gameplay simulation ticks.</summary>
public sealed class GhostPlayer
{
  private readonly ReplayPlayer _player = new();

  public bool IsActive { get; private set; }
  public ReplayWorld? World => _player.World;

  public bool TryLoadBestRun(string levelId)
  {
    Unload();
    if (!ReplayStorage.TryLoadBestReplay(levelId, out ReplayFile replayFile))
    {
      return false;
    }

    _player.Load(replayFile.Data, ReplayCameraMode.Recorded, ReplayPlaybackEndMode.Stop);
    IsActive = true;
    Reset();
    return true;
  }

  public void Unload()
  {
    _player.Unload();
    IsActive = false;
  }

  public void Reset()
  {
    if (!IsActive)
    {
      return;
    }

    _player.ResetPlayback();
    _player.SeekToFrame(0);
  }

  public void SyncToGameplayTick(long gameplayTick)
  {
    if (!IsActive || _player.FrameCount == 0)
    {
      return;
    }

    int target = (int)Math.Clamp(gameplayTick, 0, _player.FrameCount - 1);
    _player.SeekToFrame(target);
  }

  public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw = false)
  {
    if (!IsActive || _player.World is null)
    {
      return;
    }

    const float ghostAlpha = 0.42f;

    foreach (Player player in _player.World.Players)
    {
      Rectangle bounds = player.Bounds;
      Rectangle body = new(
        bounds.X + 3,
        bounds.Y + 3,
        Math.Max(1, bounds.Width - 6),
        Math.Max(1, bounds.Height - 6));

      Color fill = player.PlayerColor.ToXnaColor() * ghostAlpha;
      spriteBatch.Draw(pixel, body, fill);

      PlayerSkinData? skin = player.GetCosmeticSkinForDraw();
      if (skin is not null)
      {
        PlayerSkinRenderer.DrawSkinOverlay(spriteBatch, pixel, body, skin);
      }

      DrawHelper.DrawBorder(spriteBatch, pixel, body, Color.White * (ghostAlpha * 0.8f), 2);
    }
  }
}
