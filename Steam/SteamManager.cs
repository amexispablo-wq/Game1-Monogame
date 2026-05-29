#nullable enable
using System;
using System.Runtime.InteropServices;
using Steamworks;

namespace ColorBlocks;

public sealed class SteamManager : IDisposable
{
    private const string UnavailableText = "Unavailable";

    private bool _isDisposed;

    public bool IsInitialized { get; private set; }
    public string Username { get; private set; } = UnavailableText;
    public string SteamId { get; private set; } = UnavailableText;
    public bool IsOverlayEnabled { get; private set; }
    public string Status { get; private set; } = "Not initialized";

    public void Initialize()
    {
        if (IsInitialized || _isDisposed)
        {
            return;
        }

        try
        {
            IsInitialized = SteamAPI.Init();
            if (!IsInitialized)
            {
                Status = "Steam unavailable";
                ResetUserInfo();
                return;
            }

            RefreshSteamInfo();
            Status = "Steam initialized";
        }
        catch (Exception ex) when (IsRecoverableSteamException(ex))
        {
            Status = $"Steam unavailable: {ex.GetType().Name}";
            IsInitialized = false;
            ResetUserInfo();
        }
    }

    public void RunCallbacks()
    {
        if (!IsInitialized)
        {
            return;
        }

        try
        {
            SteamAPI.RunCallbacks();
            RefreshSteamInfo();
        }
        catch (Exception ex) when (IsRecoverableSteamException(ex))
        {
            Status = $"Steam callbacks unavailable: {ex.GetType().Name}";
            IsInitialized = false;
            ResetUserInfo();
        }
    }

    public void Shutdown()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (!IsInitialized)
        {
            return;
        }

        try
        {
            SteamAPI.Shutdown();
            Status = "Steam shutdown";
        }
        catch (Exception ex) when (IsRecoverableSteamException(ex))
        {
            Status = $"Steam shutdown failed: {ex.GetType().Name}";
        }
        finally
        {
            IsInitialized = false;
            ResetUserInfo();
        }
    }

    public void Dispose()
    {
        Shutdown();
    }

    private void RefreshSteamInfo()
    {
        Username = SteamFriends.GetPersonaName();
        SteamId = SteamUser.GetSteamID().m_SteamID.ToString();
        IsOverlayEnabled = SteamUtils.IsOverlayEnabled();
    }

    private void ResetUserInfo()
    {
        Username = UnavailableText;
        SteamId = UnavailableText;
        IsOverlayEnabled = false;
    }

    private static bool IsRecoverableSteamException(Exception exception)
    {
        return exception is DllNotFoundException
            or BadImageFormatException
            or EntryPointNotFoundException
            or SEHException
            or TypeInitializationException;
    }
}
