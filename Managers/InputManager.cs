#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game1_Monogame;

public sealed class InputManager : ILocalPlayerInputSource
{
    public const int MaxLocalPlayers = 4;

    private readonly Dictionary<PlayerId, PlayerInputState> _playerInputStates = new();
    private List<InputProfile> _profiles;
    private KeyboardInputBindings _keyboardBindings;
    private PlayerInputState _keyboardInputState;
    private PlayerId _keyboardControlledPlayerId = PlayerId.Player1;
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _currentMouse;
    private MouseState _previousMouse;

    public InputManager()
    {
        _profiles = InputProfile.CreateDefaultProfiles();
        _keyboardBindings = KeyboardInputBindings.FromSettings(SettingsManager.CurrentSettings);
        InitializeActionStates();
        SetKeyboardControlledPlayer(PlayerId.Player1);
    }

    public IReadOnlyList<InputProfile> Profiles => _profiles;
    public PlayerId KeyboardControlledPlayerId => _keyboardControlledPlayerId;
    public IEnumerable<InputProfile> ActiveProfiles
    {
        get
        {
            foreach (InputProfile profile in _profiles)
            {
                if (profile.IsActive)
                {
                    yield return profile;
                }
            }
        }
    }

    public int ActivePlayerCount
    {
        get
        {
            int count = 0;
            foreach (InputProfile profile in _profiles)
            {
                if (profile.IsActive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public float HorizontalMovement { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool FastFallHeld { get; private set; }
    public bool PullRopeHeld { get; private set; }
    public bool ExitPressed { get; private set; }
    public bool EnterPressed { get; private set; }
    public bool DebugTogglePressed { get; private set; }
    public bool LeftMousePressed { get; private set; }
    public bool LeftMouseHeld { get; private set; }
    public bool LeftMouseReleased { get; private set; }
    public bool RightMousePressed { get; private set; }
    public bool RightMouseHeld { get; private set; }
    public bool RightMouseReleased { get; private set; }
    public bool MiddleMousePressed { get; private set; }
    public bool MiddleMouseHeld { get; private set; }
    public bool MiddleMouseReleased { get; private set; }
    public Point MousePosition => new(_currentMouse.X, _currentMouse.Y);
    public Point PreviousMousePosition => new(_previousMouse.X, _previousMouse.Y);
    public Point MouseDelta => new(_currentMouse.X - _previousMouse.X, _currentMouse.Y - _previousMouse.Y);
    public int MouseWheelDelta { get; private set; }
    public bool ControlHeld { get; private set; }
    public bool ShiftHeld { get; private set; }
    public GameColor? RequestedColor { get; private set; }

    public void Update()
    {
        _previousKeyboard = _currentKeyboard;
        _currentKeyboard = Keyboard.GetState();
        _previousMouse = _currentMouse;
        _currentMouse = Mouse.GetState();

        _keyboardInputState = ReadKeyboardInputState();
        UpdatePlayerInputStates(_keyboardInputState);

        HorizontalMovement = _keyboardInputState.HorizontalMovement;
        JumpPressed = _keyboardInputState.JumpPressed;
        FastFallHeld = _keyboardInputState.FastFallHeld;
        PullRopeHeld = _keyboardInputState.PullRopeHeld;
        ExitPressed = IsNewKeyPress(Keys.Escape);
        EnterPressed = IsNewKeyPress(Keys.Enter);
        DebugTogglePressed = IsNewKeyPress(Keys.F3);
        ControlHeld = _currentKeyboard.IsKeyDown(Keys.LeftControl) || _currentKeyboard.IsKeyDown(Keys.RightControl);
        ShiftHeld = _currentKeyboard.IsKeyDown(Keys.LeftShift) || _currentKeyboard.IsKeyDown(Keys.RightShift);
        LeftMousePressed = _currentMouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton == ButtonState.Released;
        LeftMouseHeld = _currentMouse.LeftButton == ButtonState.Pressed;
        LeftMouseReleased = _currentMouse.LeftButton == ButtonState.Released
            && _previousMouse.LeftButton == ButtonState.Pressed;
        RightMousePressed = _currentMouse.RightButton == ButtonState.Pressed
            && _previousMouse.RightButton == ButtonState.Released;
        RightMouseHeld = _currentMouse.RightButton == ButtonState.Pressed;
        RightMouseReleased = _currentMouse.RightButton == ButtonState.Released
            && _previousMouse.RightButton == ButtonState.Pressed;
        MiddleMousePressed = _currentMouse.MiddleButton == ButtonState.Pressed
            && _previousMouse.MiddleButton == ButtonState.Released;
        MiddleMouseHeld = _currentMouse.MiddleButton == ButtonState.Pressed;
        MiddleMouseReleased = _currentMouse.MiddleButton == ButtonState.Released
            && _previousMouse.MiddleButton == ButtonState.Pressed;
        MouseWheelDelta = _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        RequestedColor = _keyboardInputState.RequestedColor ?? GetLegacyEditorRequestedColor();
    }

    public void ReloadProfilesFromSettings()
    {
        bool[] activeProfiles = new bool[MaxLocalPlayers];
        for (int i = 0; i < _profiles.Count && i < activeProfiles.Length; i++)
        {
            activeProfiles[i] = _profiles[i].IsActive;
        }

        PlayerId previousControlledPlayerId = _keyboardControlledPlayerId;
        _profiles = InputProfile.CreateDefaultProfiles();
        for (int i = 0; i < _profiles.Count && i < activeProfiles.Length; i++)
        {
            _profiles[i].IsActive = activeProfiles[i];
        }

        _keyboardBindings = KeyboardInputBindings.FromSettings(SettingsManager.CurrentSettings);
        InitializeActionStates();
        SetKeyboardControlledPlayer(IsActivePlayer(previousControlledPlayerId)
            ? previousControlledPlayerId
            : GetFirstActivePlayerId());
    }

    public void SetActivePlayerCount(int activePlayerCount)
    {
        int clampedCount = Math.Clamp(activePlayerCount, 1, MaxLocalPlayers);
        for (int i = 0; i < _profiles.Count; i++)
        {
            _profiles[i].IsActive = i < clampedCount;
        }

        if (!IsActivePlayer(_keyboardControlledPlayerId))
        {
            SetKeyboardControlledPlayer(GetFirstActivePlayerId());
        }
    }

    public void SetKeyboardControlledPlayer(PlayerId playerId)
    {
        if (!IsActivePlayer(playerId))
        {
            return;
        }

        _keyboardControlledPlayerId = playerId;
        foreach (InputProfile profile in _profiles)
        {
            if (profile.AssignedInput.DeviceType == InputDeviceType.Keyboard)
            {
                profile.AssignedInput = InputDevice.None;
            }

            if (profile.PlayerId == playerId)
            {
                profile.AssignedInput = InputDevice.Keyboard;
            }
        }

        UpdatePlayerInputStates(_keyboardInputState);
    }

    public PlayerInputState GetPlayerInput(PlayerId playerId)
    {
        return _playerInputStates.TryGetValue(playerId, out PlayerInputState state)
            ? state
            : PlayerInputState.Empty;
    }

    public bool IsKeyDown(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key);
    }

    public bool IsNewKeyPress(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void InitializeActionStates()
    {
        _playerInputStates.Clear();
        foreach (InputProfile profile in _profiles)
        {
            _playerInputStates[profile.PlayerId] = PlayerInputState.Empty;
        }
    }

    private void UpdatePlayerInputStates(PlayerInputState keyboardInputState)
    {
        foreach (InputProfile profile in _profiles)
        {
            _playerInputStates[profile.PlayerId] = profile.IsActive && profile.PlayerId == _keyboardControlledPlayerId
                ? keyboardInputState
                : PlayerInputState.Empty;
        }
    }

    private PlayerInputState ReadKeyboardInputState()
    {
        float horizontalMovement = 0f;
        if (_currentKeyboard.IsKeyDown(_keyboardBindings.MoveLeft))
        {
            horizontalMovement -= 1f;
        }

        if (_currentKeyboard.IsKeyDown(_keyboardBindings.MoveRight))
        {
            horizontalMovement += 1f;
        }

        GameColor? requestedColor = null;
        if (IsNewKeyPress(_keyboardBindings.Red))
        {
            requestedColor = GameColor.Red;
        }
        else if (IsNewKeyPress(_keyboardBindings.Blue))
        {
            requestedColor = GameColor.Blue;
        }
        else if (IsNewKeyPress(_keyboardBindings.Green))
        {
            requestedColor = GameColor.Green;
        }

        return new PlayerInputState(
            horizontalMovement,
            IsNewKeyPress(_keyboardBindings.Jump),
            _currentKeyboard.IsKeyDown(_keyboardBindings.FastFall),
            _currentKeyboard.IsKeyDown(_keyboardBindings.PullRope),
            requestedColor);
    }

    private bool IsActivePlayer(PlayerId playerId)
    {
        foreach (InputProfile profile in _profiles)
        {
            if (profile.PlayerId == playerId && profile.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    private PlayerId GetFirstActivePlayerId()
    {
        foreach (InputProfile profile in _profiles)
        {
            if (profile.IsActive)
            {
                return profile.PlayerId;
            }
        }

        return PlayerId.Player1;
    }

    private GameColor? GetLegacyEditorRequestedColor()
    {
        if (IsNewKeyPress(Keys.R))
        {
            return GameColor.Red;
        }

        if (IsNewKeyPress(Keys.B))
        {
            return GameColor.Blue;
        }

        if (IsNewKeyPress(Keys.G))
        {
            return GameColor.Green;
        }

        return null;
    }

    private readonly record struct KeyboardInputBindings(
        Keys MoveLeft,
        Keys MoveRight,
        Keys Jump,
        Keys FastFall,
        Keys PullRope,
        Keys Red,
        Keys Blue,
        Keys Green)
    {
        public static KeyboardInputBindings FromSettings(GameSettings settings)
        {
            return new KeyboardInputBindings(
                GetSettingKey(settings, "MoveLeft", Keys.A),
                GetSettingKey(settings, "MoveRight", Keys.D),
                GetSettingKey(settings, "Jump", Keys.W),
                GetSettingKey(settings, "FastFall", Keys.S),
                GetSettingKey(settings, "PullRope", Keys.Space),
                GetSettingKey(settings, "Red", Keys.J),
                GetSettingKey(settings, "Blue", Keys.K),
                GetSettingKey(settings, "Green", Keys.L));
        }

        private static Keys GetSettingKey(GameSettings settings, string actionName, Keys fallback)
        {
            if (settings.Keybindings.TryGetValue(actionName, out string? keyName)
                && Enum.TryParse(keyName, ignoreCase: true, out Keys key))
            {
                return key;
            }

            return fallback;
        }
    }
}
