#nullable enable
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Audio;

namespace ColorBlocks;

/// <summary>
/// Follows Windows default playback device changes by reopening MonoGame's OpenAL device
/// via ALC_SOFT_reopen_device (alcReopenDeviceSOFT). Without this, SFX/music stay on the
/// device that was active at launch.
/// </summary>
public sealed class AudioOutputHotSwap
{
    private const float PollIntervalSeconds = 0.5f;

    private string? _lastEndpointId;
    private float _pollTimer;
    private bool _reopenResolved;
    private bool _reopenAvailable;
    private AlcReopenDeviceSOFT? _reopen;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte AlcReopenDeviceSOFT(IntPtr device, IntPtr deviceName, IntPtr attribs);

    [DllImport("openal", EntryPoint = "alcGetProcAddress", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr AlcGetProcAddress(IntPtr device, string funcName);

    [DllImport("openal", EntryPoint = "alcIsExtensionPresent", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern byte AlcIsExtensionPresent(IntPtr device, string extName);

    public void Update(float dt)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _pollTimer -= dt;
        if (_pollTimer > 0f)
        {
            return;
        }

        _pollTimer = PollIntervalSeconds;
        string? endpointId = WindowsDefaultAudioEndpoint.TryGetDefaultRenderId();
        if (string.IsNullOrEmpty(endpointId))
        {
            return;
        }

        if (_lastEndpointId is null)
        {
            _lastEndpointId = endpointId;
            return;
        }

        if (string.Equals(_lastEndpointId, endpointId, StringComparison.Ordinal))
        {
            return;
        }

        _lastEndpointId = endpointId;
        if (TryReopenDefaultDevice())
        {
            DiagnosticsLog.Info("Audio", $"Output device changed → reopened OpenAL (endpoint={endpointId})");
        }
    }

    private bool TryReopenDefaultDevice()
    {
        if (!TryResolveReopen(out IntPtr device) || _reopen is null || device == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            // null deviceName → system default (speakers ↔ headphones, etc.)
            return _reopen(device, IntPtr.Zero, IntPtr.Zero) != 0;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Audio", $"alcReopenDeviceSOFT failed: {ex.Message}");
            return false;
        }
    }

    private bool TryResolveReopen(out IntPtr device)
    {
        device = TryGetOpenALDevice();
        if (device == IntPtr.Zero)
        {
            return false;
        }

        if (_reopenResolved)
        {
            return _reopenAvailable;
        }

        _reopenResolved = true;
        try
        {
            if (AlcIsExtensionPresent(device, "ALC_SOFT_reopen_device") == 0)
            {
                DiagnosticsLog.Info("Audio", "ALC_SOFT_reopen_device missing — audio device hot-swap unavailable");
                return false;
            }

            IntPtr proc = AlcGetProcAddress(device, "alcReopenDeviceSOFT");
            if (proc == IntPtr.Zero)
            {
                DiagnosticsLog.Info("Audio", "alcReopenDeviceSOFT not found");
                return false;
            }

            _reopen = Marshal.GetDelegateForFunctionPointer<AlcReopenDeviceSOFT>(proc);
            _reopenAvailable = true;
            return true;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Audio", $"OpenAL reopen resolve failed: {ex.Message}");
            return false;
        }
    }

    private static IntPtr TryGetOpenALDevice()
    {
        try
        {
            // Touch MasterVolume so OpenALSoundController initializes if needed.
            _ = SoundEffect.MasterVolume;

            Type? controllerType = typeof(SoundEffect).Assembly.GetType(
                "Microsoft.Xna.Framework.Audio.OpenALSoundController");
            if (controllerType is null)
            {
                return IntPtr.Zero;
            }

            PropertyInfo? instanceProp = controllerType.GetProperty(
                "Instance",
                BindingFlags.Public | BindingFlags.Static);
            object? instance = instanceProp?.GetValue(null);
            if (instance is null)
            {
                return IntPtr.Zero;
            }

            FieldInfo? deviceField = controllerType.GetField(
                "_device",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (deviceField?.GetValue(instance) is IntPtr device)
            {
                return device;
            }
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("Audio", $"OpenAL device reflect failed: {ex.Message}");
        }

        return IntPtr.Zero;
    }
}

/// <summary>Minimal WASAPI default-render endpoint id (no NAudio dependency).</summary>
internal static class WindowsDefaultAudioEndpoint
{
    private const int EDataFlowRender = 0;
    private const int ERoleMultimedia = 1;

    public static string? TryGetDefaultRenderId()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            int hr = enumerator.GetDefaultAudioEndpoint(EDataFlowRender, ERoleMultimedia, out device);
            if (hr < 0 || device is null)
            {
                return null;
            }

            hr = device.GetId(out string id);
            return hr < 0 ? null : id;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }

            if (enumerator is not null)
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        // Vtable order must match mmdeviceapi.h
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IntPtr device);

        [PreserveSig]
        int RegisterEndpointNotificationCallback(IntPtr client);

        [PreserveSig]
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

        [PreserveSig]
        int OpenPropertyStore(int stgmAccess, out IntPtr properties);

        [PreserveSig]
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetState(out int state);
    }
}
