#nullable enable
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks.Replay;

/// <summary>Draws highlight composite replay behind menu scenes.</summary>
public sealed class ReplayBackgroundRenderer
{
  private readonly CompositeHighlightPlayer _highlightPlayer = new();
  private int _loadedHighlightVersion = -1;

  public CompositeHighlightPlayer HighlightPlayer => _highlightPlayer;
  public bool IsVisible { get; private set; }

  public void Update(ColorBlocksGame game, GameTime gameTime)
  {
    if (!ShouldRender(game))
    {
      if (IsVisible || _highlightPlayer.IsLoaded)
      {
        _highlightPlayer.Unload();
        _loadedHighlightVersion = -1;
      }

      IsVisible = false;
      ReplayDiagnostics.ActivePlayer = null;
      return;
    }

    if (!HighlightManager.TryGetHighlightReplay(out HighlightReplayFile highlights))
    {
      IsVisible = false;
      return;
    }

    if (_loadedHighlightVersion != ReplayManager.HighlightVersion || !_highlightPlayer.IsLoaded)
    {
      _highlightPlayer.Load(highlights);
      _loadedHighlightVersion = ReplayManager.HighlightVersion;
    }

    IsVisible = true;
    ReplayDiagnostics.ActivePlayer = _highlightPlayer.ActiveClipPlayer;
    _highlightPlayer.Update(gameTime, game.Viewport);
  }

  public void Draw(ColorBlocksGame game, GameTime gameTime, SpriteBatch spriteBatch)
  {
    if (!IsVisible)
    {
      return;
    }

    _highlightPlayer.Draw(spriteBatch, game.Pixel, game.Viewport, gameTime);
  }

  public static bool ShouldRender(ColorBlocksGame game)
  {
    return ReplayManager.MenuBackgroundEnabled
      && HighlightManager.HasHighlights
      && game.CurrentScene is not GameScene
      && game.CurrentScene is not ReplayViewerScene;
  }
}

/// <summary>Helpers for menu scenes to layer UI over the replay background.</summary>
public static class ReplayMenuBackground
{
  public static bool IsActive(ColorBlocksGame game) => ReplayBackgroundRenderer.ShouldRender(game);

  public static void DrawDimmingOverlay(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
  {
    spriteBatch.Draw(
      pixel,
      new Rectangle(0, 0, viewport.Width, viewport.Height),
      new Color(18, 22, 30, 110));
  }
}
