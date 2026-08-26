#nullable enable
using System;
using System.Collections.Concurrent;

namespace ColorBlocks;

/// <summary>Marshal work back to the game thread. Steam API stays on main.</summary>
public static class MainThreadActions
{
    private static readonly ConcurrentQueue<Action> Immediate = new();
    private static readonly ConcurrentQueue<Action> Idle = new();

    public static void Post(Action action)
    {
        if (action is not null)
        {
            Immediate.Enqueue(action);
        }
    }

    public static void PostIdle(Action action)
    {
        if (action is not null)
        {
            Idle.Enqueue(action);
        }
    }

    public static void Pump(bool allowIdle)
    {
        Drain(Immediate);
        if (allowIdle)
        {
            Drain(Idle);
        }
    }

    private static void Drain(ConcurrentQueue<Action> queue)
    {
        while (queue.TryDequeue(out Action? action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Info("MainThread", $"Queued action failed: {ex.Message}");
            }
        }
    }
}
