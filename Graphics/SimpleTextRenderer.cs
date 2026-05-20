using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public static class SimpleTextRenderer
{
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int GlyphSpacing = 1;

    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        [' '] = new[]
        {
            "00000",
            "00000",
            "00000",
            "00000",
            "00000",
            "00000",
            "00000"
        },
        ['0'] = new[]
        {
            "01110",
            "10001",
            "10011",
            "10101",
            "11001",
            "10001",
            "01110"
        },
        ['1'] = new[]
        {
            "00100",
            "01100",
            "00100",
            "00100",
            "00100",
            "00100",
            "01110"
        },
        ['2'] = new[]
        {
            "01110",
            "10001",
            "00001",
            "00010",
            "00100",
            "01000",
            "11111"
        },
        ['3'] = new[]
        {
            "11110",
            "00001",
            "00001",
            "01110",
            "00001",
            "00001",
            "11110"
        },
        ['4'] = new[]
        {
            "00010",
            "00110",
            "01010",
            "10010",
            "11111",
            "00010",
            "00010"
        },
        ['5'] = new[]
        {
            "11111",
            "10000",
            "10000",
            "11110",
            "00001",
            "00001",
            "11110"
        },
        ['6'] = new[]
        {
            "01110",
            "10000",
            "10000",
            "11110",
            "10001",
            "10001",
            "01110"
        },
        ['7'] = new[]
        {
            "11111",
            "00001",
            "00010",
            "00100",
            "01000",
            "01000",
            "01000"
        },
        ['8'] = new[]
        {
            "01110",
            "10001",
            "10001",
            "01110",
            "10001",
            "10001",
            "01110"
        },
        ['9'] = new[]
        {
            "01110",
            "10001",
            "10001",
            "01111",
            "00001",
            "00001",
            "01110"
        },
        [':'] = new[]
        {
            "00000",
            "00100",
            "00100",
            "00000",
            "00100",
            "00100",
            "00000"
        },
        ['%'] = new[]
        {
            "11001",
            "11010",
            "00100",
            "01000",
            "10110",
            "00110",
            "00000"
        },
        ['<'] = new[]
        {
            "00001",
            "00010",
            "00100",
            "01000",
            "00100",
            "00010",
            "00001"
        },
        ['>'] = new[]
        {
            "10000",
            "01000",
            "00100",
            "00010",
            "00100",
            "01000",
            "10000"
        },
        ['^'] = new[]
        {
            "00100",
            "01010",
            "10001",
            "00000",
            "00000",
            "00000",
            "00000"
        },
        ['A'] = new[]
        {
            "01110",
            "10001",
            "10001",
            "11111",
            "10001",
            "10001",
            "10001"
        },
        ['B'] = new[]
        {
            "11110",
            "10001",
            "10001",
            "11110",
            "10001",
            "10001",
            "11110"
        },
        ['C'] = new[]
        {
            "01111",
            "10000",
            "10000",
            "10000",
            "10000",
            "10000",
            "01111"
        },
        ['D'] = new[]
        {
            "11110",
            "10001",
            "10001",
            "10001",
            "10001",
            "10001",
            "11110"
        },
        ['E'] = new[]
        {
            "11111",
            "10000",
            "10000",
            "11110",
            "10000",
            "10000",
            "11111"
        },
        ['F'] = new[]
        {
            "11111",
            "10000",
            "10000",
            "11110",
            "10000",
            "10000",
            "10000"
        },
        ['G'] = new[]
        {
            "01110",
            "10001",
            "10000",
            "10111",
            "10001",
            "10001",
            "01110"
        },
        ['H'] = new[]
        {
            "10001",
            "10001",
            "10001",
            "11111",
            "10001",
            "10001",
            "10001"
        },
        ['I'] = new[]
        {
            "11111",
            "00100",
            "00100",
            "00100",
            "00100",
            "00100",
            "11111"
        },
        ['J'] = new[]
        {
            "00111",
            "00010",
            "00010",
            "00010",
            "10010",
            "10010",
            "01100"
        },
        ['K'] = new[]
        {
            "10001",
            "10010",
            "10100",
            "11000",
            "10100",
            "10010",
            "10001"
        },
        ['L'] = new[]
        {
            "10000",
            "10000",
            "10000",
            "10000",
            "10000",
            "10000",
            "11111"
        },
        ['M'] = new[]
        {
            "10001",
            "11011",
            "10101",
            "10101",
            "10001",
            "10001",
            "10001"
        },
        ['N'] = new[]
        {
            "10001",
            "11001",
            "10101",
            "10011",
            "10001",
            "10001",
            "10001"
        },
        ['O'] = new[]
        {
            "01110",
            "10001",
            "10001",
            "10001",
            "10001",
            "10001",
            "01110"
        },
        ['P'] = new[]
        {
            "11110",
            "10001",
            "10001",
            "11110",
            "10000",
            "10000",
            "10000"
        },
        ['Q'] = new[]
        {
            "01110",
            "10001",
            "10001",
            "10001",
            "10101",
            "10010",
            "01101"
        },
        ['R'] = new[]
        {
            "11110",
            "10001",
            "10001",
            "11110",
            "10100",
            "10010",
            "10001"
        },
        ['S'] = new[]
        {
            "01111",
            "10000",
            "10000",
            "01110",
            "00001",
            "00001",
            "11110"
        },
        ['T'] = new[]
        {
            "11111",
            "00100",
            "00100",
            "00100",
            "00100",
            "00100",
            "00100"
        },
        ['U'] = new[]
        {
            "10001",
            "10001",
            "10001",
            "10001",
            "10001",
            "10001",
            "01110"
        },
        ['V'] = new[]
        {
            "10001",
            "10001",
            "10001",
            "10001",
            "10001",
            "01010",
            "00100"
        },
        ['W'] = new[]
        {
            "10001",
            "10001",
            "10001",
            "10101",
            "10101",
            "10101",
            "01010"
        },
        ['X'] = new[]
        {
            "10001",
            "10001",
            "01010",
            "00100",
            "01010",
            "10001",
            "10001"
        },
        ['Y'] = new[]
        {
            "10001",
            "10001",
            "01010",
            "00100",
            "00100",
            "00100",
            "00100"
        },
        ['Z'] = new[]
        {
            "11111",
            "00001",
            "00010",
            "00100",
            "01000",
            "10000",
            "11111"
        }
    };

    public static Point MeasureString(string text, int scale)
    {
        if (string.IsNullOrEmpty(text) || scale <= 0)
        {
            return Point.Zero;
        }

        int width = (text.Length * GlyphWidth * scale) + ((text.Length - 1) * GlyphSpacing * scale);
        return new Point(width, GlyphHeight * scale);
    }

    public static void DrawCentered(SpriteBatch spriteBatch, Texture2D pixel, string text, Rectangle bounds, int scale, Color color)
    {
        Point size = MeasureString(text, scale);
        Vector2 position = new(
            bounds.Center.X - (size.X * 0.5f),
            bounds.Center.Y - (size.Y * 0.5f));

        DrawString(spriteBatch, pixel, text, position, scale, color);
    }

    public static void DrawLeft(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 position, int scale, Color color)
    {
        DrawString(spriteBatch, pixel, text, position, scale, color);
    }

    public static void DrawRight(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 position, int scale, Color color)
    {
        Point size = MeasureString(text, scale);
        Vector2 rightPos = new(position.X - size.X, position.Y);
        DrawString(spriteBatch, pixel, text, rightPos, scale, color);
    }

    public static void DrawString(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 position, int scale, Color color)
    {
        if (scale <= 0)
        {
            return;
        }

        int offsetX = 0;
        foreach (char character in text.ToUpperInvariant())
        {
            string[] glyph = Glyphs.TryGetValue(character, out string[] foundGlyph)
                ? foundGlyph
                : Glyphs[' '];

            for (int y = 0; y < GlyphHeight; y++)
            {
                for (int x = 0; x < GlyphWidth; x++)
                {
                    if (glyph[y][x] == '0')
                    {
                        continue;
                    }

                    spriteBatch.Draw(
                        pixel,
                        new Rectangle(
                            (int)position.X + offsetX + (x * scale),
                            (int)position.Y + (y * scale),
                            scale,
                            scale),
                        color);
                }
            }

            offsetX += (GlyphWidth + GlyphSpacing) * scale;
        }
    }
}
