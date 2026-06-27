#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

public sealed class InputManager : ILocalPlayerInputSource
{
    public const int MaxLocalPlayers = 4;
    private const float GamepadMoveDeadZone = GamepadDefaults.MoveDeadZone;

    private readonly Dictionary<int, PlayerInputState> _gameplayInputByNetworkId = new();
    private readonly Dictionary<int, PartyMember> _gameplayBindings = new();
    private KeyboardInputBindings _keyboardBindings;
    private GamepadButtonBindings _gamepadBindings;
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _currentMouse;
    private MouseState _previousMouse;
    private readonly GamePadState[] _currentGamepads = new GamePadState[MaxLocalPlayers];
    private readonly GamePadState[] _previousGamepads = new GamePadState[MaxLocalPlayers];

    public InputManager()
    {
        _keyboardBindings = KeyboardInputBindings.FromSettings(SettingsManager.CurrentSettings);
        _gamepadBindings = GamepadButtonBindings.FromSettings(SettingsManager.CurrentSettings);
    }

    public bool ExitPressed { get; private set; }
    public bool EnterPressed { get; private set; }
    public bool DebugTogglePressed { get; private set; }
    public bool NavigationStepPressed { get; private set; }
    public bool GameplayPausePressed { get; private set; }
    public bool MenuMoveUpPressed { get; private set; }
    public bool MenuMoveDownPressed { get; private set; }
    public bool MenuConfirmPressed { get; private set; }
    public bool MenuConfirmHeld { get; private set; }
    public bool MenuCancelPressed { get; private set; }
    public bool MenuStickUpHeld { get; private set; }
    public bool MenuStickDownHeld { get; private set; }
    public bool MenuMoveLeftPressed { get; private set; }
    public bool MenuMoveRightPressed { get; private set; }
    public bool MenuTabPressed { get; private set; }
    public bool MenuTabBackwardPressed { get; private set; }
    public bool MenuStickLeftHeld { get; private set; }
    public bool MenuStickRightHeld { get; private set; }
    public bool KeyboardMenuConfirmPressed { get; private set; }
    public bool KeyboardMenuCancelPressed { get; private set; }
    public bool GamepadMenuConfirmPressed { get; private set; }
    public bool GamepadMenuCancelPressed { get; private set; }
    public bool MouseActivityThisFrame { get; private set; }
    public bool KeyboardMenuActivityThisFrame { get; private set; }
    public bool GamepadActivityThisFrame { get; private set; }
    public bool GamepadMenuActivityThisFrame { get; private set; }
    public bool KeyboardMenuMoveUpPressed { get; private set; }
    public bool KeyboardMenuMoveDownPressed { get; private set; }
    public bool KeyboardMenuMoveLeftPressed { get; private set; }
    public bool KeyboardMenuMoveRightPressed { get; private set; }
    public bool GamepadMenuMoveUpPressed { get; private set; }
    public bool GamepadMenuMoveDownPressed { get; private set; }
    public bool GamepadMenuMoveLeftPressed { get; private set; }
    public bool GamepadMenuMoveRightPressed { get; private set; }
    public InputNavigationService Navigation { get; } = new();
    private Point? _uiPointerOverride;
    private float _pointerScaleX = 1f;
    private float _pointerScaleY = 1f;

    public bool UiPointerPressed { get; private set; }
    public bool UiPointerHeld { get; private set; }
    public bool UiPointerReleased { get; private set; }
    public Point UiPointerPosition => TransformPointer(_uiPointerOverride ?? MousePosition);
    public bool IsMouseRecentlyActive => _mouseInactiveFrames < 45;
    public float EditorLeftTrigger { get; private set; }
    public float EditorRightTrigger { get; private set; }
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

    public bool GameplayInputBlocked { get; set; }

    private bool _virtualLeftClickRequested;
    private int _mouseInactiveFrames;

    public bool LeftMousePressed { get; private set; }

    public void Update()
    {
        NavigationDebug.BeginFrame();
        _previousKeyboard = _currentKeyboard;
        _currentKeyboard = Keyboard.GetState();
        _previousMouse = _currentMouse;
        _currentMouse = Mouse.GetState();

        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            _previousGamepads[i] = _currentGamepads[i];
            _currentGamepads[i] = GamePad.GetState((PlayerIndex)i);
        }

        UpdateMenuNavigation();
        UpdateSystemButtons();
        UpdateGameplayInputs();
        Navigation.Update(this);
        RequestedColor = GetLegacyEditorRequestedColor();
        _virtualLeftClickRequested = false;
    }

    public void SetUiPointerOverride(Point? position)
    {
        _uiPointerOverride = position;
    }

    public void ConfigurePointerTransform(Rectangle clientBounds, Viewport backBufferViewport, PresentationManager presentation)
    {
        if (clientBounds.Width <= 0 || clientBounds.Height <= 0
            || backBufferViewport.Width <= 0 || backBufferViewport.Height <= 0)
        {
            _pointerScaleX = 1f;
            _pointerScaleY = 1f;
            _presentationMapper = null;
            return;
        }

        _pointerScaleX = backBufferViewport.Width / (float)clientBounds.Width;
        _pointerScaleY = backBufferViewport.Height / (float)clientBounds.Height;
        _presentationMapper = presentation;
    }

    private PresentationManager? _presentationMapper;

    private Point TransformPointer(Point pointer)
    {
        Point backBufferPointer = pointer;
        if (Math.Abs(_pointerScaleX - 1f) >= 0.001f || Math.Abs(_pointerScaleY - 1f) >= 0.001f)
        {
            backBufferPointer = new Point(
                (int)MathF.Round(pointer.X * _pointerScaleX),
                (int)MathF.Round(pointer.Y * _pointerScaleY));
        }

        if (_presentationMapper is null)
        {
            return backBufferPointer;
        }

        return _presentationMapper.MapPointerToLogical(backBufferPointer);
    }

    public void RequestVirtualLeftClick()
    {
        _virtualLeftClickRequested = true;
    }

    public bool IsAnyGamepadConnected()
    {
        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            if (_currentGamepads[i].IsConnected)
            {
                return true;
            }
        }

        return false;
    }

    public Vector2 GetMenuLeftStick()
    {
        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            if (_currentGamepads[i].IsConnected)
            {
                return _currentGamepads[i].ThumbSticks.Left;
            }
        }

        return Vector2.Zero;
    }

    public Vector2 GetMenuRightStick()
    {
        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            if (_currentGamepads[i].IsConnected)
            {
                return _currentGamepads[i].ThumbSticks.Right;
            }
        }

        return Vector2.Zero;
    }

    public void ReloadKeyboardBindings()
    {
        _keyboardBindings = KeyboardInputBindings.FromSettings(SettingsManager.CurrentSettings);
        _gamepadBindings = GamepadButtonBindings.FromSettings(SettingsManager.CurrentSettings);
    }

    public void SetGameplayBindings(IReadOnlyDictionary<int, PartyMember> bindings)
    {
        _gameplayBindings.Clear();
        foreach (KeyValuePair<int, PartyMember> entry in bindings)
        {
            _gameplayBindings[entry.Key] = entry.Value;
        }
    }

    public void ClearGameplayBindings()
    {
        _gameplayBindings.Clear();
        _gameplayInputByNetworkId.Clear();
    }

    public PlayerInputState GetPlayerInput(int networkId)
    {
        return _gameplayInputByNetworkId.TryGetValue(networkId, out PlayerInputState state)
            ? state
            : PlayerInputState.Empty;
    }

    public bool IsGamepadConnected(int deviceIndex)
    {
        return deviceIndex >= 0
            && deviceIndex < MaxLocalPlayers
            && _currentGamepads[deviceIndex].IsConnected;
    }

    public bool WasGamepadPressed(int deviceIndex, Buttons button)
    {
        if (deviceIndex < 0 || deviceIndex >= MaxLocalPlayers)
        {
            return false;
        }

        return IsGamepadPressed(_currentGamepads[deviceIndex], _previousGamepads[deviceIndex], button);
    }

    public bool IsKeyDown(Keys key) => _currentKeyboard.IsKeyDown(key);

    public bool IsNewKeyPress(Keys key) =>
        _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);

    private void UpdateGameplayInputs()
    {
        _gameplayInputByNetworkId.Clear();
        if (GameplayInputBlocked)
        {
            return;
        }

        foreach (KeyValuePair<int, PartyMember> binding in _gameplayBindings)
        {
            _gameplayInputByNetworkId[binding.Key] = ReadMemberInput(binding.Value);
        }
    }

    private PlayerInputState ReadMemberInput(PartyMember member)
    {
        return member.InputSource switch
        {
            PartyInputSource.Keyboard => ReadKeyboardInputState(),
            PartyInputSource.Gamepad => ReadGamepadInputState(member.ControllerId),
            PartyInputSource.SteamRemote => PlayerInputState.Empty,
            _ => PlayerInputState.Empty
        };
    }

    private void UpdateSystemButtons()
    {
        ExitPressed = IsNewKeyPress(Keys.Escape);
        EnterPressed = IsNewKeyPress(Keys.Enter);
        DebugTogglePressed = IsNewKeyPress(Keys.F3);

        if (IsNewKeyPress(Keys.F8))
        {
            NavigationDebug.Enabled = !NavigationDebug.Enabled;
        }

        NavigationStepPressed = IsNewKeyPress(Keys.F9);
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

        if (LeftMousePressed || RightMousePressed || MiddleMousePressed || MouseWheelDelta != 0)
        {
            _mouseInactiveFrames = 0;
            MouseActivityThisFrame = true;
        }
        else if (MouseDelta.X != 0 || MouseDelta.Y != 0)
        {
            _mouseInactiveFrames = 0;
            MouseActivityThisFrame = false;
        }
        else
        {
            _mouseInactiveFrames++;
            MouseActivityThisFrame = false;
        }

        UiPointerPressed = LeftMousePressed || _virtualLeftClickRequested;
        UiPointerHeld = LeftMouseHeld || _virtualLeftClickRequested;
        UiPointerReleased = LeftMouseReleased;
    }

    private void UpdateMenuNavigation()
    {
        MouseActivityThisFrame = false;
        KeyboardMenuActivityThisFrame = false;
        GamepadActivityThisFrame = false;
        GamepadMenuActivityThisFrame = false;

        KeyboardMenuMoveUpPressed = IsNewKeyPress(Keys.Up);
        KeyboardMenuMoveDownPressed = IsNewKeyPress(Keys.Down);
        KeyboardMenuMoveLeftPressed = IsNewKeyPress(Keys.Left);
        KeyboardMenuMoveRightPressed = IsNewKeyPress(Keys.Right);
        GamepadMenuMoveUpPressed = false;
        GamepadMenuMoveDownPressed = false;
        GamepadMenuMoveLeftPressed = false;
        GamepadMenuMoveRightPressed = false;

        MenuMoveUpPressed = KeyboardMenuMoveUpPressed;
        MenuMoveDownPressed = KeyboardMenuMoveDownPressed;
        MenuMoveLeftPressed = KeyboardMenuMoveLeftPressed;
        MenuMoveRightPressed = KeyboardMenuMoveRightPressed;
        MenuTabBackwardPressed = IsNewKeyPress(Keys.Tab) && ShiftHeld;
        MenuTabPressed = IsNewKeyPress(Keys.Tab) && !ShiftHeld;
        KeyboardMenuConfirmPressed = IsNewKeyPress(Keys.Enter);
        KeyboardMenuCancelPressed = IsNewKeyPress(Keys.Escape);
        MenuConfirmPressed = KeyboardMenuConfirmPressed;
        MenuConfirmHeld = _currentKeyboard.IsKeyDown(Keys.Enter);
        MenuCancelPressed = KeyboardMenuCancelPressed;
        GamepadMenuConfirmPressed = false;
        GamepadMenuCancelPressed = false;

        if (KeyboardMenuMoveUpPressed || KeyboardMenuMoveDownPressed || KeyboardMenuMoveLeftPressed || KeyboardMenuMoveRightPressed
            || MenuTabPressed || MenuTabBackwardPressed || KeyboardMenuConfirmPressed || KeyboardMenuCancelPressed)
        {
            KeyboardMenuActivityThisFrame = true;
        }

        bool stickUp = false;
        bool stickDown = false;
        bool stickLeft = false;
        bool stickRight = false;
        bool gamepadPause = false;
        EditorLeftTrigger = 0f;
        EditorRightTrigger = 0f;

        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            if (!_currentGamepads[i].IsConnected)
            {
                continue;
            }

            GamePadState current = _currentGamepads[i];
            GamePadState previous = _previousGamepads[i];

            if (IsGamepadPressed(current, previous, GamepadDefaults.MenuConfirmButton))
            {
                GamepadMenuConfirmPressed = true;
                GamepadMenuActivityThisFrame = true;
                GamepadActivityThisFrame = true;
            }

            MenuConfirmHeld |= current.IsButtonDown(GamepadDefaults.MenuConfirmButton);

            if (IsGamepadPressed(current, previous, GamepadDefaults.MenuCancelButton))
            {
                GamepadMenuCancelPressed = true;
                GamepadMenuActivityThisFrame = true;
                GamepadActivityThisFrame = true;
            }

            gamepadPause |= IsGamepadPressed(current, previous, GamepadDefaults.PauseButton);

            if (current.DPad.Up == ButtonState.Pressed && previous.DPad.Up == ButtonState.Released)
            {
                GamepadMenuMoveUpPressed = true;
                MenuMoveUpPressed = true;
                GamepadMenuActivityThisFrame = true;
                GamepadActivityThisFrame = true;
            }

            if (current.DPad.Down == ButtonState.Pressed && previous.DPad.Down == ButtonState.Released)
            {
                GamepadMenuMoveDownPressed = true;
                MenuMoveDownPressed = true;
                GamepadMenuActivityThisFrame = true;
                GamepadActivityThisFrame = true;
            }

            if (current.DPad.Left == ButtonState.Pressed && previous.DPad.Left == ButtonState.Released)
            {
                GamepadMenuMoveLeftPressed = true;
                MenuMoveLeftPressed = true;
                GamepadMenuActivityThisFrame = true;
                GamepadActivityThisFrame = true;
            }

            if (current.DPad.Right == ButtonState.Pressed && previous.DPad.Right == ButtonState.Released)
            {
                GamepadMenuMoveRightPressed = true;
                MenuMoveRightPressed = true;
                GamepadMenuActivityThisFrame = true;
                GamepadActivityThisFrame = true;
            }

            if (current.ThumbSticks.Left.X < -GamepadMoveDeadZone)
            {
                stickLeft = true;
                GamepadActivityThisFrame = true;
            }

            if (current.ThumbSticks.Left.X > GamepadMoveDeadZone)
            {
                stickRight = true;
                GamepadActivityThisFrame = true;
            }

            if (current.ThumbSticks.Left.Y > GamepadMoveDeadZone)
            {
                stickUp = true;
                GamepadActivityThisFrame = true;
            }

            if (current.ThumbSticks.Left.Y < -GamepadMoveDeadZone)
            {
                stickDown = true;
                GamepadActivityThisFrame = true;
            }

            if (current.ThumbSticks.Right.LengthSquared() > GamepadMoveDeadZone * GamepadMoveDeadZone)
            {
                GamepadActivityThisFrame = true;
            }

            if (current.Triggers.Left > 0.1f || current.Triggers.Right > 0.1f)
            {
                GamepadActivityThisFrame = true;
            }

            if (HasGamepadButtonActivity(current, previous))
            {
                GamepadMenuActivityThisFrame = true;
                GamepadActivityThisFrame = true;
            }

            EditorLeftTrigger = MathF.Max(EditorLeftTrigger, current.Triggers.Left);
            EditorRightTrigger = MathF.Max(EditorRightTrigger, current.Triggers.Right);
        }

        MenuConfirmPressed |= GamepadMenuConfirmPressed;
        MenuCancelPressed |= GamepadMenuCancelPressed;
        GameplayPausePressed = KeyboardMenuCancelPressed || gamepadPause;
        MenuStickUpHeld = stickUp;
        MenuStickDownHeld = stickDown;
        MenuStickLeftHeld = stickLeft;
        MenuStickRightHeld = stickRight;
    }

    private static bool HasGamepadButtonActivity(GamePadState current, GamePadState previous)
    {
        return current.Buttons != previous.Buttons
            || current.DPad != previous.DPad;
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
            IsNewKeyPress(_keyboardBindings.Respawn),
            _currentKeyboard.IsKeyDown(_keyboardBindings.FastFall),
            _currentKeyboard.IsKeyDown(_keyboardBindings.PullRope),
            requestedColor);
    }

    private PlayerInputState ReadGamepadInputState(int deviceIndex)
    {
        if (deviceIndex < 0 || deviceIndex >= MaxLocalPlayers)
        {
            return PlayerInputState.Empty;
        }

        GamePadState current = _currentGamepads[deviceIndex];
        GamePadState previous = _previousGamepads[deviceIndex];
        if (!current.IsConnected)
        {
            return PlayerInputState.Empty;
        }

        float horizontal = 0f;
        if (current.ThumbSticks.Left.X < -GamepadMoveDeadZone || current.DPad.Left == ButtonState.Pressed)
        {
            horizontal -= 1f;
        }

        if (current.ThumbSticks.Left.X > GamepadMoveDeadZone || current.DPad.Right == ButtonState.Pressed)
        {
            horizontal += 1f;
        }

        horizontal = Math.Clamp(horizontal, -1f, 1f);

        bool fastFall = current.ThumbSticks.Left.Y < -GamepadDefaults.FastFallStickThreshold
            || current.DPad.Down == ButtonState.Pressed;
        bool pullRope = current.Triggers.Right > GamepadDefaults.PullRopeTriggerThreshold;

        GameColor? requestedColor = null;
        if (IsGamepadPressed(current, previous, _gamepadBindings.Red))
        {
            requestedColor = GameColor.Red;
        }
        else if (IsGamepadPressed(current, previous, _gamepadBindings.Green))
        {
            requestedColor = GameColor.Green;
        }
        else if (IsGamepadPressed(current, previous, _gamepadBindings.Blue))
        {
            requestedColor = GameColor.Blue;
        }

        return new PlayerInputState(
            horizontal,
            IsGamepadPressed(current, previous, _gamepadBindings.Jump),
            IsGamepadPressed(current, previous, _gamepadBindings.Respawn),
            fastFall,
            pullRope,
            requestedColor);
    }

    private static bool IsGamepadPressed(GamePadState current, GamePadState previous, Buttons button)
    {
        return current.IsButtonDown(button) && previous.IsButtonUp(button);
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
        Keys Respawn,
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
                GetSettingKey(settings, "Respawn", Keys.R),
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

    private readonly record struct GamepadButtonBindings(
        Buttons Jump,
        Buttons Respawn,
        Buttons Red,
        Buttons Blue,
        Buttons Green)
    {
        public static GamepadButtonBindings FromSettings(GameSettings settings)
        {
            return new GamepadButtonBindings(
                Resolve(settings, GameplayInputAction.Jump),
                Resolve(settings, GameplayInputAction.Respawn),
                Resolve(settings, GameplayInputAction.Red),
                Resolve(settings, GameplayInputAction.Blue),
                Resolve(settings, GameplayInputAction.Green));
        }

        private static Buttons Resolve(GameSettings settings, GameplayInputAction action)
        {
            if (settings.GamepadBindings.TryGetValue(action.ToString(), out string? stored)
                && Enum.TryParse(stored, out Buttons button))
            {
                return button;
            }

            return GamepadDefaults.GetDefaultButton(action);
        }
    }
}
