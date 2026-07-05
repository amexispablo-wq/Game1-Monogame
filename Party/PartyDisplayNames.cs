namespace ColorBlocks;

public static class PartyDisplayNames
{
    public static string FormatLocalMemberName(string? localSteamUsername, int localOrdinal)
    {
        string baseName = ResolveLocalBaseName(localSteamUsername);
        if (localOrdinal == 0 && !IsFallbackPlayerName(baseName))
        {
            return baseName;
        }

        if (IsFallbackPlayerName(baseName))
        {
            return $"Player {localOrdinal + 1}";
        }

        return $"{baseName} {localOrdinal + 1}";
    }

    public static string FormatMemberListLabel(PartyMember member)
    {
        if (member.IsLeader)
        {
            return $"{member.DisplayName} LEADER";
        }

        return member.DisplayName;
    }

    public static string FormatEmptySlotLabel(int slotIndex) => $"Player {slotIndex + 1}";

    private static string ResolveLocalBaseName(string? username)
    {
        if (string.IsNullOrWhiteSpace(username) || username == "Unavailable")
        {
            return "Player";
        }

        return username;
    }

    private static bool IsFallbackPlayerName(string baseName) => baseName == "Player";
}
