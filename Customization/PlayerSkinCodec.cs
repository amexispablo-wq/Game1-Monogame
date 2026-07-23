#nullable enable
using System;
using System.Collections.Generic;

namespace ColorBlocks;

/// <summary>
/// Pack 16×16 skin bits for lobby strings and snapshot wire format (32 bytes).
/// </summary>
public static class PlayerSkinCodec
{
    public const int PackedByteCount = (PlayerSkinData.GridSize * PlayerSkinData.GridSize) / 8;

    public static byte[] Pack(PlayerSkinData? skin)
    {
        byte[] packed = new byte[PackedByteCount];
        if (skin is null)
        {
            return packed;
        }

        bool[] pixels = skin.Pixels;
        for (int i = 0; i < pixels.Length && i < PackedByteCount * 8; i++)
        {
            if (pixels[i])
            {
                packed[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        return packed;
    }

    public static PlayerSkinData Unpack(ReadOnlySpan<byte> packed)
    {
        var data = new PlayerSkinData();
        bool[] pixels = data.Pixels;
        int bitCount = Math.Min(pixels.Length, packed.Length * 8);
        for (int i = 0; i < bitCount; i++)
        {
            pixels[i] = (packed[i >> 3] & (1 << (i & 7))) != 0;
        }

        return data;
    }

    public static bool PackedEquals(PlayerSkinData? skin, ReadOnlySpan<byte> packed)
    {
        if (skin is null)
        {
            for (int i = 0; i < packed.Length; i++)
            {
                if (packed[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        byte[] current = Pack(skin);
        return current.AsSpan().SequenceEqual(packed);
    }

    public static string ToBase64(PlayerSkinData? skin) => Convert.ToBase64String(Pack(skin));

    public static PlayerSkinData? FromBase64(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        try
        {
            byte[] packed = Convert.FromBase64String(encoded.Trim());
            if (packed.Length != PackedByteCount)
            {
                return null;
            }

            return Unpack(packed);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string SerializeSlotSkins(IReadOnlyList<PartyMember> localMembers)
    {
        if (localMembers.Count == 0)
        {
            return string.Empty;
        }

        var parts = new string[localMembers.Count];
        for (int i = 0; i < localMembers.Count; i++)
        {
            PartyMember member = localMembers[i];
            PlayerSkinData? skin = SkinLibraryStorage.GetSkinForMember(member.Id);
            parts[i] = ToBase64(skin);
        }

        return string.Join(",", parts);
    }

    public static PlayerSkinData? TryGetSlotSkin(string? skinsData, int slotIndex)
    {
        if (string.IsNullOrWhiteSpace(skinsData) || slotIndex < 0)
        {
            return null;
        }

        string[] parts = skinsData.Split(',', StringSplitOptions.None);
        if (slotIndex >= parts.Length)
        {
            return null;
        }

        return FromBase64(parts[slotIndex]);
    }

    public static int LocalSlotIndex(IReadOnlyList<PartyMember> members, PartyMember target)
    {
        int slot = 0;
        for (int i = 0; i < members.Count; i++)
        {
            PartyMember member = members[i];
            if (member.OwningSteamId != target.OwningSteamId)
            {
                continue;
            }

            if (ReferenceEquals(member, target) || member.Id == target.Id)
            {
                return slot;
            }

            slot++;
        }

        return -1;
    }

    public static (PlayerSkinData? Skin, string? SkinId) ResolveForMember(
        SteamLobbyService? lobby,
        PartyMember member,
        IReadOnlyList<PartyMember> members)
    {
        if (member.IsLocallyOwned)
        {
            string? skinId = SkinLibraryStorage.GetSelectedSkinId(member.Id);
            return (SkinLibraryStorage.GetSkinForMember(member.Id), skinId);
        }

        if (lobby is null || !lobby.IsInLobby || member.OwningSteamId == 0)
        {
            return (null, null);
        }

        int slot = LocalSlotIndex(members, member);
        if (slot < 0)
        {
            return (null, null);
        }

        string? skinsData = lobby.GetLobbyMemberData(member.OwningSteamId, SteamConstants.LobbyMemberDataSkins);
        PlayerSkinData? skin = TryGetSlotSkin(skinsData, slot);
        if (skin is null)
        {
            return (null, null);
        }

        // Remote id is slot-scoped so snapshot diffs stay stable per peer slot.
        string remoteId = $"remote:{member.OwningSteamId}:{slot}";
        return (skin, remoteId);
    }
}
