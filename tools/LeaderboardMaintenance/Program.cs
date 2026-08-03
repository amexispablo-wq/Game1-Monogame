#nullable enable
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace LeaderboardMaintenance;

/// <summary>
/// Local Steam leaderboard maintenance tool. Double-click the published .exe — no env setup.
/// NEVER ship this tool (or the publisher key) in the Steam game depot.
/// </summary>
internal static class Program
{
    private const int OfficialLevelReserve = 20;
    private const int PlayerCountBoards = 4;
    private const int SteamMaxLeaderboards = 10_000;
    private const int WorkshopEligibleLevelCap =
        (SteamMaxLeaderboards - OfficialLevelReserve * PlayerCountBoards) / PlayerCountBoards;

    // Built-in defaults for zero-config local use. Env vars still override if set.
    private const string DefaultPublisherKey = "DD8BCA33BE7CB1D6FA5038ADBC4D245F";
    private const uint DefaultAppId = 4796400;

    private static async Task<int> Main()
    {
        int exitCode = 0;
        try
        {
            exitCode = await RunAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex}");
            exitCode = 1;
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit…");
        try
        {
            Console.ReadKey(intercept: true);
        }
        catch
        {
            // non-interactive host
        }

        return exitCode;
    }

    private static async Task<int> RunAsync()
    {
        string? keyFromEnv = Environment.GetEnvironmentVariable("STEAM_PUBLISHER_KEY");
        string publisherKey = string.IsNullOrWhiteSpace(keyFromEnv) ? DefaultPublisherKey : keyFromEnv;

        uint appId = DefaultAppId;
        if (uint.TryParse(
                Environment.GetEnvironmentVariable("STEAM_APP_ID"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out uint parsedApp)
            && parsedApp != 0)
        {
            appId = parsedApp;
        }
        else
        {
            uint fromFile = TryReadAppIdFromSteamAppIdTxt();
            if (fromFile != 0)
            {
                appId = fromFile;
            }
        }

        string toolDir = AppContext.BaseDirectory;
        string statePath = Path.Combine(toolDir, "maintenance-state.json");
        string? officialOverride = Environment.GetEnvironmentVariable("OFFICIAL_LEVELS_DIR");
        string officialDir = !string.IsNullOrWhiteSpace(officialOverride)
            ? officialOverride
            : FindOfficialLevelsDir(toolDir);

        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var steam = new SteamWebApi(http, publisherKey, appId);
        var state = MaintenanceState.Load(statePath);

        Console.WriteLine($"LeaderboardMaintenance appId={appId}");
        Console.WriteLine($"officialDir='{officialDir}'");
        Console.WriteLine($"Workshop cap={WorkshopEligibleLevelCap}  Official reserve levels={OfficialLevelReserve}");

        Console.WriteLine("→ Fetching top workshop by unique subscriptions…");
        List<ulong> topWorkshop = await steam.QueryTopWorkshopBySubscriptionsAsync(WorkshopEligibleLevelCap);
        var topSet = new HashSet<ulong>(topWorkshop);
        Console.WriteLine($"  top count={topWorkshop.Count}");

        Console.WriteLine("→ Fetching all leaderboards for game…");
        List<(string Name, uint Id)> boards = await steam.GetLeaderboardsForGameAsync();
        Console.WriteLine($"  boards={boards.Count}");

        var boardByName = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach ((string name, uint id) in boards)
        {
            boardByName[name] = id;
        }

        int officialResets = 0;
        int workshopResets = 0;
        int deletes = 0;

        Console.WriteLine("→ Official version checks…");
        foreach ((string stem, int version) in ReadOfficialVersions(officialDir))
        {
            if (state.OfficialVersions.TryGetValue(stem, out int last) && last == version)
            {
                continue;
            }

            Console.WriteLine($"  RESET official stem={stem} version {last} → {version}");
            for (int p = 1; p <= PlayerCountBoards; p++)
            {
                string name = $"official_{stem}_p{p}_f4";
                if (await steam.ResetLeaderboardByNameAsync(name, boardByName))
                {
                    officialResets++;
                }
            }

            state.OfficialVersions[stem] = version;
        }

        Console.WriteLine("→ Workshop content / out-of-top checks…");
        var workshopIdsWithBoards = new HashSet<ulong>();
        foreach (string name in boardByName.Keys)
        {
            if (TryParseWorkshopIdFromBoardName(name, out ulong wid))
            {
                workshopIdsWithBoards.Add(wid);
            }
        }

        var workshopIdsToInspect = new HashSet<ulong>(workshopIdsWithBoards);
        foreach (ulong id in topWorkshop)
        {
            workshopIdsToInspect.Add(id);
        }

        Dictionary<ulong, uint> timeUpdated =
            await steam.GetPublishedFileTimeUpdatedAsync(workshopIdsToInspect.ToList());

        foreach (ulong workshopId in workshopIdsToInspect.OrderBy(x => x))
        {
            if (!topSet.Contains(workshopId))
            {
                foreach (string name in boardByName.Keys.Where(n => BoardBelongsToWorkshop(n, workshopId)).ToList())
                {
                    Console.WriteLine($"  DELETE out-of-top board={name}");
                    if (await steam.DeleteLeaderboardAsync(name))
                    {
                        deletes++;
                        boardByName.Remove(name);
                    }
                }

                state.WorkshopTimeUpdated.Remove(workshopId.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            if (!timeUpdated.TryGetValue(workshopId, out uint updated) || updated == 0)
            {
                continue;
            }

            string key = workshopId.ToString(CultureInfo.InvariantCulture);
            if (state.WorkshopTimeUpdated.TryGetValue(key, out uint lastUpdated) && lastUpdated == updated)
            {
                continue;
            }

            Console.WriteLine($"  RESET workshop id={workshopId} time_updated {lastUpdated} → {updated}");
            for (int p = 1; p <= PlayerCountBoards; p++)
            {
                string name = $"workshop_{workshopId}_p{p}_f4";
                if (await steam.ResetLeaderboardByNameAsync(name, boardByName))
                {
                    workshopResets++;
                }
            }

            state.WorkshopTimeUpdated[key] = updated;
        }

        Console.WriteLine("→ Deleting leftover versioned (_v*) boards…");
        foreach (string name in boardByName.Keys.Where(n => n.Contains("_v", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            Console.WriteLine($"  DELETE legacy board={name}");
            if (await steam.DeleteLeaderboardAsync(name))
            {
                deletes++;
                boardByName.Remove(name);
            }
        }

        state.LastRunUtc = DateTime.UtcNow;
        state.Save(statePath);

        Console.WriteLine();
        Console.WriteLine($"Done. officialResets={officialResets} workshopResets={workshopResets} deletes={deletes}");
        Console.WriteLine($"State saved: {statePath}");
        return 0;
    }

    private static uint TryReadAppIdFromSteamAppIdTxt()
    {
        foreach (string candidate in EnumerateSteamAppIdCandidates())
        {
            try
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }

                string text = File.ReadAllText(candidate).Trim();
                if (uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint id) && id != 0)
                {
                    return id;
                }
            }
            catch
            {
                // ignore
            }
        }

        return 0;
    }

    private static IEnumerable<string> EnumerateSteamAppIdCandidates()
    {
        string toolDir = AppContext.BaseDirectory;
        yield return Path.Combine(toolDir, "steam_appid.txt");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "steam_appid.txt");
        yield return Path.GetFullPath(Path.Combine(toolDir, "..", "..", "..", "..", "..", "steam_appid.txt"));
        yield return Path.GetFullPath(Path.Combine(toolDir, "..", "..", "..", "steam_appid.txt"));
    }

    private static string FindOfficialLevelsDir(string toolDir)
    {
        string[] candidates =
        {
            Path.Combine(toolDir, "OfficialLevels"),
            Path.Combine(toolDir, "Content", "OfficialLevels"),
            Path.GetFullPath(Path.Combine(toolDir, "..", "..", "..", "..", "..", "Content", "OfficialLevels")),
            Path.GetFullPath(Path.Combine(toolDir, "..", "..", "..", "Content", "OfficialLevels")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Content", "OfficialLevels")),
        };

        foreach (string path in candidates)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return candidates[0];
    }

    private static List<(string Stem, int Version)> ReadOfficialVersions(string officialDir)
    {
        var result = new List<(string, int)>();
        if (!Directory.Exists(officialDir))
        {
            Console.Error.WriteLine($"WARNING: Official levels dir not found: {officialDir}");
            Console.Error.WriteLine("  Place Content/OfficialLevels next to the exe, or set OFFICIAL_LEVELS_DIR.");
            return result;
        }

        foreach (string file in Directory.EnumerateFiles(officialDir, "*.json"))
        {
            string stem = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(stem, "manifest", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
                int version = 1;
                if (doc.RootElement.TryGetProperty("version", out JsonElement v) && v.TryGetInt32(out int ver))
                {
                    version = Math.Max(1, ver);
                }

                result.Add((stem, version));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"WARNING: failed to read {file}: {ex.Message}");
            }
        }

        return result;
    }

    private static bool TryParseWorkshopIdFromBoardName(string name, out ulong workshopId)
    {
        workshopId = 0;
        if (!name.StartsWith("workshop_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string rest = name["workshop_".Length..];
        int cut = rest.IndexOf("_v", StringComparison.OrdinalIgnoreCase);
        if (cut < 0)
        {
            cut = rest.IndexOf("_p", StringComparison.OrdinalIgnoreCase);
        }

        if (cut <= 0)
        {
            return false;
        }

        return ulong.TryParse(rest[..cut], NumberStyles.Integer, CultureInfo.InvariantCulture, out workshopId);
    }

    private static bool BoardBelongsToWorkshop(string name, ulong workshopId)
    {
        if (!TryParseWorkshopIdFromBoardName(name, out ulong id))
        {
            return false;
        }

        return id == workshopId;
    }
}

internal sealed class MaintenanceState
{
    [JsonPropertyName("lastRunUtc")]
    public DateTime LastRunUtc { get; set; }

    [JsonPropertyName("officialVersions")]
    public Dictionary<string, int> OfficialVersions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("workshopTimeUpdated")]
    public Dictionary<string, uint> WorkshopTimeUpdated { get; set; } = new(StringComparer.Ordinal);

    public static MaintenanceState Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                MaintenanceState? loaded = JsonSerializer.Deserialize<MaintenanceState>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    loaded.OfficialVersions ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    loaded.WorkshopTimeUpdated ??= new Dictionary<string, uint>(StringComparer.Ordinal);
                    return loaded;
                }
            }
        }
        catch
        {
            // start fresh
        }

        return new MaintenanceState();
    }

    public void Save(string path)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(this, opts));
    }
}

internal sealed class SteamWebApi
{
    private readonly HttpClient _http;
    private readonly string _key;
    private readonly uint _appId;

    public SteamWebApi(HttpClient http, string key, uint appId)
    {
        _http = http;
        _key = key;
        _appId = appId;
    }

    public async Task<List<ulong>> QueryTopWorkshopBySubscriptionsAsync(int cap)
    {
        var ids = new List<ulong>();
        const int pageSize = 50;
        int page = 1;
        while (ids.Count < cap)
        {
            string url =
                "https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/"
                + $"?key={Uri.EscapeDataString(_key)}"
                + "&query_type=9"
                + $"&page={page}"
                + $"&numperpage={pageSize}"
                + $"&appid={_appId}"
                + $"&creator_appid={_appId}"
                + "&return_vote_data=false";

            using HttpResponseMessage response = await _http.GetAsync(url);
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"QueryFiles failed status={(int)response.StatusCode} body={Truncate(body)}");
                break;
            }

            int before = ids.Count;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("response", out JsonElement resp)
                    || !resp.TryGetProperty("publishedfiledetails", out JsonElement details))
                {
                    break;
                }

                foreach (JsonElement item in details.EnumerateArray())
                {
                    if (ids.Count >= cap)
                    {
                        break;
                    }

                    if (item.TryGetProperty("publishedfileid", out JsonElement idEl)
                        && ulong.TryParse(idEl.GetString() ?? idEl.GetRawText(), out ulong id)
                        && id != 0)
                    {
                        ids.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"QueryFiles parse failed: {ex.Message}");
                break;
            }

            if (ids.Count == before)
            {
                break;
            }

            page++;
            if (page > 200)
            {
                break;
            }
        }

        return ids;
    }

    public async Task<List<(string Name, uint Id)>> GetLeaderboardsForGameAsync()
    {
        var result = new List<(string, uint)>();
        string url =
            "https://partner.steam-api.com/ISteamLeaderboards/GetLeaderboardsForGame/v2/"
            + $"?key={Uri.EscapeDataString(_key)}"
            + $"&appid={_appId}";

        using HttpResponseMessage response = await _http.GetAsync(url);
        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"GetLeaderboardsForGame failed status={(int)response.StatusCode}");
            return result;
        }

        try
        {
            if (body.TrimStart().StartsWith('<'))
            {
                XDocument doc = XDocument.Parse(body);
                foreach (XElement lb in doc.Descendants("leaderboard"))
                {
                    string? name = lb.Element("name")?.Value;
                    string? idText = lb.Element("id")?.Value ?? lb.Element("leaderboardid")?.Value;
                    if (!string.IsNullOrWhiteSpace(name) && uint.TryParse(idText, out uint id))
                    {
                        result.Add((name, id));
                    }
                }
            }
            else
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("leaderboards", out JsonElement arr)
                    || (doc.RootElement.TryGetProperty("response", out JsonElement resp)
                        && resp.TryGetProperty("leaderboards", out arr)))
                {
                    foreach (JsonElement item in arr.EnumerateArray())
                    {
                        string? name = item.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                        uint id = 0;
                        if (item.TryGetProperty("id", out JsonElement idEl) && idEl.TryGetUInt32(out uint u))
                        {
                            id = u;
                        }
                        else if (item.TryGetProperty("leaderboardid", out JsonElement idEl2)
                                 && idEl2.TryGetUInt32(out uint u2))
                        {
                            id = u2;
                        }

                        if (!string.IsNullOrWhiteSpace(name) && id != 0)
                        {
                            result.Add((name!, id));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetLeaderboardsForGame parse failed: {ex.Message}");
        }

        return result;
    }

    public async Task<Dictionary<ulong, uint>> GetPublishedFileTimeUpdatedAsync(List<ulong> ids)
    {
        var map = new Dictionary<ulong, uint>();
        const int batch = 50;
        for (int i = 0; i < ids.Count; i += batch)
        {
            var form = new Dictionary<string, string>
            {
                ["itemcount"] = Math.Min(batch, ids.Count - i).ToString(CultureInfo.InvariantCulture)
            };
            for (int j = 0; j < batch && i + j < ids.Count; j++)
            {
                form[$"publishedfileids[{j}]"] = ids[i + j].ToString(CultureInfo.InvariantCulture);
            }

            using HttpResponseMessage response = await _http.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                new FormUrlEncodedContent(form));
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"GetPublishedFileDetails failed status={(int)response.StatusCode}");
                continue;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                JsonElement details = doc.RootElement.GetProperty("response").GetProperty("publishedfiledetails");
                foreach (JsonElement item in details.EnumerateArray())
                {
                    if (!item.TryGetProperty("publishedfileid", out JsonElement idEl)
                        || !ulong.TryParse(idEl.GetString() ?? idEl.GetRawText(), out ulong id))
                    {
                        continue;
                    }

                    uint updated = 0;
                    if (item.TryGetProperty("time_updated", out JsonElement tu))
                    {
                        if (tu.ValueKind == JsonValueKind.Number)
                        {
                            updated = tu.TryGetUInt32(out uint u) ? u : 0;
                        }
                        else
                        {
                            uint.TryParse(tu.GetString(), out updated);
                        }
                    }

                    map[id] = updated;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"GetPublishedFileDetails parse failed: {ex.Message}");
            }
        }

        return map;
    }

    public async Task<bool> DeleteLeaderboardAsync(string name)
    {
        var form = new Dictionary<string, string>
        {
            ["key"] = _key,
            ["appid"] = _appId.ToString(CultureInfo.InvariantCulture),
            ["name"] = name
        };

        using HttpResponseMessage response = await _http.PostAsync(
            "https://partner.steam-api.com/ISteamLeaderboards/DeleteLeaderboard/v1/",
            new FormUrlEncodedContent(form));
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"DeleteLeaderboard '{name}' status={(int)response.StatusCode}");
        }

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResetLeaderboardByNameAsync(string name, Dictionary<string, uint> boardByName)
    {
        if (!boardByName.TryGetValue(name, out uint id) || id == 0)
        {
            Console.WriteLine($"    skip reset (board missing): {name}");
            return false;
        }

        var form = new Dictionary<string, string>
        {
            ["key"] = _key,
            ["appid"] = _appId.ToString(CultureInfo.InvariantCulture),
            ["leaderboardid"] = id.ToString(CultureInfo.InvariantCulture)
        };

        using HttpResponseMessage response = await _http.PostAsync(
            "https://partner.steam-api.com/ISteamLeaderboards/ResetLeaderboard/v1/",
            new FormUrlEncodedContent(form));
        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"ResetLeaderboard '{name}' id={id} status={(int)response.StatusCode}");
        }

        return response.IsSuccessStatusCode;
    }

    private static string Truncate(string value) =>
        value.Length <= 300 ? value : value[..300] + "…";
}
