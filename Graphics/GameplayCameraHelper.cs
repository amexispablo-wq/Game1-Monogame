using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>Shared gameplay camera targeting for live play and replay playback.</summary>
public static class GameplayCameraHelper
{
  public static Vector2 GetPlayersCenter(IReadOnlyList<Player> players, Vector2 fallback, float interpolationAlpha = 1f)
  {
    float alpha = MathHelper.Clamp(interpolationAlpha, 0f, 1f);
    int localCount = 0;
    Vector2 total = Vector2.Zero;
    foreach (Player player in players)
    {
      if (!player.IsLocal)
      {
        continue;
      }

      localCount++;
      total += player.GetDrawPosition(alpha) + (player.Size * 0.5f);
    }

    if (localCount == 0)
    {
      return fallback;
    }

    return total / localCount;
  }

  public static float GetTargetCameraZoom(
    IReadOnlyList<Player> players,
    IReadOnlyList<Rope> ropes,
    Viewport viewport,
    float interpolationAlpha = 1f)
  {
    float alpha = MathHelper.Clamp(interpolationAlpha, 0f, 1f);
    int localCount = 0;
    foreach (Player player in players)
    {
      if (player.IsLocal)
      {
        localCount++;
      }
    }

    if (localCount <= 1 || viewport.Width <= 0 || viewport.Height <= 0)
    {
      return 1f;
    }

    float minX = float.MaxValue;
    float minY = float.MaxValue;
    float maxX = float.MinValue;
    float maxY = float.MinValue;

    foreach (Player player in players)
    {
      if (!player.IsLocal)
      {
        continue;
      }

      Vector2 center = player.GetDrawPosition(alpha) + (player.Size * 0.5f);
      minX = MathF.Min(minX, center.X);
      minY = MathF.Min(minY, center.Y);
      maxX = MathF.Max(maxX, center.X);
      maxY = MathF.Max(maxY, center.Y);
    }

    foreach (Rope rope in ropes)
    {
      Vector2 startAnchor = PlayerDrawAnchor(rope.StartPlayer, alpha);
      Vector2 endAnchor = PlayerDrawAnchor(rope.EndPlayer, alpha);
      for (int i = 0; i < rope.Nodes.Count; i++)
      {
        Vector2 p = rope.GetDrawNodePosition(i, alpha, startAnchor, endAnchor);
        minX = MathF.Min(minX, p.X);
        minY = MathF.Min(minY, p.Y);
        maxX = MathF.Max(maxX, p.X);
        maxY = MathF.Max(maxY, p.Y);
      }
    }

    const float cameraPadding = 360f;
    float groupWidth = MathF.Max(1f, maxX - minX + cameraPadding);
    float groupHeight = MathF.Max(1f, maxY - minY + cameraPadding);
    float zoomX = viewport.Width / groupWidth;
    float zoomY = viewport.Height / groupHeight;

    return MathHelper.Clamp(MathF.Min(zoomX, zoomY), 0.55f, 1f);
  }

  public static void UpdateSmoothFollow(Camera camera, GameTime gameTime, Vector2 targetCenter, float targetZoom)
  {
    float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
    float smoothing = 1f - MathF.Exp(-6f * dt);
    camera.Position = Vector2.Lerp(camera.Position, targetCenter, smoothing);
    camera.SetZoom(MathHelper.Lerp(camera.Zoom, targetZoom, smoothing));
  }

    private static Vector2 PlayerDrawAnchor(Player player, float alpha) =>
      player.GetDrawPosition(alpha) + (player.Size * 0.5f);
}
