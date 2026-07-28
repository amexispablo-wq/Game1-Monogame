#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        string resolved = ResolveTokens(TextTemplate);
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

    public static string ResolveTokens(string template)
    {
        if (string.IsNullOrEmpty(template) || template.IndexOf('{') < 0)
        {
            return template;
        }

        Dictionary<string, string> keys = SettingsManager.CurrentSettings.Keybindings;
        return TokenRegex.Replace(template, match =>
        {
            string action = match.Groups[1].Value;
            string keyName = ResolveKeyName(action, keys);
            return FormatKeyForSign(keyName);
        });
    }

    private static string ResolveKeyName(string action, Dictionary<string, string> keys)
    {
        if (keys.TryGetValue(action, out string? stored)
            && !string.IsNullOrWhiteSpace(stored)
            && !string.Equals(stored, "None", StringComparison.OrdinalIgnoreCase))
        {
            return stored;
        }

        return action switch
        {
            "MoveLeft" => Keys.A.ToString(),
            "MoveRight" => Keys.D.ToString(),
            "Jump" => Keys.W.ToString(),
            "Respawn" => Keys.R.ToString(),
            "RestartLevel" => Keys.F5.ToString(),
            "PullRope" => Keys.Space.ToString(),
            "FastFall" => Keys.S.ToString(),
            "Red" => Keys.J.ToString(),
            "Blue" => Keys.K.ToString(),
            "Green" => Keys.L.ToString(),
            _ => action
        };
    }

    private static string FormatKeyForSign(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)
            || string.Equals(keyName, "None", StringComparison.OrdinalIgnoreCase))
        {
            return "NONE";
        }

        string formatted = keyName switch
        {
            "Space" => "SPACE",
            "LeftShift" => "L SHIFT",
            "RightShift" => "R SHIFT",
            "LeftControl" => "L CTRL",
            "RightControl" => "R CTRL",
            "LeftAlt" => "L ALT",
            "RightAlt" => "R ALT",
            _ => keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1])
                ? keyName[1].ToString()
                : keyName
        };

        var sb = new StringBuilder(formatted.Length);
        foreach (char c in formatted.ToUpperInvariant())
        {
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is ' ' or '<' or '>' or '^' or ':' or '%')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append(' ');
            }
        }

        return sb.ToString().Trim();
    }
}
