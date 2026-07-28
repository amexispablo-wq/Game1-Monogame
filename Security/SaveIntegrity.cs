#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorBlocks;

/// <summary>
/// HMAC-SHA256 envelope for local JSON saves. Key material = embedded salt +
/// user-data root + optional Steam id. Blocks casual hand-edits; not a server trust root.
/// </summary>
public static class SaveIntegrity
{
    private const int EnvelopeVersion = 1;

    // Not a secret against a patched binary — only raises the bar for notepad edits.
    private static readonly byte[] EmbeddedSalt =
    {
        0x43, 0x6F, 0x6C, 0x6F, 0x72, 0x42, 0x6C, 0x6F,
        0x63, 0x6B, 0x73, 0x53, 0x61, 0x76, 0x65, 0x31,
        0xA7, 0x3E, 0x91, 0x5C, 0x22, 0xF8, 0x4D, 0x1B,
        0x6E, 0x09, 0xD4, 0xB2, 0x77, 0x8A, 0xE5, 0x30
    };

    private static readonly JsonSerializerOptions EnvelopeOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Optional Steam id provider. Set after Steam init for machine-bound keys.</summary>
    public static Func<ulong>? SteamIdProvider { get; set; }

    public static bool TryLoadSigned<T>(string path, out T? payload) where T : class
    {
        payload = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("v", out JsonElement versionElement)
                || !root.TryGetProperty("payload", out JsonElement payloadElement)
                || !root.TryGetProperty("hmac", out JsonElement hmacElement))
            {
                return false;
            }

            if (versionElement.GetInt32() != EnvelopeVersion)
            {
                DiagnosticsLog.Info("SaveIntegrity", $"Unsupported envelope version in '{path}'");
                return false;
            }

            string storedHmac = hmacElement.GetString() ?? string.Empty;
            string payloadJson = payloadElement.GetRawText();
            string expected = ComputeHmacHex(payloadJson);
            if (!string.Equals(storedHmac, expected, StringComparison.OrdinalIgnoreCase))
            {
                DiagnosticsLog.Info("SaveIntegrity", $"HMAC mismatch — rejecting '{path}'");
                return false;
            }

            payload = JsonSerializer.Deserialize<T>(payloadJson, PayloadOptions);
            return payload is not null;
        }
        catch (Exception ex)
        {
            DiagnosticsLog.Info("SaveIntegrity", $"Failed to load signed '{path}': {ex.Message}");
            return false;
        }
    }

    public static void SaveSigned<T>(string path, T payload)
    {
        string payloadJson = JsonSerializer.Serialize(payload, PayloadOptions);
        string hmac = ComputeHmacHex(payloadJson);
        var envelope = new SignedEnvelope
        {
            V = EnvelopeVersion,
            Payload = JsonSerializer.Deserialize<JsonElement>(payloadJson),
            Hmac = hmac
        };

        string json = JsonSerializer.Serialize(envelope, EnvelopeOptions);
        AtomicFileWriter.WriteAllText(path, json);
    }

    /// <summary>True when the file looks like a signed envelope (regardless of HMAC validity).</summary>
    public static bool LooksSigned(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("v", out _)
                && root.TryGetProperty("payload", out _)
                && root.TryGetProperty("hmac", out _);
        }
        catch
        {
            return false;
        }
    }

    private static string ComputeHmacHex(string payloadJson)
    {
        byte[] key = DeriveKey();
        byte[] data = Encoding.UTF8.GetBytes(payloadJson);
        byte[] hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash);
    }

    private static byte[] DeriveKey()
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(EmbeddedSalt);
        sha.AppendData(Encoding.UTF8.GetBytes(UserDataPaths.Root));

        ulong steamId = 0;
        try
        {
            steamId = SteamIdProvider?.Invoke() ?? 0;
        }
        catch
        {
            steamId = 0;
        }

        if (steamId != 0)
        {
            sha.AppendData(BitConverter.GetBytes(steamId));
        }

        byte[] localSalt = EnsureLocalSalt();
        sha.AppendData(localSalt);
        return sha.GetHashAndReset();
    }

    private static byte[] EnsureLocalSalt()
    {
        string saltPath = Path.Combine(UserDataPaths.Root, ".save_salt");
        try
        {
            UserDataPaths.Initialize();
            if (File.Exists(saltPath))
            {
                byte[] existing = File.ReadAllBytes(saltPath);
                if (existing.Length >= 16)
                {
                    return existing;
                }
            }

            byte[] fresh = RandomNumberGenerator.GetBytes(32);
            AtomicFileWriter.WriteAllBytes(saltPath, fresh);
            return fresh;
        }
        catch
        {
            return EmbeddedSalt;
        }
    }

    private sealed class SignedEnvelope
    {
        [JsonPropertyName("v")]
        public int V { get; set; }

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; set; }

        [JsonPropertyName("hmac")]
        public string Hmac { get; set; } = string.Empty;
    }
}
