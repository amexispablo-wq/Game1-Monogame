#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ColorBlocks;

/// <summary>
/// Immutable build identity loaded from Content/version.json (generated on every build/publish
/// by the GenerateBuildInfo MSBuild target). Used for on-screen version display, multiplayer
/// build handshakes and diagnostics export.
/// </summary>
public sealed class BuildInfo
{
    private static BuildInfo? _current;

    public string GameVersion { get; init; } = SteamConstants.GameVersion;
    public string BuildTimestampUtc { get; init; } = "unknown";
    public string GitCommit { get; init; } = "unknown";
    public string GitBranch { get; init; } = "unknown";
    public string Configuration { get; init; } = "unknown";
    public string BuildGuid { get; init; } = "UNKNOWN";

    /// <summary>Short human-friendly build id (first 6 chars of the build GUID).</summary>
    public string ShortBuildId => BuildGuid.Length >= 6 ? BuildGuid[..6] : BuildGuid;

    /// <summary>"1.0.0 (A1B3C9)" — used in mismatch messages and overlays.</summary>
    public string Label => $"{GameVersion} ({ShortBuildId})";

    /// <summary>"version|buildGuid|commit" — wire format for the build handshake.</summary>
    public string HandshakeToken => $"{GameVersion}|{BuildGuid}|{GitCommit}";

    public static BuildInfo Current => _current ??= Load();

    public IReadOnlyList<string> DescribeLines() => new[]
    {
        $"GameVersion  : {GameVersion}",
        $"BuildGuid    : {BuildGuid}",
        $"GitCommit    : {GitCommit}",
        $"GitBranch    : {GitBranch}",
        $"Configuration: {Configuration}",
        $"BuildTimeUtc : {BuildTimestampUtc}"
    };

    private static BuildInfo Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "version.json");
        try
        {
            if (File.Exists(path))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = document.RootElement;
                return new BuildInfo
                {
                    GameVersion = ReadString(root, "GameVersion", SteamConstants.GameVersion),
                    BuildTimestampUtc = ReadString(root, "BuildTimestampUtc", "unknown"),
                    GitCommit = ReadString(root, "GitCommit", "unknown"),
                    GitBranch = ReadString(root, "GitBranch", "unknown"),
                    Configuration = ReadString(root, "Configuration", "unknown"),
                    BuildGuid = ReadString(root, "BuildGuid", "UNKNOWN")
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BuildInfo] Failed to read version.json: {ex.Message}");
        }

        return new BuildInfo();
    }

    private static string ReadString(JsonElement root, string property, string fallback)
    {
        if (root.TryGetProperty(property, out JsonElement element)
            && element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return fallback;
    }
}
