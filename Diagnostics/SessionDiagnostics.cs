#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ColorBlocks;

/// <summary>
/// Cross-peer validation state for the current multiplayer session:
/// build handshake results, level hash comparison and Steam Input file hashes.
/// Diagnostics only — never changes gameplay or networking behavior.
/// </summary>
public static class SessionDiagnostics
{
    public static string HostBuildLabel { get; private set; } = "-";
    public static string ClientBuildLabel { get; private set; } = "-";
    public static bool? BuildMatch { get; private set; }
    public static bool? LevelMatch { get; private set; }
    public static string LocalLevelHash { get; private set; } = "-";
    public static string RemoteLevelHash { get; private set; } = "-";
    public static string SteamInputManifestHash { get; private set; } = "-";
    public static string ControllerConfigHash { get; private set; } = "-";

    public static string LocalBuildLabel => BuildInfo.Current.Label;

    public static void ResetSessionState()
    {
        HostBuildLabel = "-";
        ClientBuildLabel = "-";
        BuildMatch = null;
        LevelMatch = null;
        LocalLevelHash = "-";
        RemoteLevelHash = "-";
    }

    public static void RecordBuildHandshake(string hostBuildLabel, string clientBuildLabel, bool match)
    {
        HostBuildLabel = hostBuildLabel;
        ClientBuildLabel = clientBuildLabel;
        BuildMatch = match;
    }

    public static void RecordLevelHashes(string localHash, string remoteHash)
    {
        LocalLevelHash = ShortHash(localHash);
        RemoteLevelHash = ShortHash(remoteHash);
        LevelMatch = !string.IsNullOrEmpty(localHash)
            && string.Equals(localHash, remoteHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>SHA256 of the loaded level file. Empty string when the file cannot be read.</summary>
    public static string ComputeLevelHash(string levelId)
    {
        try
        {
            LevelMetadata? metadata = LevelLibrary.GetLevel(levelId);
            if (metadata is null || !File.Exists(metadata.FilePath))
            {
                return string.Empty;
            }

            return ComputeFileSha256(metadata.FilePath);
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Warn("Level", $"Level hash failed for '{levelId}': {ex.Message}");
            return string.Empty;
        }
    }

    public static string ComputeFileSha256(string path)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    /// <summary>Startup build validation: hash the shipped Steam Input files and log them.</summary>
    public static void LogSteamInputFileHashes()
    {
        SteamInputManifestHash = HashSteamFile("steam_input_manifest.vdf");
        ControllerConfigHash = HashSteamFile("controller_gamepad.vdf");
        DiagnosticsLog.Info("BuildValidation", $"steam_input_manifest.vdf SHA256={SteamInputManifestHash}");
        DiagnosticsLog.Info("BuildValidation", $"controller_gamepad.vdf SHA256={ControllerConfigHash}");
    }

    public static IReadOnlyList<string> BuildSummaryLines()
    {
        return new[]
        {
            $"SessionId        : {DiagnosticsLog.SessionId}",
            $"Local build      : {LocalBuildLabel}",
            $"Host build       : {HostBuildLabel}",
            $"Client build     : {ClientBuildLabel}",
            $"Build match      : {FormatMatch(BuildMatch)}",
            $"Level match      : {FormatMatch(LevelMatch)}",
            $"Local level hash : {LocalLevelHash}",
            $"Remote level hash: {RemoteLevelHash}",
            $"steam_input_manifest.vdf: {SteamInputManifestHash}",
            $"controller_gamepad.vdf  : {ControllerConfigHash}"
        };
    }

    public static string FormatMatch(bool? match) => match switch
    {
        true => "MATCH",
        false => "MISMATCH",
        null => "N/A"
    };

    public static string ShortHash(string hash) =>
        string.IsNullOrEmpty(hash) ? "-" : (hash.Length > 12 ? hash[..12] : hash);

    private static string HashSteamFile(string fileName)
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Steam", fileName);
            if (!File.Exists(path))
            {
                DiagnosticsLog.Warn("BuildValidation", $"Steam Input file missing: {path}");
                return "MISSING";
            }

            return ComputeFileSha256(path);
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Warn("BuildValidation", $"Hash failed for {fileName}: {ex.Message}");
            return "ERROR";
        }
    }
}
