#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>Formats current keyboard or gamepad bindings for signs and HUD.</summary>
public static class BindingDisplay
{
    public static bool UseGamepadBindings(PartyInputSource lastUsed) =>
        lastUsed == PartyInputSource.Gamepad;

    public static string ForAction(GameplayInputAction action, bool useGamepad) =>
        ForAction(action, useGamepad, GamepadDefaults.DetectActiveLabelFamily());

    public static string ForAction(GameplayInputAction action, bool useGamepad, GamepadLabelFamily family)
    {
        if (useGamepad)
        {
            string pad = GamepadDefaults.GetGamepadDisplayName(
                action,
                SettingsManager.CurrentSettings.GamepadBindings,
                family);
            if (IsUnboundDisplay(pad))
            {
                return "NONE";
            }

            return SanitizeForBitmapFont(pad);
        }

        string keyName = ResolveKeyName(action.ToString(), SettingsManager.CurrentSettings.Keybindings);
        return SanitizeForBitmapFont(FormatKeyboardName(keyName));
    }

    public static string ForActionName(string actionName, bool useGamepad) =>
        ForActionName(actionName, useGamepad, GamepadDefaults.DetectActiveLabelFamily());

    public static string ForActionName(string actionName, bool useGamepad, GamepadLabelFamily family)
    {
        if (Enum.TryParse(actionName, ignoreCase: true, out GameplayInputAction action))
        {
            return ForAction(action, useGamepad, family);
        }

        return SanitizeForBitmapFont(actionName);
    }

    public static string FormatKeyboardName(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)
            || string.Equals(keyName, "None", StringComparison.OrdinalIgnoreCase)
            || string.Equals(keyName, "UNBOUND", StringComparison.OrdinalIgnoreCase))
        {
            return "NONE";
        }

        if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1]))
        {
            return keyName[1].ToString();
        }

        return keyName switch
        {
            "Space" => "SPACE",
            "LeftShift" => "L SHIFT",
            "RightShift" => "R SHIFT",
            "LeftControl" => "L CTRL",
            "RightControl" => "R CTRL",
            "LeftAlt" => "L ALT",
            "RightAlt" => "R ALT",
            _ => keyName
        };
    }

    public static string SanitizeForBitmapFont(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || IsUnboundDisplay(text))
        {
            return "NONE";
        }

        var sb = new StringBuilder(text.Length);
        foreach (char c in text.ToUpperInvariant())
        {
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is ' ' or '<' or '>' or '^' or ':' or '%' or '/')
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

    private static bool IsUnboundDisplay(string text) =>
        string.IsNullOrWhiteSpace(text)
        || text == "—"
        || string.Equals(text, "None", StringComparison.OrdinalIgnoreCase)
        || string.Equals(text, "UNBOUND", StringComparison.OrdinalIgnoreCase);

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
            "RestartLevel" => Keys.F.ToString(),
            "PullRope" => Keys.Space.ToString(),
            "FastFall" => Keys.S.ToString(),
            "Red" => Keys.J.ToString(),
            "Blue" => Keys.L.ToString(),
            "Green" => Keys.K.ToString(),
            _ => action
        };
    }
}
