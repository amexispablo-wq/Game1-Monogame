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

        string locals = PartyRosterCodec.SerializeLocalSlots(localMembers, localSteamId);
        _lobby.SetLocalMemberData(SteamConstants.LobbyMemberDataLocals, locals);
    }

    public void PublishLocalPartyState(PartyManager party)
    {
        PublishLocalMemberData(party);
        if (_lobby.IsLobbyOwner())
        {
            PublishAuthoritativeRoster(party);
        }
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
            return;
        }

        _lastAppliedRoster = rosterData;
        List<PartyRosterEntry> entries = PartyRosterCodec.Deserialize(rosterData);
        if (entries.Count == 0)
        {
            RebuildFromLobbyMembersOnly(party);
            if (_lobby.IsLobbyOwner())
            {
                PublishAuthoritativeRoster(party);
            }

            return;
        }

        ulong localSteamId = _lobby.LocalSteamId;
        ulong leaderSteamId = _lobby.GetLobbyOwnerSteamId();
        party.RebuildFromRoster(entries, localSteamId, leaderSteamId);
    }

    public void ResetSyncState()
    {
        _lastAppliedRoster = null;
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
            return;
        }

        _lobby.PublishPartyRoster(serialized);
        _lastAppliedRoster = serialized;
        _lobby.SetLobbyData(SteamConstants.LobbyDataLeaderSteam, _lobby.GetLobbyOwnerSteamId().ToString());
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
            if (localSlots.Count == 0 && lobbyMember.IsOwner)
            {
                localSlots.Add((PartyMemberType.LocalKeyboard, -1));
            }

            if (localSlots.Count == 0)
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
                continue;
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
