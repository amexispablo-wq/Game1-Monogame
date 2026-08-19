#nullable enable
using System;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>World-space instructional text. Tokens like {Jump} resolve to current keybinds.</summary>
public sealed class LevelSign
{
    private static readonly Regex TokenRegex = new(@"\{([A-Za-z0-9]+)\}", RegexOptions.Compiled);

    public LevelSign(Vector2 position, string textTemplate, int scale = 2)
    {
        Position = position;
        TextTemplate = textTemplate ?? string.Empty;
        Scale = Math.Clamp(scale <= 0 ? 2 : scale, 1, 6);
    }

    public Vector2 Position { get; }
    public string TextTemplate { get; }
    public int Scale { get; }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool useGamepadBindings = false)
    {
        string resolved = ResolveTokens(TextTemplate, useGamepadBindings);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return;
        }

        string[] lines = resolved.Replace("\r\n", "\n").Split('\n');
        float y = Position.Y;
        int lineHeight = (7 * Scale) + (2 * Scale);
        foreach (string line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                SimpleTextRenderer.DrawString(
                    spriteBatch,
                    pixel,
                    line.Trim(),
                    new Vector2(Position.X, y),
                    Scale,
                    new Color(240, 240, 245));
            }

            y += lineHeight;
        }
    }

    public static string ResolveTokens(string template, bool useGamepadBindings = false)
    {
        if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0)
        {
            return template;
        }

        return TokenRegex.Replace(template, match =>
            BindingDisplay.ForActionName(match.Groups[1].Value, useGamepadBindings));
    }
}
