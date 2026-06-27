namespace ColorBlocks;



public sealed class PartyMember

{

    public PartyMember(

        PartyMemberId id,

        string displayName,

        PartyMemberType memberType,

        PartyInputSource inputSource,

        int controllerId = -1,

        ulong steamId = 0,

        ulong owningSteamId = 0)

    {

        Id = id;

        DisplayName = displayName;

        MemberType = memberType;

        InputSource = inputSource;

        ControllerId = controllerId;

        SteamId = steamId;

        OwningSteamId = owningSteamId;

        IsLocallyOwned = memberType != PartyMemberType.SteamRemote;

    }



    public PartyMemberId Id { get; }

    public string DisplayName { get; set; }

    public PartyMemberType MemberType { get; private set; }

    public PartyInputSource InputSource { get; set; }

    public int ControllerId { get; set; }

    public ulong SteamId { get; set; }

    public ulong OwningSteamId { get; set; }

    public int MemberIndex { get; set; }

    public int NetworkPlayerId { get; set; }

    public int OwnerId { get; set; }

    public bool IsLeader { get; set; }

    public bool IsReady { get; set; }

    public bool IsLocallyOwned { get; set; }

    public bool IsLocal => IsLocallyOwned;

    public bool IsRemote => !IsLocallyOwned;



    public void SetLocalGamepad(int controllerId)

    {

        MemberType = PartyMemberType.LocalGamepad;

        InputSource = PartyInputSource.Gamepad;

        ControllerId = controllerId;

        IsLocallyOwned = true;

    }



    public void SetLocalKeyboard()

    {

        MemberType = PartyMemberType.LocalKeyboard;

        InputSource = PartyInputSource.Keyboard;

        ControllerId = -1;

        IsLocallyOwned = true;

    }



    public void SetSteamRemote(string displayName, ulong steamId = 0)

    {

        DisplayName = displayName;

        MemberType = PartyMemberType.SteamRemote;

        InputSource = PartyInputSource.SteamRemote;

        ControllerId = -1;

        SteamId = steamId;

        IsLocallyOwned = false;

    }



    public string GetInputLabel()

    {

        if (!IsLocallyOwned)

        {

            return DisplayName;

        }



        return InputSource switch

        {

            PartyInputSource.Keyboard => "Keyboard",

            PartyInputSource.Gamepad => $"Gamepad {ControllerId + 1}",

            PartyInputSource.SteamRemote => DisplayName,

            _ => "Unknown"

        };

    }

}

