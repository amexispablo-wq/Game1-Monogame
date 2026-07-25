#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorBlocks;

/// <summary>
/// Options > Debug > Export Diagnostics. Packs logs, build info, settings, replay metadata,
/// Steam Input diagnostics, network diagnostics and a last-session summary into one zip.
/// </summary>
public static class DiagnosticsExporter
{
    public static string ExportDirectory => Path.Combine(UserDataPaths.Root, "Diagnostics");

    /// <summary>Returns the created zip path, or throws with a readable message.</summary>
    public static string Export(InputManager? input = null)
    {
        Directory.CreateDirectory(ExportDirectory);
        string zipPath = Path.Combine(ExportDirectory, $"Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        DiagnosticsLog.Info("Export", $"Export diagnostics -> {zipPath}");

        using (ZipArchive zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddLogs(zip);
            AddText(zip, "BuildInfo.txt", string.Join(Environment.NewLine, BuildInfo.Current.DescribeLines()));
            TryAddFile(zip, Path.Combine(AppContext.BaseDirectory, "Content", "version.json"), "version.json");
            TryAddFile(zip, UserDataPaths.SettingsFile, "Settings/settings.json");
            AddText(zip, "ReplayMetadata.txt", BuildReplayMetadata());
            AddText(zip, "SteamInputDiagnostics.txt", BuildSteamInputDiagnostics(input));
            AddText(zip, "GamepadDiagnostics.txt", BuildGamepadDiagnostics(input));
            AddText(zip, "InputEdgeRing.txt", InputDiagnostics.BuildEdgeRingText());
            AddText(zip, "PhantomInputSummary.txt", InputDiagnostics.BuildPhantomInputSummary(input));
            AddText(zip, "NetworkDiagnostics.txt", BuildNetworkDiagnostics());
            AddText(zip, "LastSessionSummary.txt", BuildLastSessionSummary());
            AddText(zip, "FRIEND_PAD_NOTES.txt", BuildFriendPadNotes());
        }

        DiagnosticsLog.Info("Export", "Export diagnostics complete");
        return zipPath;
    }

    private static void AddLogs(ZipArchive zip)
    {
        if (!Directory.Exists(UserDataPaths.Logs))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(UserDataPaths.Logs))
        {
            TryAddFile(zip, file, $"Logs/{Path.GetFileName(file)}");
        }
    }

    private static string BuildReplayMetadata()
    {
        StringBuilder sb = new();
        sb.AppendLine($"Replays root: {UserDataPaths.Replays}");
        if (!Directory.Exists(UserDataPaths.Replays))
        {
            sb.AppendLine("(no replays folder)");
            return sb.ToString();
        }

        foreach (string file in Directory.GetFiles(UserDataPaths.Replays, "*", SearchOption.AllDirectories))
        {
            FileInfo info = new(file);
            string relative = Path.GetRelativePath(UserDataPaths.Replays, file);
            sb.AppendLine($"{relative} | {info.Length} bytes | modified {info.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss}Z");
        }

        return sb.ToString();
    }

    private static string BuildSteamInputDiagnostics(InputManager? input)
    {
        List<string> lines = new()
        {
            $"steam_input_manifest.vdf SHA256: {SessionDiagnostics.SteamInputManifestHash}",
            $"controller_gamepad.vdf SHA256  : {SessionDiagnostics.ControllerConfigHash}",
            ""
        };

        if (input is not null)
        {
            lines.AddRange(input.BuildInputDiagnosticsSnapshot());
            lines.Add("");
        }
        else
        {
            lines.Add("(InputManager not passed to Export — live snapshot unavailable)");
            lines.Add("");
        }

        lines.Add("=== RECENT SteamInputLog RING ===");
        if (SteamInputLog.Count == 0)
        {
            lines.Add("(empty)");
        }
        else
        {
            for (int i = 0; i < SteamInputLog.Count; i++)
            {
                lines.Add(SteamInputLog.GetLine(i));
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildGamepadDiagnostics(InputManager? input)
    {
        if (input is null)
        {
            return "InputManager not passed to Export.";
        }

        return InputDiagnostics.BuildGamepadOnlyText(input);
    }

    private static string BuildFriendPadNotes() =>
        string.Join(Environment.NewLine, new[]
        {
            "Friend pad / phantom input troubleshooting",
            "==========================================",
            "1. Open PhantomInputSummary.txt first — verdict hint + EDGE / EDGE_SUPPRESSED / APPLY counts.",
            "2. Ghost overlays are visual only — they do NOT inject Jump/Respawn/Color.",
            "3. Search session log for: EDGE_SUPPRESSED | EDGE ... Respawn | APPLY Respawn | SUSPICIOUS",
            "4. EDGE_SUPPRESSED Jump/Respawn = Soft Claim thrash almost injected phantom (fix blocked it).",
            "5. EDGE src=Keyboard bind=R → MonoGame R rising edge (not Soft Claim). Check debounce lines.",
            "6. EDGE src=GamepadSoft softSec=<1.5 unstable=True → Soft Claim fallthrough digitals.",
            "7. APPLY Respawn without EDGE → latch/UI (pause menu / death), not input EDGE.",
            "8. If Steam handle live=no and XInput connected=false → soft-claim / hollow pad.",
            "   Workaround: Steam → Color Blocks → Properties → Controller → Steam Input = Disabled.",
            "9. Prefer official layout; after game update verify install has Steam\\*.vdf."
        });

    private static string BuildNetworkDiagnostics()
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"PacketsSent            : {MultiplayerDebug.PacketsSent}",
            $"PacketsReceived        : {MultiplayerDebug.PacketsReceived}",
            $"InputPacketsSent       : {MultiplayerDebug.InputPacketsSent}",
            $"InputPacketsReceived   : {MultiplayerDebug.InputPacketsReceived}",
            $"SnapshotPacketsSent    : {MultiplayerDebug.SnapshotPacketsSent}",
            $"SnapshotPacketsReceived: {MultiplayerDebug.SnapshotPacketsReceived}",
            $"BytesSent              : {MultiplayerDebug.BytesSent}",
            $"BytesReceived          : {MultiplayerDebug.BytesReceived}",
            $"SnapshotsApplied       : {MultiplayerDebug.SnapshotsApplied}",
            $"MissingPlayerHits      : {MultiplayerDebug.MissingPlayerSnapshotHits}",
            $"MissingRopeHits        : {MultiplayerDebug.MissingRopeSnapshotHits}",
            $"PacketsSent/s          : {MultiplayerDebug.PacketsSentPerSecond:0.0}",
            $"PacketsReceived/s      : {MultiplayerDebug.PacketsReceivedPerSecond:0.0}",
            $"Snapshots/s            : {MultiplayerDebug.SnapshotsPerSecond:0.0}"
        });
    }

    private static string BuildLastSessionSummary()
    {
        StringBuilder sb = new();
        foreach (string line in SessionDiagnostics.BuildSummaryLines())
        {
            sb.AppendLine(line);
        }

        if (MultiplayerDebug.StartupValidationErrors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Startup validation errors:");
            foreach (string error in MultiplayerDebug.StartupValidationErrors)
            {
                sb.AppendLine($"  ! {error}");
            }
        }

        return sb.ToString();
    }

    private static void AddText(ZipArchive zip, string entryName, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(entryName);
        using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static void TryAddFile(ZipArchive zip, string path, string entryName)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            ZipArchiveEntry entry = zip.CreateEntry(entryName);
            using Stream target = entry.Open();
            using FileStream source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            source.CopyTo(target);
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Warn("Export", $"Skipped '{path}': {ex.Message}");
        }
    }
}
