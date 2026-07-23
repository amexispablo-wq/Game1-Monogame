using System;
using System.Collections.Generic;

namespace ColorBlocks;

public static class SteamConstants
{
    public const string GameVersion = "1.0.0";
    public const int PartyVersion = 1;
    public const int MaxLobbyPlayers = 4;

    public const string LobbyDataPartyVersion = "party_version";
    public const string LobbyDataGameVersion = "game_version";
    public const string LobbyDataBuildGuid = "build_guid";
    public const string LobbyDataGitCommit = "git_commit";
    public const string LobbyDataSessionId = "session_id";
    public const string LobbyDataLevelHash = "level_hash";
    public const string LobbyDataLevel = "level";
    public const string LobbyDataRopeMode = "rope_mode";
    public const string LobbyDataLavaRise = "lava_rise";
    public const string LobbyDataPartyRoster = "party_roster";
    public const string LobbyDataLeaderSteam = "leader_steam";
    /// <summary>"1" while host GameScene is active; "0" when host leaves gameplay (party lobby stays).</summary>
    public const string LobbyDataGameplay = "gameplay";

    public const string LobbyMemberDataLocals = "locals";
    public const string LobbyMemberDataBuild = "build_info";
    /// <summary>Comma-separated base64 16×16 packs, same order as <see cref="LobbyMemberDataLocals"/>.</summary>
    public const string LobbyMemberDataSkins = "skins";

    public const string ChatPrefixStart = "START:";
    public const string ChatPrefixKick = "KICK:";
    /// <summary>Guest intentional leave of GameScene; host ends level too. Lobby stays.</summary>
    public const string ChatPrefixLeaveLevel = "LEAVE_LEVEL:";
    public const string RichPresenceConnectKey = "connect";
    public const string RichPresenceDisplayKey = "steam_display";
    public const string RichPresencePlayerGroupKey = "steam_player_group";
    public const string RichPresencePlayerGroupSizeKey = "steam_player_group_size";
    public const string RichPresenceInPartyToken = "#StatusInParty";
    public const string RichPresenceConnectPrefix = "lobby:";
    public const string RichPresenceLegacyConnectPrefix = "+connect_lobby_";
    /// <summary>Steam classic launch flag; lobby id follows as the next argv token.</summary>
    public const string RichPresenceConnectLobbyFlag = "+connect_lobby";
}

public enum SteamPartyError
{
    SteamOffline,
    LobbyFull,
    VersionMismatch,
    JoinFailed,
    LobbyClosed,
    CreateFailed
}

public readonly record struct LobbyMemberInfo(
    ulong SteamId,
    string DisplayName,
    bool IsOwner);

public readonly record struct PartyStartMessage(
    string LevelId,
    RopeGameplayMode RopeMode,
    bool LavaRiseEnabled,
    string? LevelHash = null);

public sealed class PartyRosterEntry
{
    public int MemberIndex { get; init; }
    public ulong OwningSteamId { get; init; }
    public ulong SteamId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public PartyMemberType MemberType { get; init; }
    public int ControllerId { get; init; }
    public bool IsLeader { get; init; }
}

public static class PartyRosterCodec
{
    private const char EntrySeparator = '|';
    private const char FieldSeparator = ';';

    public static string Serialize(IReadOnlyList<PartyRosterEntry> entries)
    {
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        List<string> serialized = new(entries.Count);
        foreach (PartyRosterEntry entry in entries)
        {
            serialized.Add(string.Join(
                FieldSeparator,
                entry.MemberIndex,
                entry.OwningSteamId,
                entry.SteamId,
                EncodeName(entry.DisplayName),
                (int)entry.MemberType,
                entry.ControllerId,
                entry.IsLeader ? 1 : 0));
        }

        return string.Join(EntrySeparator, serialized);
    }

    public static List<PartyRosterEntry> Deserialize(string? data)
    {
        List<PartyRosterEntry> entries = new();
        if (string.IsNullOrWhiteSpace(data))
        {
            return entries;
        }

        string[] parts = data.Split(EntrySeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            string[] fields = part.Split(FieldSeparator);
            if (fields.Length < 7)
            {
                continue;
            }

            if (!int.TryParse(fields[0], out int memberIndex)
                || !ulong.TryParse(fields[1], out ulong owningSteamId)
                || !ulong.TryParse(fields[2], out ulong steamId)
                || !int.TryParse(fields[4], out int memberType)
                || !int.TryParse(fields[5], out int controllerId)
                || !int.TryParse(fields[6], out int isLeader))
            {
                continue;
            }

            entries.Add(new PartyRosterEntry
            {
                MemberIndex = memberIndex,
                OwningSteamId = owningSteamId,
                SteamId = steamId,
                DisplayName = DecodeName(fields[3]),
                MemberType = (PartyMemberType)memberType,
                ControllerId = controllerId,
                IsLeader = isLeader == 1
            });
        }

        return entries;
    }

    public static string SerializeLocalSlots(IReadOnlyList<PartyMember> localMembers, ulong owningSteamId)
    {
        List<string> slots = new();
        foreach (PartyMember member in localMembers)
        {
            if (member.OwningSteamId != owningSteamId || member.MemberType == PartyMemberType.SteamRemote)
            {
                continue;
            }

            slots.Add(member.MemberType switch
            {
                PartyMemberType.LocalKeyboard => "K",
                PartyMemberType.LocalGamepad => $"G{member.ControllerId}",
                _ => string.Empty
            });
        }

        return string.Join(",", slots);
    }

    public static List<(PartyMemberType type, int controllerId)> DeserializeLocalSlots(string? data)
    {
        List<(PartyMemberType type, int controllerId)> slots = new();
        if (string.IsNullOrWhiteSpace(data))
        {
            return slots;
        }

        foreach (string token in data.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token == "K")
            {
                slots.Add((PartyMemberType.LocalKeyboard, -1));
                continue;
            }

            if (token.StartsWith('G') && int.TryParse(token.AsSpan(1), out int controllerId))
            {
                slots.Add((PartyMemberType.LocalGamepad, controllerId));
            }
        }

        return slots;
    }

    private static string EncodeName(string name) => name.Replace(';', ',').Replace('|', '/');

    private static string DecodeName(string encoded) => encoded.Replace(',', ';').Replace('/', '|');
}

public static class SteamOwnerId
{
    public static int FromSteamId(ulong steamId) => (int)(steamId & 0x7FFFFFFF);
}
