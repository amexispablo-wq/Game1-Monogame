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

    public static PlayerSkinData? GetSkinForMember(PartyMemberId memberId)
    {
        string? skinId = GetSelectedSkinId(memberId);
        if (string.IsNullOrEmpty(skinId))
        {
            return null;
        }

        PlayerSkinEntry? entry = FindSkin(skinId);
        return entry?.ToSkinData();
    }

    public static string? GetSelectedSkinId(PartyMemberId memberId)
    {
        string key = memberId.Value.ToString();
        if (_library.Selections.TryGetValue(key, out string? skinId) && FindSkin(skinId) is not null)
        {
            return skinId;
        }

        return _library.Skins.Count > 0 ? _library.Skins[0].Id : null;
    }

  public static void SetSelectedSkinId(PartyMemberId memberId, string skinId)
  {
    if (FindSkin(skinId) is null)
    {
      return;
    }

    _library.Selections[memberId.Value.ToString()] = skinId;
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading skin library: {ex.Message}");
            _library = new SkinLibraryFile();
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
