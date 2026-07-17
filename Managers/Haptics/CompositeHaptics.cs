#nullable enable
namespace ColorBlocks;

/// <summary>
/// Composite haptics: Steam when available for the slot, else MonoGame gamepad, else no-op.
/// </summary>
public sealed class CompositeHaptics : IHaptics
{
    private readonly SteamInputManager _steamInput;
    private readonly SteamHaptics _steam;
    private readonly GamepadHaptics _gamepad;
    private readonly DummyHaptics _dummy = DummyHaptics.Instance;

    public CompositeHaptics(SteamInputManager steamInput)
    {
        _steamInput = steamInput;
        _steam = new SteamHaptics(steamInput);
        _gamepad = new GamepadHaptics();
    }

    public void Play(HapticEvent hapticEvent, int localPlayerSlot = 0)
    {
        if (_steamInput.IsInitialized && _steamInput.GetHandleForSlot(localPlayerSlot).m_InputHandle != 0)
        {
            _steam.Play(hapticEvent, localPlayerSlot);
            return;
        }

        if (localPlayerSlot >= 0 && localPlayerSlot < InputManager.MaxLocalPlayers)
        {
            _gamepad.Play(hapticEvent, localPlayerSlot);
            return;
        }

        _dummy.Play(hapticEvent, localPlayerSlot);
    }

    public void Stop(int localPlayerSlot = 0)
    {
        if (_steamInput.IsInitialized)
        {
            _steam.Stop(localPlayerSlot);
        }

        _gamepad.Stop(localPlayerSlot);
    }
}
