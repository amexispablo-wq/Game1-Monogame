#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

public sealed class PartyManager
{
    public const int MaxMembers = 4;

    private readonly List<PartyMember> _members = new();
    private int _nextMemberId = 1;
    private SteamLobbyService? _steamLobby;
    private SteamPartyService? _steamParty;

    public IReadOnlyList<PartyMember> Members => _members;
    public bool AssignmentsLocked { get; private set; }
    public bool IsInSteamLobby => _steamLobby?.IsInLobby == true;
    public ulong? LobbyId => IsInSteamLobby ? _steamLobby!.CurrentLobbyId : null;
    public bool IsLeader => _steamLobby?.IsLobbyOwner() ?? true;

    public PartyMember? Leader
    {
        get
        {
            foreach (PartyMember member in _members)
            {
                if (member.IsLeader)
                {
                    return member;
                }
            }

            return _members.Count > 0 ? _members[0] : null;
        }
    }

    public event Action? Changed;
    public event Action<SteamPartyError, string>? ErrorOccurred;

    public void BindSteamServices(SteamLobbyService lobby, SteamPartyService party)
    {
        _steamLobby = lobby;
        _steamParty = party;
        _steamLobby.LobbyStateChanged += HandleLobbyStateChanged;
        _steamLobby.LobbyReady += HandleLobbyReady;
        _steamLobby.ErrorOccurred += (error, message) => ErrorOccurred?.Invoke(error, message);
    }

    private void HandleLobbyReady()
    {
        _steamParty?.PublishLocalMemberData(this);
    }

    public void EnsureDefaultParty()
    {
        if (_members.Count > 0)
        {
            return;
        }

        ulong owningSteamId = _steamLobby?.LocalSteamId ?? 0;
        PartyMember keyboardMember = CreateMember(
            "Player 1",
            PartyMemberType.LocalKeyboard,
            PartyInputSource.Keyboard,
            owningSteamId: owningSteamId);
        keyboardMember.IsLeader = true;
        _members.Add(keyboardMember);
        NotifyChanged();
    }

    public void EnsureSteamParty()
    {
        if (_steamLobby is null || !_steamLobby.IsAvailable)
        {
            EnsureDefaultParty();
            return;
        }

        _steamLobby.EnsurePartyLobby();
    }

    public void Clear()
    {
        _members.Clear();
        _nextMemberId = 1;
        AssignmentsLocked = false;
        NotifyChanged();
    }

    public void LeaveParty()
    {
        _steamLobby?.LeaveLobby();
        _steamParty?.ResetSyncState();
        Clear();
        EnsureDefaultParty();
    }

    public bool TryKickMember(PartyMemberId memberId)
    {
        if (AssignmentsLocked)
        {
            return false;
        }

        PartyMember? member = GetMember(memberId);
        if (member is null || member.IsLeader)
        {
            return false;
        }

        if (member.IsLocallyOwned && member.InputSource == PartyInputSource.Keyboard)
        {
            return false;
        }

        if (!member.IsLocallyOwned || member.MemberType == PartyMemberType.SteamRemote)
        {
            if (_steamLobby is null || !_steamLobby.IsInLobby || !_steamLobby.IsLobbyOwner())
            {
                return false;
            }

            _steamLobby.KickMember(member.SteamId);
            return true;
        }

        if (member.MemberType == PartyMemberType.LocalGamepad)
        {
            return TryLeaveGamepad(member.ControllerId);
        }

        return false;
    }

    public void LockAssignments()
    {
        AssignmentsLocked = true;
        AssignNetworkPlayerIds();
    }

    public void ApplyPreferredInputForPrimaryLocalMember(InputManager input)
    {
        if (AssignmentsLocked)
        {
            return;
        }

        PartyMember? primary = null;
        foreach (PartyMember member in _members)
        {
            if (member.IsLocallyOwned)
            {
                primary = member;
                break;
            }
        }

        if (primary is null)
        {
            return;
        }

        if (input.LastUsedPartyInputSource == PartyInputSource.Gamepad && input.LastUsedPartyControllerId >= 0)
        {
            TryAssignInput(primary.Id, PartyInputSource.Gamepad, input.LastUsedPartyControllerId);
            return;
        }

        if (input.LastUsedPartyInputSource == PartyInputSource.Keyboard)
        {
            TryAssignInput(primary.Id, PartyInputSource.Keyboard);
        }
    }

    public void UnlockAssignments()
    {
        AssignmentsLocked = false;
    }

    public void AssignNetworkPlayerIds()
    {
        for (int i = 0; i < _members.Count; i++)
        {
            PartyMember member = _members[i];
            member.NetworkPlayerId = i + 1;
            member.OwnerId = SteamOwnerId.FromSteamId(member.OwningSteamId);
            member.MemberIndex = i;
        }
    }

    public void RebuildFromRoster(IReadOnlyList<PartyRosterEntry> entries, ulong localSteamId, ulong leaderSteamId)
    {
        if (AssignmentsLocked)
        {
            return;
        }

        _members.Clear();
        foreach (PartyRosterEntry entry in entries)
        {
            bool isLocal = entry.OwningSteamId == localSteamId;
            PartyMember member = CreateMember(
                entry.DisplayName,
                isLocal ? entry.MemberType : PartyMemberType.SteamRemote,
                isLocal ? ToInputSource(entry.MemberType, entry.ControllerId) : PartyInputSource.SteamRemote,
                controllerId: entry.ControllerId,
                steamId: entry.SteamId,
                owningSteamId: entry.OwningSteamId);
            member.MemberIndex = entry.MemberIndex;
            member.IsLeader = entry.OwningSteamId == leaderSteamId && entry.IsLeader;
            member.OwnerId = SteamOwnerId.FromSteamId(entry.OwningSteamId);
            member.IsLocallyOwned = isLocal;

            if (isLocal)
            {
                if (entry.MemberType == PartyMemberType.LocalKeyboard)
                {
                    member.SetLocalKeyboard();
                }
                else if (entry.MemberType == PartyMemberType.LocalGamepad)
                {
                    member.SetLocalGamepad(entry.ControllerId);
                }
            }
            else
            {
                member.SetSteamRemote(entry.DisplayName, entry.SteamId);
            }

            _members.Add(member);
        }

        NotifyChanged();
    }

    public PartyMember? GetMember(PartyMemberId id)
    {
        foreach (PartyMember member in _members)
        {
            if (member.Id == id)
            {
                return member;
            }
        }

        return null;
    }

    public bool IsControllerAssigned(int controllerId)
    {
        foreach (PartyMember member in _members)
        {
            if (member.IsLocallyOwned
                && member.InputSource == PartyInputSource.Gamepad
                && member.ControllerId == controllerId)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsKeyboardAssigned()
    {
        foreach (PartyMember member in _members)
        {
            if (member.IsLocallyOwned && member.InputSource == PartyInputSource.Keyboard)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryJoinGamepad(int controllerIndex)
    {
        if (AssignmentsLocked || _members.Count >= MaxMembers || IsControllerAssigned(controllerIndex))
        {
            return false;
        }

        ulong owningSteamId = _steamLobby?.LocalSteamId ?? 0;
        PartyMember member = CreateMember(
            $"Player {_members.Count + 1}",
            PartyMemberType.LocalGamepad,
            PartyInputSource.Gamepad,
            controllerIndex,
            owningSteamId: owningSteamId);
        _members.Add(member);
        PublishLocalState();
        NotifyChanged();
        return true;
    }

    public bool TryLeaveGamepad(int controllerIndex)
    {
        if (AssignmentsLocked)
        {
            return false;
        }

        for (int i = _members.Count - 1; i >= 0; i--)
        {
            PartyMember member = _members[i];
            if (!member.IsLocallyOwned
                || member.MemberType != PartyMemberType.LocalGamepad
                || member.ControllerId != controllerIndex)
            {
                continue;
            }

            _members.RemoveAt(i);
            RenumberDisplayNames();
            PublishLocalState();
            NotifyChanged();
            return true;
        }

        return false;
    }

    public bool TryAssignInput(PartyMemberId memberId, PartyInputSource source, int controllerId = -1)
    {
        if (AssignmentsLocked)
        {
            return false;
        }

        PartyMember? member = GetMember(memberId);
        if (member is null || !member.IsLocallyOwned)
        {
            return false;
        }

        switch (source)
        {
            case PartyInputSource.Keyboard:
                if (IsKeyboardAssignedByOther(member))
                {
                    return false;
                }

                member.SetLocalKeyboard();
                PublishLocalState();
                NotifyChanged();
                return true;

            case PartyInputSource.Gamepad:
                if (controllerId < 0 || controllerId >= InputManager.MaxLocalPlayers)
                {
                    return false;
                }

                if (!GamePad.GetState((Microsoft.Xna.Framework.PlayerIndex)controllerId).IsConnected)
                {
                    return false;
                }

                if (IsControllerAssignedByOther(member, controllerId))
                {
                    return false;
                }

                member.SetLocalGamepad(controllerId);
                PublishLocalState();
                NotifyChanged();
                return true;

            default:
                return false;
        }
    }

    public bool TryCycleMemberInput(PartyMemberId memberId, int direction, Func<int, bool> isGamepadConnected)
    {
        PartyMember? member = GetMember(memberId);
        if (member is null || !member.IsLocallyOwned || AssignmentsLocked)
        {
            return false;
        }

        List<(PartyInputSource source, int controllerId)> options = BuildInputOptions(member, isGamepadConnected);
        if (options.Count == 0)
        {
            return false;
        }

        int currentIndex = FindOptionIndex(options, member.InputSource, member.ControllerId);
        int nextIndex = (currentIndex + direction + options.Count) % options.Count;
        (PartyInputSource source, int controllerId) next = options[nextIndex];
        return TryAssignInput(memberId, next.source, next.controllerId);
    }

    public void ProcessGamepadJoinLeave(InputManager input)
    {
        if (AssignmentsLocked)
        {
            return;
        }

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (!input.IsGamepadConnected(i))
            {
                continue;
            }

            if (input.WasGamepadPressed(i, Buttons.Start) && !IsControllerAssigned(i))
            {
                TryJoinGamepad(i);
            }

            if (input.WasGamepadPressed(i, Buttons.Back) && IsControllerAssigned(i))
            {
                TryLeaveGamepad(i);
            }
        }
    }

    public List<(PartyInputSource source, int controllerId)> BuildInputOptions(
        PartyMember member,
        Func<int, bool> isGamepadConnected)
    {
        List<(PartyInputSource source, int controllerId)> options = new();

        if (!IsKeyboardAssignedByOther(member))
        {
            options.Add((PartyInputSource.Keyboard, -1));
        }

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (!isGamepadConnected(i) || IsControllerAssignedByOther(member, i))
            {
                continue;
            }

            options.Add((PartyInputSource.Gamepad, i));
        }

        return options;
    }

    private void HandleLobbyStateChanged()
    {
        _steamParty?.RebuildPartyFromLobby(this);
    }

    private void PublishLocalState()
    {
        _steamParty?.PublishLocalPartyState(this);
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }

    private PartyMember CreateMember(
        string displayName,
        PartyMemberType memberType,
        PartyInputSource inputSource,
        int controllerId = -1,
        ulong steamId = 0,
        ulong owningSteamId = 0)
    {
        return new PartyMember(
            new PartyMemberId(_nextMemberId++),
            displayName,
            memberType,
            inputSource,
            controllerId,
            steamId,
            owningSteamId);
    }

    private static PartyInputSource ToInputSource(PartyMemberType memberType, int controllerId)
    {
        return memberType switch
        {
            PartyMemberType.LocalKeyboard => PartyInputSource.Keyboard,
            PartyMemberType.LocalGamepad => PartyInputSource.Gamepad,
            _ => PartyInputSource.SteamRemote
        };
    }

    private bool IsKeyboardAssignedByOther(PartyMember member)
    {
        foreach (PartyMember other in _members)
        {
            if (other.Id != member.Id && other.IsLocallyOwned && other.InputSource == PartyInputSource.Keyboard)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsControllerAssignedByOther(PartyMember member, int controllerId)
    {
        foreach (PartyMember other in _members)
        {
            if (other.Id != member.Id
                && other.IsLocallyOwned
                && other.InputSource == PartyInputSource.Gamepad
                && other.ControllerId == controllerId)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindOptionIndex(
        List<(PartyInputSource source, int controllerId)> options,
        PartyInputSource source,
        int controllerId)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].source == source && options[i].controllerId == controllerId)
            {
                return i;
            }
        }

        return 0;
    }

    private void RenumberDisplayNames()
    {
        int localIndex = 1;
        for (int i = 0; i < _members.Count; i++)
        {
            PartyMember member = _members[i];
            if (member.IsLocallyOwned && member.MemberType != PartyMemberType.SteamRemote)
            {
                member.DisplayName = $"Player {localIndex++}";
            }
        }
    }
}
