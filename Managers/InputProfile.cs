using System.Collections.Generic;

namespace Game1_Monogame;

public sealed class InputProfile
{
    public InputProfile(PlayerId playerId, int playerIndex, InputDevice assignedInput, bool isActive)
    {
        PlayerId = playerId;
        PlayerIndex = playerIndex;
        AssignedInput = assignedInput;
        IsActive = isActive;
    }

    public PlayerId PlayerId { get; }
    public int PlayerIndex { get; }
    public string DisplayName => $"Player {PlayerIndex + 1}";
    public InputDevice AssignedInput { get; set; }
    public bool IsActive { get; set; }

    public static List<InputProfile> CreateDefaultProfiles()
    {
        return new List<InputProfile>
        {
            new(PlayerId.Player1, 0, InputDevice.Keyboard, isActive: true),
            new(PlayerId.Player2, 1, InputDevice.None, isActive: true),
            new(PlayerId.Player3, 2, InputDevice.None, isActive: false),
            new(PlayerId.Player4, 3, InputDevice.None, isActive: false)
        };
    }
}
