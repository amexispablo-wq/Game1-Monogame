#nullable enable
using System;
using System.Collections.Generic;

namespace ColorBlocks;

public sealed class SteamPartyService
{
    private readonly SteamLobbyService _lobby;
    private string? _lastAppliedRoster;

    public SteamPartyService(SteamLobbyService lobby)
    {
        _lobby = lobby;
    }

    public void PublishLocalMemberData(PartyManager party)
    {
        if (!_lobby.IsInLobby)
        {
            return;
        }

        ulong localSteamId = _lobby.LocalSteamId;
        List<PartyMember> localMembers = new();
        foreach (PartyMember member in party.Members)
        {
            if (member.IsLocallyOwned && member.MemberType != PartyMemberType.SteamRemote)
            {
                localMembers.Add(member);
            }
        }

        // Always publish at least one keyboard slot so host roster never treats this peer as spectator.
        string locals = localMembers.Count > 0
            ? PartyRosterCodec.SerializeLocalSlots(localMembers, localSteamId)
            : "K";
        string skins = localMembers.Count > 0
            ? PlayerSkinCodec.SerializeSlotSkins(localMembers)
            : PlayerSkinCodec.ToBase64(ResolveDefaultLobbySkin(party));

        _lobby.SetLocalMemberData(SteamConstants.LobbyMemberDataLocals, locals);
        _lobby.SetLocalMemberData(SteamConstants.LobbyMemberDataSkins, skins);
        MultiplayerDebug.LogParty(
            $"PublishLocalMemberData sid={localSteamId} localSlots={Math.Max(1, localMembers.Count)} data='{locals}' skinsBytes={skins.Length}" +
            (localMembers.Count == 0 ? " (defaulted empty party → K)" : string.Empty));
    }

    private static PlayerSkinData? ResolveDefaultLobbySkin(PartyManager party)
    {
        foreach (PartyMember member in party.Members)
        {
            if (member.IsLocallyOwned && member.MemberType != PartyMemberType.SteamRemote)
            {
                return SkinLibraryStorage.GetSkinForMember(member.Id);
            }
        }

        return null;
    }

    public void PublishLocalPartyState(PartyManager party)
    {
        PublishLocalMemberData(party);
        if (_lobby.IsLobbyOwner())
        {
            PublishAuthoritativeRoster(party);
        }
    }

    /// <summary>
    /// Host path on LobbyStateChanged: rebuild roster from live lobby members + locals,
    /// publish, and apply into PartyManager so joiners appear immediately.
    /// </summary>
    public void ForceSyncFromLobby(PartyManager party)
    {
        if (!_lobby.IsInLobby || !_lobby.IsLobbyOwner())
        {
            return;
        }

        PublishLocalMemberData(party);
        List<PartyRosterEntry> entries = BuildAuthoritativeRoster(party);
        string serialized = PartyRosterCodec.Serialize(entries);
        string existing = _lobby.GetLobbyData(SteamConstants.LobbyDataPartyRoster) ?? string.Empty;

        bool lobbyMembershipChanged = !RosterOwnersMatchLobby(entries);
        if (!string.Equals(existing, serialized, StringComparison.Ordinal) || lobbyMembershipChanged)
        {
            if (!string.Equals(existing, serialized, StringComparison.Ordinal))
            {
                _lobby.PublishPartyRoster(serialized);
                _lobby.SetLobbyData(SteamConstants.LobbyDataLeaderSteam, _lobby.GetLobbyOwnerSteamId().ToString());
                MultiplayerDebug.LogParty(
                    $"ForceSyncFromLobby published entries={entries.Count} bytes={serialized.Length} " +
                    $"membershipChanged={lobbyMembershipChanged}");
                foreach (PartyRosterEntry entry in entries)
                {
                    MultiplayerDebug.LogParty(
                        $"  auth idx={entry.MemberIndex} '{entry.DisplayName}' ownerSteam={entry.OwningSteamId} type={entry.MemberType}");
                }
            }
            else
            {
                MultiplayerDebug.LogParty(
                    $"ForceSyncFromLobby roster string unchanged but membership mismatch — re-apply locally entries={entries.Count}");
            }
        }
        else
        {
            MultiplayerDebug.LogParty($"ForceSyncFromLobby SKIPPED — roster+membership unchanged entries={entries.Count}");
        }

        // Always apply built roster to host party (covers join when publish skipped due to race).
        ApplyRosterEntries(party, entries, serialized);
    }

    public void RebuildPartyFromLobby(PartyManager party)
    {
        if (!_lobby.IsInLobby)
        {
            return;
        }

        string rosterData = _lobby.GetLobbyData(SteamConstants.LobbyDataPartyRoster) ?? string.Empty;
        if (string.Equals(rosterData, _lastAppliedRoster, StringComparison.Ordinal))
        {
            // Owner should not rely on this path for joins; ForceSyncFromLobby handles that.
            // Clients: skip only when string truly unchanged.
            if (!LobbyHasUncoveredMembers(party))
            {
                MultiplayerDebug.LogParty(
                    $"RebuildPartyFromLobby SKIPPED — roster unchanged (len={rosterData.Length}) " +
                    $"lobbyMembers={_lobby.GetLobbyMemberCount()} partyMembers={party.Members.Count}");
                return;
            }

            MultiplayerDebug.LogParty(
                "RebuildPartyFromLobby roster string unchanged but lobby has uncovered members — fall through");
        }

        _lastAppliedRoster = rosterData;
        List<PartyRosterEntry> entries = PartyRosterCodec.Deserialize(rosterData);
        MultiplayerDebug.LogParty(
            $"RebuildPartyFromLobby rosterLen={rosterData.Length} entries={entries.Count} " +
            $"lobbyMembers={_lobby.GetLobbyMemberCount()}");
        if (entries.Count == 0)
        {
            MultiplayerDebug.LogParty("Empty roster → RebuildFromLobbyMembersOnly fallback");
            RebuildFromLobbyMembersOnly(party);
            if (_lobby.IsLobbyOwner())
            {
                PublishAuthoritativeRoster(party);
            }

            return;
        }

        ApplyRosterEntries(party, entries, rosterData);
    }

    public void ResetSyncState()
    {
        _lastAppliedRoster = null;
    }

    private void ApplyRosterEntries(PartyManager party, List<PartyRosterEntry> entries, string serialized)
    {
        _lastAppliedRoster = serialized;
        ulong localSteamId = _lobby.LocalSteamId;
        ulong leaderSteamId = _lobby.GetLobbyOwnerSteamId();
        party.RebuildFromRoster(entries, localSteamId, leaderSteamId);
        MultiplayerDebug.LogParty(
            $"Roster applied partyCount={party.Members.Count} local={localSteamId} leader={leaderSteamId}");
        foreach (PartyMember member in party.Members)
        {
            MultiplayerDebug.LogParty(
                $"  party '{member.DisplayName}' {(member.IsLocallyOwned ? "LOCAL" : "REMOTE")} " +
                $"OWNER{member.OwnerId} TYPE {member.MemberType} steam={member.OwningSteamId}");
        }
    }

    private bool RosterOwnersMatchLobby(List<PartyRosterEntry> entries)
    {
        HashSet<ulong> rosterOwners = new();
        foreach (PartyRosterEntry entry in entries)
        {
            if (entry.OwningSteamId != 0)
            {
                rosterOwners.Add(entry.OwningSteamId);
            }
        }

        HashSet<ulong> lobbyIds = new();
        foreach (LobbyMemberInfo member in _lobby.GetLobbyMembers())
        {
            if (member.SteamId != 0)
            {
                lobbyIds.Add(member.SteamId);
            }
        }

        if (lobbyIds.Count != rosterOwners.Count)
        {
            return false;
        }

        foreach (ulong id in lobbyIds)
        {
            if (!rosterOwners.Contains(id))
            {
                return false;
            }
        }

        return true;
    }

    private bool LobbyHasUncoveredMembers(PartyManager party)
    {
        HashSet<ulong> partyOwners = new();
        foreach (PartyMember member in party.Members)
        {
            if (member.OwningSteamId != 0)
            {
                partyOwners.Add(member.OwningSteamId);
            }
        }

        foreach (LobbyMemberInfo lobbyMember in _lobby.GetLobbyMembers())
        {
            if (lobbyMember.SteamId != 0 && !partyOwners.Contains(lobbyMember.SteamId))
            {
                return true;
            }
        }

        return false;
    }

    private void PublishAuthoritativeRoster(PartyManager party)
    {
        if (!_lobby.IsLobbyOwner())
        {
            return;
        }

        List<PartyRosterEntry> entries = BuildAuthoritativeRoster(party);
        string serialized = PartyRosterCodec.Serialize(entries);
        string existing = _lobby.GetLobbyData(SteamConstants.LobbyDataPartyRoster) ?? string.Empty;
        if (string.Equals(existing, serialized, StringComparison.Ordinal))
        {
            MultiplayerDebug.LogParty($"PublishAuthoritativeRoster SKIPPED — unchanged entries={entries.Count}");
            return;
        }

        _lobby.PublishPartyRoster(serialized);
        _lastAppliedRoster = serialized;
        _lobby.SetLobbyData(SteamConstants.LobbyDataLeaderSteam, _lobby.GetLobbyOwnerSteamId().ToString());
        MultiplayerDebug.LogParty($"PublishAuthoritativeRoster entries={entries.Count} bytes={serialized.Length}");
        foreach (PartyRosterEntry entry in entries)
        {
            MultiplayerDebug.LogParty(
                $"  auth idx={entry.MemberIndex} '{entry.DisplayName}' ownerSteam={entry.OwningSteamId} type={entry.MemberType}");
        }
    }

    private List<PartyRosterEntry> BuildAuthoritativeRoster(PartyManager party)
    {
        List<PartyRosterEntry> entries = new();
        IReadOnlyList<LobbyMemberInfo> lobbyMembers = _lobby.GetLobbyMembers();
        int memberIndex = 0;

        foreach (LobbyMemberInfo lobbyMember in lobbyMembers)
        {
            string? localsData = _lobby.GetLobbyMemberData(lobbyMember.SteamId, SteamConstants.LobbyMemberDataLocals);
            List<(PartyMemberType type, int controllerId)> localSlots = PartyRosterCodec.DeserializeLocalSlots(localsData);
            // Any peer with empty locals gets a default keyboard slot (not spectator SteamRemote).
            if (localSlots.Count == 0)
            {
                localSlots.Add((PartyMemberType.LocalKeyboard, -1));
            }

            int localSlotOrdinal = 0;
            foreach ((PartyMemberType type, int controllerId) slot in localSlots)
            {
                string displayName = PartyDisplayNames.FormatLocalMemberName(
                    lobbyMember.DisplayName,
                    localSlotOrdinal++);
                entries.Add(new PartyRosterEntry
                {
                    MemberIndex = memberIndex++,
                    OwningSteamId = lobbyMember.SteamId,
                    SteamId = lobbyMember.SteamId,
                    DisplayName = displayName,
                    MemberType = slot.type,
                    ControllerId = slot.controllerId,
                    IsLeader = lobbyMember.IsOwner && slot.type == PartyMemberType.LocalKeyboard
                });
            }
        }

        if (entries.Count > PartyManager.MaxMembers)
        {
            entries.RemoveRange(PartyManager.MaxMembers, entries.Count - PartyManager.MaxMembers);
        }

        return entries;
    }

    private void RebuildFromLobbyMembersOnly(PartyManager party)
    {
        ulong localSteamId = _lobby.LocalSteamId;
        ulong leaderSteamId = _lobby.GetLobbyOwnerSteamId();
        List<PartyRosterEntry> entries = new();
        int memberIndex = 0;

        foreach (LobbyMemberInfo lobbyMember in _lobby.GetLobbyMembers())
        {
            bool isLocalMember = lobbyMember.SteamId == localSteamId;
            if (isLocalMember)
            {
                entries.Add(new PartyRosterEntry
                {
                    MemberIndex = memberIndex++,
                    OwningSteamId = lobbyMember.SteamId,
                    SteamId = lobbyMember.SteamId,
                    DisplayName = lobbyMember.DisplayName,
                    MemberType = PartyMemberType.LocalKeyboard,
                    ControllerId = -1,
                    IsLeader = lobbyMember.IsOwner
                });
            }
            else
            {
                entries.Add(new PartyRosterEntry
                {
                    MemberIndex = memberIndex++,
                    OwningSteamId = lobbyMember.SteamId,
                    SteamId = lobbyMember.SteamId,
                    DisplayName = lobbyMember.DisplayName,
                    MemberType = PartyMemberType.SteamRemote,
                    ControllerId = -1,
                    IsLeader = lobbyMember.IsOwner
                });
            }
        }

        party.RebuildFromRoster(entries, localSteamId, leaderSteamId);
    }
}
