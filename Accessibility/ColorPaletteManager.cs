using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public static class ColorPaletteManager
{
    private static ColorMode _currentMode = ColorMode.Normal;

    public static ColorMode CurrentMode => _currentMode;

    public static void SetMode(ColorMode mode)
    {
        if (_currentMode == mode)
        {
            return;
        }

        _currentMode = mode;
        LevelPreviewManager.InvalidateCache();
    }

    public static void ApplySettings(ColorMode mode) => SetMode(mode);

    public static Color Get(ColorType type) =>
        Palettes[_currentMode].TryGetValue(type, out Color color) ? color : Color.White;

    public static Color GetGameColor(GameColor gameColor) => gameColor switch
    {
        GameColor.Red => Get(ColorType.Red),
        GameColor.Green => Get(ColorType.Green),
        GameColor.Blue => Get(ColorType.Blue),
        _ => Get(ColorType.White)
    };

    public static Color MixGameColors(IReadOnlyList<GameColor> colors)
    {
        bool hasRed = false;
        bool hasGreen = false;
        bool hasBlue = false;

        foreach (GameColor color in colors)
        {
            switch (color)
            {
                case GameColor.Red:
                    hasRed = true;
                    break;
                case GameColor.Green:
                    hasGreen = true;
                    break;
                case GameColor.Blue:
                    hasBlue = true;
                    break;
            }
        }

        return MixFlags(hasRed, hasGreen, hasBlue);
    }

    public static Color MixFlags(bool hasRed, bool hasGreen, bool hasBlue)
    {
        if (hasRed && hasGreen && hasBlue)
        {
            return Get(ColorType.White);
        }

        if (hasRed && hasGreen)
        {
            return Get(ColorType.Yellow);
        }

        if (hasRed && hasBlue)
        {
            return Get(ColorType.Magenta);
        }

        if (hasGreen && hasBlue)
        {
            return Get(ColorType.Cyan);
        }

        if (hasRed)
        {
            return Get(ColorType.Red);
        }

        if (hasGreen)
        {
            return Get(ColorType.Green);
        }

        if (hasBlue)
        {
            return Get(ColorType.Blue);
        }

        return Get(ColorType.White);
    }

    public static Color GetCheckpointColor(bool active) =>
        active ? Brighten(Get(ColorType.Checkpoint), 0.22f) : Get(ColorType.Checkpoint);

    public static Color GetCheckpointShadow(bool active) =>
        active ? Darken(Get(ColorType.Checkpoint), 0.12f) : Darken(Get(ColorType.Checkpoint), 0.28f);

    public static Color GetGoalShadow() => Darken(Get(ColorType.Goal), 0.32f);

    public static Color GetLaunchPadBright() => Brighten(Get(ColorType.LaunchPad), 0.35f);

    public static Color GetLaunchPadBase() => Darken(Get(ColorType.LaunchPad), 0.55f);

    public static Color GetLaunchPadParticle() => Brighten(Get(ColorType.LaunchPad), 0.18f);

    public static Color GetLaunchPadArrow() => Get(ColorType.Yellow);

    public static string FormatMode(ColorMode mode) => mode switch
    {
        ColorMode.Normal => "Normal",
        ColorMode.Protanopia => "Protanopia",
        ColorMode.Deuteranopia => "Deuteranopia",
        ColorMode.Tritanopia => "Tritanopia",
        ColorMode.HighContrast => "High Contrast",
        _ => mode.ToString()
    };

    public static ColorMode ParseMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ColorMode.Normal;
        }

        return Enum.TryParse(value, ignoreCase: true, out ColorMode parsed)
            ? parsed
            : ColorMode.Normal;
    }

    private static Color Brighten(Color color, float amount)
    {
        amount = MathHelper.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)Math.Clamp(color.R + ((255 - color.R) * amount), 0, 255),
            (byte)Math.Clamp(color.G + ((255 - color.G) * amount), 0, 255),
            (byte)Math.Clamp(color.B + ((255 - color.B) * amount), 0, 255),
            color.A);
    }

    private static Color Darken(Color color, float amount)
    {
        amount = MathHelper.Clamp(amount, 0f, 1f);
        float factor = 1f - amount;
        return new Color(
            (byte)(color.R * factor),
            (byte)(color.G * factor),
            (byte)(color.B * factor),
            color.A);
    }

    private static Color C(byte r, byte g, byte b) => new(r, g, b);

    private static readonly Dictionary<ColorMode, Dictionary<ColorType, Color>> Palettes =
        new()
        {
            [ColorMode.Normal] = CreatePalette(
                red: C(224, 64, 64),
                green: C(72, 184, 96),
                blue: C(64, 128, 224),
                yellow: C(255, 235, 64),
                magenta: C(224, 64, 192),
                cyan: C(64, 200, 224),
                white: Color.White,
                black: Color.Black,
                rope: C(210, 180, 140),
                checkpoint: C(255, 160, 48),
                goal: C(255, 207, 72),
                launchPad: C(80, 210, 255)),
            [ColorMode.Protanopia] = CreatePalette(
                red: C(230, 159, 0),
                green: C(0, 158, 115),
                blue: C(0, 114, 178),
                yellow: C(240, 228, 66),
                magenta: C(204, 121, 167),
                cyan: C(86, 180, 233),
                white: Color.White,
                black: Color.Black,
                rope: C(210, 180, 140),
                checkpoint: C(240, 180, 60),
                goal: C(240, 228, 66),
                launchPad: C(86, 180, 233)),
            [ColorMode.Deuteranopia] = CreatePalette(
                red: C(213, 94, 0),
                green: C(0, 158, 115),
                blue: C(86, 180, 233),
                yellow: C(240, 228, 66),
                magenta: C(204, 121, 167),
                cyan: C(0, 114, 178),
                white: Color.White,
                black: Color.Black,
                rope: C(200, 170, 130),
                checkpoint: C(240, 200, 72),
                goal: C(240, 228, 66),
                launchPad: C(0, 114, 178)),
            [ColorMode.Tritanopia] = CreatePalette(
                red: C(227, 26, 28),
                green: C(51, 160, 44),
                blue: C(106, 61, 154),
                yellow: C(255, 127, 14),
                magenta: C(231, 138, 195),
                cyan: C(166, 216, 84),
                white: Color.White,
                black: Color.Black,
                rope: C(210, 180, 140),
                checkpoint: C(255, 150, 64),
                goal: C(255, 190, 72),
                launchPad: C(106, 61, 154)),
            [ColorMode.HighContrast] = CreatePalette(
                red: C(255, 0, 0),
                green: C(0, 255, 0),
                blue: C(0, 0, 255),
                yellow: C(255, 255, 0),
                magenta: C(255, 20, 147),
                cyan: C(0, 255, 255),
                white: Color.White,
                black: Color.Black,
                rope: C(210, 180, 140),
                checkpoint: C(255, 165, 0),
                goal: C(255, 255, 0),
                launchPad: C(0, 255, 255))
        };

    private static Dictionary<ColorType, Color> CreatePalette(
        Color red,
        Color green,
        Color blue,
        Color yellow,
        Color magenta,
        Color cyan,
        Color white,
        Color black,
        Color rope,
        Color checkpoint,
        Color goal,
        Color launchPad) =>
        new()
        {
            [ColorType.Red] = red,
            [ColorType.Green] = green,
            [ColorType.Blue] = blue,
            [ColorType.Yellow] = yellow,
            [ColorType.Magenta] = magenta,
            [ColorType.Cyan] = cyan,
            [ColorType.White] = white,
            [ColorType.Black] = black,
            [ColorType.Rope] = rope,
            [ColorType.Checkpoint] = checkpoint,
            [ColorType.Goal] = goal,
            [ColorType.LaunchPad] = launchPad
        };
}
