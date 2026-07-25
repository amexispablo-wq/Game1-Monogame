#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ColorBlocks;

public static class SkinLibraryStorage
{
    private const string LibraryFileName = "skin_library.json";
    private static SkinLibraryFile _library = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<PlayerSkinEntry> Skins => _library.Skins;

    public static void Initialize()
    {
        Load();
        EnsureDefaultSkin();
    }

    public static PlayerSkinData? GetSkinForLocalSlot(int localSlot)
    {
        string? skinId = GetSelectedSkinId(localSlot);
        if (string.IsNullOrEmpty(skinId))
        {
            return null;
        }

        PlayerSkinEntry? entry = FindSkin(skinId);
        return entry?.ToSkinData();
    }

    public static string? GetSelectedSkinId(int localSlot)
    {
        if (localSlot < 0)
        {
            return _library.Skins.Count > 0 ? _library.Skins[0].Id : null;
        }

        string key = SlotKey(localSlot);
        if (_library.Selections.TryGetValue(key, out string? skinId) && FindSkin(skinId) is not null)
        {
            return skinId;
        }

        return _library.Skins.Count > 0 ? _library.Skins[0].Id : null;
    }

    public static void SetSelectedSkinId(int localSlot, string skinId)
    {
        if (localSlot < 0 || FindSkin(skinId) is null)
        {
            return;
        }

        _library.Selections[SlotKey(localSlot)] = skinId;
        Save();
    }

    public static void UpdateSkinPixels(string skinId, PlayerSkinData data)
    {
        PlayerSkinEntry? entry = FindSkin(skinId);
        if (entry is null)
        {
            return;
        }

        entry.Pixels = (bool[])data.Pixels.Clone();
        Save();
    }

    public static PlayerSkinEntry AddSkin(string name, PlayerSkinData data)
    {
        PlayerSkinEntry entry = PlayerSkinEntry.FromSkinData(name, data);
        _library.Skins.Add(entry);
        Save();
        return entry;
    }

    public static bool DeleteSkin(string skinId)
    {
        int index = _library.Skins.FindIndex(skin => skin.Id == skinId);
        if (index < 0)
        {
            return false;
        }

        _library.Skins.RemoveAt(index);

        foreach (string key in _library.Selections.Keys.ToList())
        {
            if (_library.Selections[key] == skinId)
            {
                _library.Selections.Remove(key);
            }
        }

        EnsureDefaultSkin();
        Save();
        return true;
    }

    public static PlayerSkinEntry? FindSkin(string skinId)
    {
        return _library.Skins.FirstOrDefault(skin => skin.Id == skinId);
    }

    private static string SlotKey(int localSlot) => localSlot.ToString();

    private static void EnsureDefaultSkin()
    {
        if (_library.Skins.Count > 0)
        {
            return;
        }

        _library.Skins.Add(PlayerSkinEntry.FromSkinData("Default", new PlayerSkinData()));
        Save();
    }

    private static void Load()
    {
        string path = GetWritablePath();
        if (!File.Exists(path))
        {
            _library = new SkinLibraryFile();
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            SkinLibraryFile? loaded = JsonSerializer.Deserialize<SkinLibraryFile>(json, JsonOptions);
            _library = loaded ?? new SkinLibraryFile();
            _library.Skins ??= new List<PlayerSkinEntry>();
            _library.Selections ??= new Dictionary<string, string>();
            MigrateLegacyMemberIdSelections();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading skin library: {ex.Message}");
            _library = new SkinLibraryFile();
        }
    }

    /// <summary>
    /// Old builds keyed selections by PartyMemberId (often "1", "2", plus orphaned high IDs).
    /// New builds key by local slot ("0", "1", ...). Copy common offline keys, drop the rest.
    /// </summary>
    private static void MigrateLegacyMemberIdSelections()
    {
        // Slot keys start at "0"; legacy member ids started at 1. Presence of "0" = already migrated.
        bool alreadySlotBased = _library.Selections.ContainsKey("0");
        string? legacySlot0 = null;
        string? legacySlot1 = null;

        if (!alreadySlotBased)
        {
            if (_library.Selections.TryGetValue("1", out string? skin0) && FindSkin(skin0) is not null)
            {
                legacySlot0 = skin0;
            }

            if (_library.Selections.TryGetValue("2", out string? skin1) && FindSkin(skin1) is not null)
            {
                legacySlot1 = skin1;
            }
        }

        List<string>? orphanKeys = null;
        foreach (string key in _library.Selections.Keys)
        {
            if (!int.TryParse(key, out int value))
            {
                continue;
            }

            // Keep slot keys 0/1 when already migrated; otherwise strip all member-id keys.
            if (alreadySlotBased)
            {
                if (value >= 2)
                {
                    orphanKeys ??= new List<string>();
                    orphanKeys.Add(key);
                }
            }
            else if (value >= 1)
            {
                orphanKeys ??= new List<string>();
                orphanKeys.Add(key);
            }
        }

        bool migrated = false;
        if (orphanKeys is not null)
        {
            foreach (string key in orphanKeys)
            {
                _library.Selections.Remove(key);
            }

            migrated = true;
        }

        if (!alreadySlotBased)
        {
            if (legacySlot0 is not null)
            {
                _library.Selections["0"] = legacySlot0;
                migrated = true;
            }

            if (legacySlot1 is not null)
            {
                _library.Selections["1"] = legacySlot1;
                migrated = true;
            }
        }

        if (migrated)
        {
            Save();
        }
    }

    private static void Save()
    {
        try
        {
            string path = GetWritablePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = JsonSerializer.Serialize(_library, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving skin library: {ex.Message}");
        }
    }

    private static string GetWritablePath()
    {
        return UserDataPaths.SkinLibraryFile;
    }
}
