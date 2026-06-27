using System;

namespace ColorBlocks;

/// <summary>
/// Global development tooling for the UI navigation system.
/// Toggled with F8 (overlay) and F9 (step focus). Has zero visual effect when disabled.
/// </summary>
public static class NavigationDebug
{
    public static bool Enabled { get; set; }

    /// <summary>Name of the scene currently being shown (set by the game shell).</summary>
    public static string CurrentScene { get; set; } = "";

    private static int _lastStepFrame = -1;
    private static int _frame;

    public static void BeginFrame()
    {
        _frame++;
    }

    /// <summary>
    /// Ensures a single F9 step is consumed by only one focus manager per frame,
    /// even when several managers update on the same frame.
    /// </summary>
    public static bool TryConsumeStep()
    {
        if (_lastStepFrame == _frame)
        {
            return false;
        }

        _lastStepFrame = _frame;
        return true;
    }

    public static void Log(string message)
    {
        Console.WriteLine(message);
    }
}
