#nullable enable
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>
/// UI glyph for a gameplay/menu action. Prefer <see cref="Label"/> when texture unavailable.
/// </summary>
public readonly struct InputGlyph
{
    public InputGlyph(string label, string? glyphPath = null, Texture2D? texture = null)
    {
        Label = label ?? string.Empty;
        GlyphPath = glyphPath;
        Texture = texture;
    }

    public string Label { get; }
    public string? GlyphPath { get; }
    public Texture2D? Texture { get; }

    public static InputGlyph Fallback(string label) => new(label);

    public override string ToString() => Label;
}
