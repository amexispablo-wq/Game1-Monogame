#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public static class LevelPreviewManager
{
    private const int PreviewWidth = 340;
    private const int PreviewHeight = 190;
    private const string PreviewDirectoryName = "LevelPreviews";
    private static readonly Dictionary<string, Texture2D> PreviewCache = new();

    public static void InvalidateCache()
    {
        foreach (Texture2D texture in PreviewCache.Values)
        {
            texture.Dispose();
        }

        PreviewCache.Clear();
    }

    public static Texture2D GetPreview(GraphicsDevice graphicsDevice, Texture2D pixel, Level level, string levelId)
    {
        if (PreviewCache.TryGetValue(levelId, out Texture2D? cached) && cached is { IsDisposed: false })
        {
            return cached;
        }

        // Always regenerate from the in-memory level. On-disk PNGs are only a
        // side artifact and may be stale (older builds saved blank previews).
        return GenerateAndSavePreview(graphicsDevice, pixel, level, levelId);
    }

    public static Texture2D GenerateAndSavePreview(GraphicsDevice graphicsDevice, Texture2D pixel, Level level, string levelId)
    {
        string previewsDir = GetPreviewDirectory(levelId);
        Directory.CreateDirectory(previewsDir);
        string previewPath = GetPreviewPath(levelId, level.Name);

        RenamePreviousPreviewFiles(previewsDir, levelId, previewPath);

        Texture2D preview = GeneratePreview(graphicsDevice, pixel, level);

        try
        {
            using FileStream writeStream = File.Create(previewPath);
            preview.SaveAsPng(writeStream, preview.Width, preview.Height);
        }
        catch
        {
            // Ignore save failures; preview is still available in memory.
        }

        PreviewCache[levelId] = preview;
        return preview;
    }

    private static void RenamePreviousPreviewFiles(string previewsDir, string levelId, string previewPath)
    {
        if (!Directory.Exists(previewsDir))
        {
            return;
        }

        foreach (string filePath in Directory.EnumerateFiles(previewsDir, $"*_{levelId}.png"))
        {
            if (string.Equals(filePath, previewPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            TryMoveFile(filePath, previewPath);
        }
    }

    private static void TryMoveFile(string sourcePath, string destinationPath)
    {
        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(sourcePath, destinationPath);
        }
        catch
        {
            try
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static string GetPreviewDirectory(string levelId)
    {
        LevelSource source = LevelIdentity.GetSource(levelId);
        return LevelContentPaths.GetPreviewsRoot(source);
    }

    private static string GetPreviewPath(string levelId, string levelName)
    {
        string fileName = GetPreviewFileName(levelId, levelName);
        return Path.Combine(GetPreviewDirectory(levelId), fileName);
    }

    private static string GetPreviewFileName(string levelId, string levelName)
    {
        string safeName = SanitizeFileName(levelName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return $"{levelId}.png";
        }

        return $"{safeName}_{levelId}.png";
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (char character in name.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                builder.Append('_');
                continue;
            }

            if (Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0)
            {
                builder.Append('_');
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString().Trim('_');
    }

    private static Texture2D GeneratePreview(GraphicsDevice graphicsDevice, Texture2D pixel, Level level)
    {
        int width = PreviewWidth;
        int height = PreviewHeight;

        Rectangle bounds = GetPreviewBounds(level);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return CreatePlaceholderPreview(graphicsDevice, pixel);
        }

        const float padding = 24f;
        float sourceWidth = bounds.Width + padding * 2f;
        float sourceHeight = bounds.Height + padding * 2f;
        float scale = System.Math.Min(width / sourceWidth, height / sourceHeight);
        if (scale <= 0f)
        {
            scale = 1f;
        }

        float scaledWidth = sourceWidth * scale;
        float scaledHeight = sourceHeight * scale;
        float extraX = (width - scaledWidth) / 2f;
        float extraY = (height - scaledHeight) / 2f;
        float worldOffsetX = padding + extraX - bounds.X * scale;
        float worldOffsetY = padding + extraY - bounds.Y * scale;

        // PreserveContents is required: with the default DiscardContents, GL
        // backends drop the pixels once the target is unbound, so GetData and
        // drawing both read back a black/empty texture.
        using var renderTarget = new RenderTarget2D(
            graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

        RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();
        graphicsDevice.SetRenderTarget(renderTarget);
        graphicsDevice.Clear(new Color(28, 33, 43));

        using (var spriteBatch = new SpriteBatch(graphicsDevice))
        {
            var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(worldOffsetX, worldOffsetY, 0f);
            spriteBatch.Begin(transformMatrix: transform, samplerState: SamplerState.PointClamp);
            level.Draw(spriteBatch, pixel, debugDraw: false, animationSeconds: 0f, isEditorMode: false);
            spriteBatch.End();
        }

        // Copy into a plain Texture2D. A RenderTarget2D loses its contents on
        // device reset (e.g. changing resolution), which would turn cached
        // previews black; a regular texture survives.
        var data = new Color[width * height];
        renderTarget.GetData(data);

        graphicsDevice.SetRenderTargets(previousTargets);

        var texture = new Texture2D(graphicsDevice, width, height);
        texture.SetData(data);
        return texture;
    }

    private static Rectangle GetPreviewBounds(Level level)
    {
        Rectangle bounds = Rectangle.Empty;

        foreach (Platform platform in level.Platforms)
        {
            bounds = bounds.IsEmpty ? platform.Bounds : Rectangle.Union(bounds, platform.Bounds);
        }

        foreach (Goal goal in level.Goals)
        {
            bounds = bounds.IsEmpty ? goal.Bounds : Rectangle.Union(bounds, goal.Bounds);
        }

        foreach (CheckpointFlag checkpoint in level.CheckpointFlags)
        {
            bounds = bounds.IsEmpty ? checkpoint.Bounds : Rectangle.Union(bounds, checkpoint.Bounds);
        }

        foreach (LaunchPad launchPad in level.LaunchPads)
        {
            bounds = bounds.IsEmpty ? launchPad.Bounds : Rectangle.Union(bounds, launchPad.Bounds);
        }

        if (bounds.IsEmpty)
        {
            return new Rectangle(0, 0, level.WorldSize.X, level.WorldSize.Y);
        }

        return bounds;
    }

    private static Texture2D CreatePlaceholderPreview(GraphicsDevice graphicsDevice, Texture2D pixel)
    {
        var placeholder = new RenderTarget2D(
            graphicsDevice, PreviewWidth, PreviewHeight, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        graphicsDevice.SetRenderTarget(placeholder);
        graphicsDevice.Clear(new Color(22, 26, 34));

        using (var spriteBatch = new SpriteBatch(graphicsDevice))
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            var background = new Rectangle(0, 0, PreviewWidth, PreviewHeight);
            spriteBatch.Draw(pixel, background, new Color(28, 34, 46));
            DrawHelper.DrawBorder(spriteBatch, pixel, background, new Color(95, 110, 135), 2);

            var messageBounds = new Rectangle(16, 16, PreviewWidth - 32, PreviewHeight - 32);
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "No Preview Available", messageBounds, 2, new Color(220, 220, 220));
            spriteBatch.End();
        }

        graphicsDevice.SetRenderTarget(null);
        return placeholder;
    }
}
