using System.Collections.Generic;
using ColorBlocks.Replay;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>Shared world rendering used by gameplay and replay backgrounds.</summary>
public static class GameplayWorldRenderer
{
  public static void Draw(
    SpriteBatch spriteBatch,
    Texture2D pixel,
    Viewport viewport,
    Camera camera,
    Level level,
    IReadOnlyList<Rope> ropes,
    IReadOnlyList<Player> players,
    bool lavaActive,
    float lavaSurfaceY,
    float elapsedTime,
    GameTime gameTime,
    bool debugDraw = false,
    GhostPlayer? ghostPlayer = null)
  {
    spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(36, 41, 52));
    spriteBatch.End();

    spriteBatch.Begin(
      samplerState: SamplerState.PointClamp,
      transformMatrix: camera.GetTransform(viewport));

    level.Draw(spriteBatch, pixel, debugDraw, elapsedTime, isEditorMode: false);
    foreach (Rope rope in ropes)
    {
      rope.Draw(spriteBatch, pixel, debugDraw);
    }

    foreach (Player player in players)
    {
      player.Draw(spriteBatch, pixel, debugDraw);
    }

    ghostPlayer?.Draw(spriteBatch, pixel, debugDraw);

    if (lavaActive)
    {
      Rectangle lavaView = camera.GetVisibleWorldRectangle(viewport, 96);
      LavaLine.Draw(
        spriteBatch,
        pixel,
        lavaView,
        lavaSurfaceY,
        elapsedTime,
        drawParticles: true);
    }

    spriteBatch.End();
  }
}
