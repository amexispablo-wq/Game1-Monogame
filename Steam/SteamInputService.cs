#nullable enable
using System;
using System.IO;
using Steamworks;

namespace ColorBlocks;

/// <summary>
/// Steam Input bootstrap for legacy gamepad games (MonoGame GamePad / Xbox layout).
/// Maps DualShock 4, DualSense, and other pads to Xbox-style buttons when Steam is running.
/// </summary>
public sealed class SteamInputService
{
    private readonly SteamManager _steam;
    private bool _isInitialized;

    public SteamInputService(SteamManager steam)
    {
        _steam = steam;
    }

    public bool IsInitialized => _isInitialized;

    public void Initialize()
    {
        if (_isInitialized || !_steam.IsInitialized)
        {
            return;
        }

        try
        {
            if (!SteamInput.Init(false))
            {
                return;
            }

            string manifestPath = Path.Combine(AppContext.BaseDirectory, "steam_input_manifest.vdf");
            if (File.Exists(manifestPath))
            {
                SteamInput.SetInputActionManifestFilePath(manifestPath);
            }

            _isInitialized = true;
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            _isInitialized = false;
        }
    }

    public void RunFrame()
    {
        if (!_isInitialized)
        {
            return;
        }

        try
        {
            SteamInput.RunFrame();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            _isInitialized = false;
        }
    }

    public void Shutdown()
    {
        if (!_isInitialized)
        {
            return;
        }

        try
        {
            SteamInput.Shutdown();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            // Ignore shutdown failures.
        }
        finally
        {
            _isInitialized = false;
        }
    }

    public ESteamInputType GetControllerType(int gamepadIndex)
    {
        if (!_isInitialized || gamepadIndex < 0 || gamepadIndex >= InputManager.MaxLocalPlayers)
        {
            return ESteamInputType.k_ESteamInputType_Unknown;
        }

        try
        {
            InputHandle_t handle = SteamInput.GetControllerForGamepadIndex(gamepadIndex);
            if (handle.m_InputHandle == 0)
            {
                return ESteamInputType.k_ESteamInputType_Unknown;
            }

            return SteamInput.GetInputTypeForHandle(handle);
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            return ESteamInputType.k_ESteamInputType_Unknown;
        }
    }

    public string GetControllerLabel(int gamepadIndex)
    {
        return FormatControllerType(GetControllerType(gamepadIndex));
    }

    public static string FormatControllerType(ESteamInputType type) => type switch
    {
        ESteamInputType.k_ESteamInputType_PS4Controller => "DualShock 4",
        ESteamInputType.k_ESteamInputType_PS5Controller => "DualSense",
        ESteamInputType.k_ESteamInputType_XBox360Controller => "Xbox 360",
        ESteamInputType.k_ESteamInputType_XBoxOneController => "Xbox One",
        ESteamInputType.k_ESteamInputType_SwitchProController => "Switch Pro",
        ESteamInputType.k_ESteamInputType_SteamDeckController => "Steam Deck",
        ESteamInputType.k_ESteamInputType_SteamController => "Steam Controller",
        ESteamInputType.k_ESteamInputType_GenericGamepad => "Generic Gamepad",
        ESteamInputType.k_ESteamInputType_PS3Controller => "DualShock 3",
        _ => "Gamepad"
    };

    private static bool IsRecoverableException(Exception exception) =>
        exception is DllNotFoundException
            or BadImageFormatException
            or EntryPointNotFoundException
            or TypeInitializationException;
}
