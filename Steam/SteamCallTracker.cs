#nullable enable
using System;
using System.Collections.Generic;
using Steamworks;

namespace ColorBlocks;

/// <summary>
/// Keeps Steamworks CallResult instances alive until their async result fires.
/// Results are dispatched on the main thread by SteamAPI.RunCallbacks (pumped every frame).
/// </summary>
public static class SteamCallTracker
{
    private static readonly HashSet<object> Pending = new();

    public static void Track<T>(SteamAPICall_t apiCall, Action<T, bool> handler) where T : struct
    {
        if (apiCall == SteamAPICall_t.Invalid)
        {
            handler(default, true);
            return;
        }

        CallResult<T>? callResult = null;
        callResult = CallResult<T>.Create((result, ioFailure) =>
        {
            if (callResult is not null)
            {
                Pending.Remove(callResult);
                callResult.Dispose();
            }

            try
            {
                handler(result, ioFailure);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Info("Steam", $"Async call handler failed: {ex.Message}");
            }
        });

        Pending.Add(callResult);
        callResult.Set(apiCall);
    }
}
