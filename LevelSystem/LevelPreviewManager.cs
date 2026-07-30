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
    private const int WorkshopPreviewSize = 512;
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
        catch (Exception ex)
        {
            DiagnosticsLog.Info("LevelPreview", $"Save UI preview failed level={levelId} path='{previewPath}': {ex.Message}");
        }

        PreviewCache[levelId] = preview;
        return preview;
    }

    /// <summary>
    /// Ensures a fresh UI preview exists, then writes a 512x512 letterboxed PNG for Steam SetItemPreview.
    /// Returns the absolute workshop PNG path, or null on failure.
    /// </summary>
    public static string? EnsureWorkshopPreviewFile(
        GraphicsDevice graphicsDevice,
        Texture2D pixel,
        Level level,
        string levelId)
    {
        Texture2D preview = GenerateAndSavePreview(graphicsDevice, pixel, level, levelId);
        string workshopPath = GetWorkshopPreviewPath(levelId, level.Name);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(workshopPath)!);
            SaveLetterboxedWorkshopPng(graphicsDevice, pixel, preview, workshopPath);
            if (!File.Exists(workshopPath))
            {
                DiagnosticsLog.Info("LevelPreview", $"Workshop preview missing after save level={levelId}");
                return null;
            }

            string fullPath = Path.GetFullPath(workshopPath);
            DiagnosticsLog.Info(
                "LevelPreview",
                $"Workshop preview ready level={levelId} path='{fullPath}' bytes={new FileInfo(fullPath).Length}");
            return fullPath;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("LevelPreview", $"Workshop preview export failed level={levelId}: {ex.Message}");
            return null;
        }
    }

    public static string? TryFindExistingWorkshopPreviewFile(string levelId)
    {
        try
        {
            string previewsRoot = GetPreviewDirectory(levelId);
            if (!Directory.Exists(previewsRoot))
            {
                return null;
            }

            string stem = SanitizeLevelIdForFile(levelId);
            foreach (string file in Directory.EnumerateFiles(previewsRoot, "*_workshop.png"))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (!name.EndsWith($"_{stem}_workshop", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, $"{stem}_workshop", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains(stem, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Path.GetFullPath(file);
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("LevelPreview", $"Workshop preview lookup failed level={levelId}: {ex.Message}");
        }

        return null;
    }

    private static void SaveLetterboxedWorkshopPng(
        GraphicsDevice graphicsDevice,
        Texture2D pixel,
        Texture2D source,
        string destinationPath)
    {
        const int size = WorkshopPreviewSize;
        using var renderTarget = new RenderTarget2D(
            graphicsDevice,
            size,
            size,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.PreserveContents);

        RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();
        graphicsDevice.SetRenderTarget(renderTarget);
        graphicsDevice.Clear(new Color(28, 33, 43));

        float scale = Math.Min((float)size / source.Width, (float)size / source.Height);
        int drawW = Math.Max(1, (int)Math.Round(source.Width * scale));
        int drawH = Math.Max(1, (int)Math.Round(source.Height * scale));
        int drawX = (size - drawW) / 2;
        int drawY = (size - drawH) / 2;

        using (var spriteBatch = new SpriteBatch(graphicsDevice))
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(pixel, new Rectangle(0, 0, size, size), new Color(28, 33, 43));
            spriteBatch.Draw(source, new Rectangle(drawX, drawY, drawW, drawH), Color.White);
            spriteBatch.End();
        }

        var data = new Color[size * size];
        renderTarget.GetData(data);
        graphicsDevice.SetRenderTargets(previousTargets);

        using var texture = new Texture2D(graphicsDevice, size, size);
        texture.SetData(data);
        using FileStream writeStream = File.Create(destinationPath);
        texture.SaveAsPng(writeStream, size, size);
    }

    private static void RenamePreviousPreviewFiles(string previewsDir, string levelId, string previewPath)
    {
        if (!Directory.Exists(previewsDir))
        {
            return;
        }

        string stem = SanitizeLevelIdForFile(levelId);
        foreach (string filePath in Directory.EnumerateFiles(previewsDir, $"*_{stem}.png"))
        {
            if (filePath.EndsWith("_workshop.png", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

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

    private static string GetWorkshopPreviewPath(string levelId, string levelName)
    {
        string stem = SanitizeLevelIdForFile(levelId);
        string safeName = SanitizeFileName(levelName);
        string fileName = string.IsNullOrWhiteSpace(safeName)
            ? $"{stem}_workshop.png"
            : $"{safeName}_{stem}_workshop.png";
        return Path.Combine(GetPreviewDirectory(levelId), fileName);
    }

    private static string GetPreviewFileName(string levelId, string levelName)
    {
        string safeId = SanitizeLevelIdForFile(levelId);
        string safeName = SanitizeFileName(levelName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return $"{safeId}.png";
        }

        return $"{safeName}_{safeId}.png";
    }

    private static string SanitizeLevelIdForFile(string levelId) =>
        levelId.Replace(':', '_');

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

        foreach (PowerUp powerUp in level.PowerUps)
        {
            bounds = bounds.IsEmpty ? powerUp.Bounds : Rectangle.Union(bounds, powerUp.Bounds);
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
