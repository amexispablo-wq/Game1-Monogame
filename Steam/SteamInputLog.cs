#nullable enable
using System;

namespace ColorBlocks;

/// <summary>
/// Verbose Steam Input logging. Console + Debug output, plus a small ring buffer
/// so the F3 diagnostic panel can show recent events without reading the console.
/// </summary>
public static class SteamInputLog
{
    public const int Capacity = 14;

    public static bool Verbose { get; set; } = true;

    private static readonly string[] Ring = new string[Capacity];
    private static int _next;
    private static int _count;

    public static int Count => _count;

    public static void Log(string message)
    {
        if (!Verbose)
        {
            return;
        }

        string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
        Console.WriteLine($"[SteamInput] {line}");
        System.Diagnostics.Debug.WriteLine($"[SteamInput] {line}");
        DiagnosticsLog.Info("SteamInput", message);

        Ring[_next] = line;
        _next = (_next + 1) % Capacity;
        if (_count < Capacity)
        {
            _count++;
        }
    }

    /// <summary>Oldest-first. Index 0 = oldest retained line.</summary>
    public static string GetLine(int index)
    {
        if (index < 0 || index >= _count)
        {
            return string.Empty;
        }

        int start = (_next - _count + Capacity) % Capacity;
        return Ring[(start + index) % Capacity] ?? string.Empty;
    }
}
