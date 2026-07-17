#nullable enable
using System;
using Steamworks;

namespace ColorBlocks;

public sealed class SteamHaptics : IHaptics
{
    private readonly SteamInputManager _steamInput;

    public SteamHaptics(SteamInputManager steamInput)
    {
        _steamInput = steamInput;
    }

    public void Play(HapticEvent hapticEvent, int localPlayerSlot = 0)
    {
        if (!_steamInput.IsInitialized)
        {
            return;
        }

        InputHandle_t handle = _steamInput.GetHandleForSlot(localPlayerSlot);
        if (handle.m_InputHandle == 0)
        {
            return;
        }

        (ushort left, ushort right) = Resolve(hapticEvent);
        try
        {
            SteamInput.TriggerVibration(handle, left, right);
        }
        catch (Exception)
        {
        }
    }

    public void Stop(int localPlayerSlot = 0)
    {
        if (!_steamInput.IsInitialized)
        {
            return;
        }

        InputHandle_t handle = _steamInput.GetHandleForSlot(localPlayerSlot);
        if (handle.m_InputHandle == 0)
        {
            return;
        }

        try
        {
            SteamInput.TriggerVibration(handle, 0, 0);
        }
        catch (Exception)
        {
        }
    }

    private static (ushort Left, ushort Right) Resolve(HapticEvent hapticEvent) => hapticEvent switch
    {
        HapticEvent.SmallImpact => ((ushort)4000, (ushort)4000),
        HapticEvent.MediumImpact => ((ushort)12000, (ushort)12000),
        HapticEvent.LargeImpact => ((ushort)28000, (ushort)28000),
        HapticEvent.Checkpoint => ((ushort)8000, (ushort)4000),
        HapticEvent.Goal => ((ushort)22000, (ushort)22000),
        HapticEvent.PullRope => ((ushort)6000, (ushort)16000),
        _ => ((ushort)8000, (ushort)8000)
    };
}
