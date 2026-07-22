#nullable enable
using System;
using System.IO;
using System.Threading;

namespace ColorBlocks;

/// <summary>
/// Session recorder: one log file per game launch under %LocalAppData%\Color Blocks\Logs\.
/// Every event gets a sequential EventId so host and client logs can be compared line-by-line.
/// Line format: [Timestamp][SessionId][EventId][Thread][Severity][Subsystem] message
/// </summary>
public static class DiagnosticsLog
{
    public const string DefaultSessionId = "CB-LOCAL";
    private const int MaxLogFiles = 20;

    private static readonly object Sync = new();
    private static StreamWriter? _writer;
    private static int _eventId;
    private static int _mainThreadId = -1;

    public static string SessionId { get; private set; } = DefaultSessionId;
    public static string LogFilePath { get; private set; } = string.Empty;
    public static int LastEventId => _eventId;

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_writer is not null)
            {
                return;
            }

            _mainThreadId = Thread.CurrentThread.ManagedThreadId;

            try
            {
                Directory.CreateDirectory(UserDataPaths.Logs);
                LogFilePath = Path.Combine(
                    UserDataPaths.Logs,
                    $"Session_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                var stream = new FileStream(LogFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(stream) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Diagnostics] Log file unavailable: {ex.Message}");
                return;
            }
        }

        PruneOldLogs();
        Info("Session", $"Log opened {LogFilePath}");
    }

    /// <summary>Deterministic session id shared by every peer of a lobby: CB-yyyy-MM-dd-XXXXXXXX.</summary>
    public static string CreateSessionId(ulong lobbyId) =>
        $"CB-{DateTime.UtcNow:yyyy-MM-dd}-{(uint)(lobbyId ^ (lobbyId >> 32)):X8}";

    public static void SetSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId == SessionId)
        {
            return;
        }

        SessionId = sessionId;
        Info("Session", $"SessionId={sessionId}");
    }

    public static void ResetSessionId()
    {
        if (SessionId == DefaultSessionId)
        {
            return;
        }

        Info("Session", $"SessionId cleared (was {SessionId})");
        SessionId = DefaultSessionId;
    }

    /// <summary>File-only write. MultiplayerDebug mirrors to console itself.</summary>
    public static void Write(string severity, string subsystem, string message)
    {
        lock (Sync)
        {
            _eventId++;
            if (_writer is null)
            {
                return;
            }

            int threadId = Thread.CurrentThread.ManagedThreadId;
            string thread = threadId == _mainThreadId ? "Main" : $"T{threadId}";
            try
            {
                _writer.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}][{SessionId}][{_eventId:0000}][{thread}][{severity}][{subsystem}] {message}");
            }
            catch
            {
                // Never let diagnostics writing break the game.
            }
        }
    }

    /// <summary>Write to log file and console (for events not routed through MultiplayerDebug).</summary>
    public static void Info(string subsystem, string message)
    {
        Write("INFO", subsystem, message);
        Console.WriteLine($"[DIAG][{subsystem}] {message}");
    }

    public static void Warn(string subsystem, string message)
    {
        Write("WARN", subsystem, message);
        Console.WriteLine($"[DIAG][WARN][{subsystem}] {message}");
    }

    public static void Error(string subsystem, string message)
    {
        Write("ERROR", subsystem, message);
        Console.WriteLine($"[DIAG][ERROR][{subsystem}] {message}");
    }

    private static void PruneOldLogs()
    {
        try
        {
            string[] files = Directory.GetFiles(UserDataPaths.Logs, "Session_*.log");
            if (files.Length <= MaxLogFiles)
            {
                return;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            int removeCount = files.Length - MaxLogFiles;
            for (int i = 0; i < removeCount; i++)
            {
                if (!string.Equals(files[i], LogFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(files[i]);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Diagnostics] Log prune failed: {ex.Message}");
        }
    }
}
