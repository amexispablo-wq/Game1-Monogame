#nullable enable
using System;

namespace ColorBlocks;

/// <summary>
/// Client-side gate run when a host START message arrives, before entering gameplay.
/// Cancels gameplay on build mismatch or level hash mismatch. Diagnostics only —
/// a passing gate changes nothing about how gameplay starts.
/// </summary>
public static class MultiplayerStartGate
{
    public static bool ValidateClientStart(
        SteamLobbyService lobby,
        PartyStartMessage message,
        out string errorTitle,
        out string errorMessage)
    {
        errorTitle = string.Empty;
        errorMessage = string.Empty;

        if (lobby.TryGetHostBuildMismatch(out string buildMismatch))
        {
            errorTitle = "VERSION MISMATCH";
            errorMessage = buildMismatch;
            MultiplayerDebug.LogError("BuildHandshake", "Gameplay start CANCELLED — build mismatch");
            return false;
        }

        if (!string.IsNullOrEmpty(message.LevelHash))
        {
            string localHash = SessionDiagnostics.ComputeLevelHash(message.LevelId);
            SessionDiagnostics.RecordLevelHashes(localHash, message.LevelHash!);
            bool match = !string.IsNullOrEmpty(localHash)
                && string.Equals(localHash, message.LevelHash, StringComparison.OrdinalIgnoreCase);
            MultiplayerDebug.Log(
                "LevelHash",
                $"Level '{message.LevelId}' local={SessionDiagnostics.ShortHash(localHash)} " +
                $"host={SessionDiagnostics.ShortHash(message.LevelHash!)} match={match}");

            if (!match)
            {
                errorTitle = "LEVEL MISMATCH";
                errorMessage = "Level mismatch.";
                MultiplayerDebug.LogError(
                    "LevelHash",
                    $"Gameplay start CANCELLED — level mismatch. Local SHA256={localHash} Host SHA256={message.LevelHash}");
                return false;
            }
        }

        return true;
    }
}
