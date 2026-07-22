#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Steamworks;

namespace ColorBlocks;

/// <summary>
/// Caches Steam Input action origins and converts them to UI glyphs.
/// Refresh only when layout/device callbacks fire or forced.
/// </summary>
public sealed class SteamInputGlyphProvider
{
    private readonly SteamInputManager _manager;
    private readonly Dictionary<GlyphCacheKey, InputGlyph> _glyphCache = new();
    private readonly Dictionary<string, string> _pathCache = new();
    private readonly Dictionary<string, Texture2D?> _textureCache = new();
    private readonly EInputActionOrigin[] _originsScratch = new EInputActionOrigin[Constants.STEAM_INPUT_MAX_ORIGINS];
    private GraphicsDevice? _graphicsDevice;
    private int _layoutVersion;
    private DateTime _lastLayoutRefreshUtc = DateTime.MinValue;

    public SteamInputGlyphProvider(SteamInputManager manager)
    {
        _manager = manager;
    }

    public int LayoutVersion => _layoutVersion;
    public DateTime LastLayoutRefreshUtc => _lastLayoutRefreshUtc;
    public int CachedGlyphCount => _glyphCache.Count;
    public string GlyphSource => _manager.IsInitialized ? "Steam Input" : "Fallback";

    public void BindGraphicsDevice(GraphicsDevice? device)
    {
        _graphicsDevice = device;
    }

    public void Invalidate()
    {
        _layoutVersion++;
        _lastLayoutRefreshUtc = DateTime.UtcNow;
        _glyphCache.Clear();
        // Keep texture path→texture cache; paths are stable per origin art file.
    }

    public InputGlyph GetGlyph(GameplayInputAction action, int localPlayerSlot)
    {
        string? steamAction = MapGameplayAction(action);
        if (steamAction is null)
        {
            return InputGlyph.Fallback(GamepadDefaults.GetDisplayName(action));
        }

        return GetGlyphBySteamName(steamAction, localPlayerSlot, GamepadDefaults.GetDisplayName(action));
    }

    public InputGlyph GetGlyphBySteamName(string steamActionName, int localPlayerSlot, string fallbackLabel)
    {
        if (!_manager.IsInitialized)
        {
            return InputGlyph.Fallback(fallbackLabel);
        }

        InputHandle_t handle = _manager.GetHandleForSlot(localPlayerSlot);
        if (handle.m_InputHandle == 0)
        {
            // Prefer any connected Steam controller for UI prompts.
            handle = _manager.GetPrimaryHandle();
            if (handle.m_InputHandle == 0)
            {
                return InputGlyph.Fallback(fallbackLabel);
            }
        }

        var key = new GlyphCacheKey(steamActionName, handle.m_InputHandle, _layoutVersion);
        if (_glyphCache.TryGetValue(key, out InputGlyph cached))
        {
            return cached;
        }

        InputGlyph glyph = BuildGlyph(handle, steamActionName, fallbackLabel);
        _glyphCache[key] = glyph;
        return glyph;
    }

    public string GetActionDisplayLabel(GameplayInputAction action, int localPlayerSlot)
    {
        return GetGlyph(action, localPlayerSlot).Label;
    }

    public void DumpOriginsToConsole()
    {
        if (!_manager.IsInitialized)
        {
            Console.WriteLine("[SteamInput] Glyph dump skipped — Steam Input disabled.");
            return;
        }

        Console.WriteLine($"[SteamInput] === Action Origins dump (layout v{_layoutVersion}) ===");
        for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
        {
            InputHandle_t handle = _manager.GetHandleForSlot(slot);
            if (handle.m_InputHandle == 0)
            {
                continue;
            }

            Console.WriteLine($"  Slot {slot} handle={handle.m_InputHandle} type={_manager.GetControllerType(slot)}");
            DumpDigital(handle, SteamInputActionNames.Jump);
            DumpDigital(handle, SteamInputActionNames.PullRope);
            DumpDigital(handle, SteamInputActionNames.Respawn);
            DumpDigital(handle, SteamInputActionNames.Pause);
            DumpDigital(handle, SteamInputActionNames.ColorRed);
            DumpDigital(handle, SteamInputActionNames.ColorGreen);
            DumpDigital(handle, SteamInputActionNames.ColorBlue);
            DumpAnalog(handle, SteamInputActionNames.Move);
        }

        Console.WriteLine("[SteamInput] === end dump ===");
    }

    private void DumpDigital(InputHandle_t handle, string actionName)
    {
        InputDigitalActionHandle_t action = _manager.GetDigitalHandle(actionName);
        InputActionSetHandle_t set = _manager.GameplayActionSet;
        int count = 0;
        try
        {
            count = SteamInput.GetDigitalActionOrigins(handle, set, action, _originsScratch);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    {actionName}: ERROR {ex.GetType().Name}");
            return;
        }

        if (count <= 0)
        {
            Console.WriteLine($"    {actionName}: (none)");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            EInputActionOrigin origin = _originsScratch[i];
            string label = SafeStringForOrigin(origin);
            Console.WriteLine($"    {actionName}[{i}] {origin} => {label}");
        }
    }

    private void DumpAnalog(InputHandle_t handle, string actionName)
    {
        InputAnalogActionHandle_t action = _manager.GetAnalogHandle(actionName);
        InputActionSetHandle_t set = _manager.GameplayActionSet;
        int count = 0;
        try
        {
            count = SteamInput.GetAnalogActionOrigins(handle, set, action, _originsScratch);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    {actionName}: ERROR {ex.GetType().Name}");
            return;
        }

        if (count <= 0)
        {
            Console.WriteLine($"    {actionName}: (none)");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            EInputActionOrigin origin = _originsScratch[i];
            string label = SafeStringForOrigin(origin);
            Console.WriteLine($"    {actionName}[{i}] {origin} => {label}");
        }
    }

    private InputGlyph BuildGlyph(InputHandle_t handle, string steamActionName, string fallbackLabel)
    {
        EInputActionOrigin origin = EInputActionOrigin.k_EInputActionOrigin_None;
        bool found = TryGetFirstDigitalOrigin(handle, steamActionName, out origin)
            || TryGetFirstAnalogOrigin(handle, steamActionName, out origin);

        if (!found || origin == EInputActionOrigin.k_EInputActionOrigin_None)
        {
            return InputGlyph.Fallback(fallbackLabel);
        }

        string label = SafeStringForOrigin(origin);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = fallbackLabel;
        }

        string? path = SafeGlyphPath(origin);
        Texture2D? texture = TryLoadTexture(path);
        return new InputGlyph(label, path, texture, fromSteam: true);
    }

    private bool TryGetFirstDigitalOrigin(InputHandle_t handle, string actionName, out EInputActionOrigin origin)
    {
        origin = EInputActionOrigin.k_EInputActionOrigin_None;
        InputDigitalActionHandle_t action = _manager.GetDigitalHandle(actionName);
        if (action.m_InputDigitalActionHandle == 0)
        {
            return false;
        }

        try
        {
            int count = SteamInput.GetDigitalActionOrigins(handle, _manager.GameplayActionSet, action, _originsScratch);
            if (count <= 0)
            {
                return false;
            }

            origin = _originsScratch[0];
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool TryGetFirstAnalogOrigin(InputHandle_t handle, string actionName, out EInputActionOrigin origin)
    {
        origin = EInputActionOrigin.k_EInputActionOrigin_None;
        InputAnalogActionHandle_t action = _manager.GetAnalogHandle(actionName);
        if (action.m_InputAnalogActionHandle == 0)
        {
            return false;
        }

        try
        {
            int count = SteamInput.GetAnalogActionOrigins(handle, _manager.GameplayActionSet, action, _originsScratch);
            if (count <= 0)
            {
                return false;
            }

            origin = _originsScratch[0];
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string SafeStringForOrigin(EInputActionOrigin origin)
    {
        try
        {
            return SteamInput.GetStringForActionOrigin(origin) ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string? SafeGlyphPath(EInputActionOrigin origin)
    {
        try
        {
            string path = SteamInput.GetGlyphPNGForActionOrigin(
                origin,
                ESteamInputGlyphSize.k_ESteamInputGlyphSize_Small,
                0);
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Texture2D? TryLoadTexture(string? path)
    {
        if (_graphicsDevice is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (_textureCache.TryGetValue(path, out Texture2D? cached))
        {
            return cached;
        }

        try
        {
            if (!System.IO.File.Exists(path))
            {
                _textureCache[path] = null;
                return null;
            }

            using var stream = System.IO.File.OpenRead(path);
            Texture2D texture = Texture2D.FromStream(_graphicsDevice, stream);
            _textureCache[path] = texture;
            _pathCache[path] = path;
            return texture;
        }
        catch (Exception)
        {
            _textureCache[path] = null;
            return null;
        }
    }

    public static string? MapGameplayAction(GameplayInputAction action) => action switch
    {
        GameplayInputAction.Jump => SteamInputActionNames.Jump,
        GameplayInputAction.PullRope => SteamInputActionNames.PullRope,
        GameplayInputAction.Respawn => SteamInputActionNames.Respawn,
        GameplayInputAction.Red => SteamInputActionNames.ColorRed,
        GameplayInputAction.Green => SteamInputActionNames.ColorGreen,
        GameplayInputAction.Blue => SteamInputActionNames.ColorBlue,
        GameplayInputAction.MoveLeft => SteamInputActionNames.Move,
        GameplayInputAction.MoveRight => SteamInputActionNames.Move,
        GameplayInputAction.FastFall => SteamInputActionNames.Move,
        _ => null
    };

    private readonly record struct GlyphCacheKey(string Action, ulong Handle, int LayoutVersion);
}
