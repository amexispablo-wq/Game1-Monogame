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
    public bool ReplayForceSavePressed { get; private set; }
    public bool ReplayBackgroundTogglePressed { get; private set; }
    public bool ReplayViewerExitPressed { get; private set; }
    public bool ReplayViewerPausePressed { get; private set; }
    public bool ReplayViewerRestartPressed { get; private set; }
    public bool ReplayViewerSpeedUpPressed { get; private set; }
    public bool ReplayViewerSpeedDownPressed { get; private set; }
    public bool ReplayViewerSpeedUpHeld { get; private set; }
    public bool ReplayViewerSpeedDownHeld { get; private set; }
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
    public bool GamepadBackPressed { get; private set; }
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

    public PartyInputSource LastUsedPartyInputSource { get; private set; } = PartyInputSource.Keyboard;
    public int LastUsedPartyControllerId { get; private set; } = -1;

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
        UpdateLastUsedPartyInput();
        Navigation.Update(this);
        RequestedColor = GetEditorColorRequest();
        _virtualLeftClickRequested = false;
        UpdateReplayViewerInput();
    }

    private void UpdateReplayViewerInput()
    {
        ReplayViewerExitPressed = IsNewKeyPress(Keys.Escape) || GamepadBackPressed;
        ReplayViewerPausePressed = IsNewKeyPress(Keys.Enter) || KeyboardMenuConfirmPressed || GamepadMenuConfirmPressed;
        ReplayViewerRestartPressed = IsNewKeyPress(Keys.R);
        ReplayViewerSpeedUpHeld = EditorRightTrigger > 0.35f;
        ReplayViewerSpeedDownHeld = EditorLeftTrigger > 0.35f;
        ReplayViewerSpeedUpPressed = ReplayViewerSpeedUpHeld;
        ReplayViewerSpeedDownPressed = ReplayViewerSpeedDownHeld;
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
                return GamepadDefaults.ProcessLeftStick(_currentGamepads[i].ThumbSticks.Left);
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
                return GamepadDefaults.ProcessRightStick(_currentGamepads[i].ThumbSticks.Right);
            }
        }

        return Vector2.Zero;
    }

    public Vector2 GetEditorLeftStick()
    {
        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            if (_currentGamepads[i].IsConnected)
            {
                return GamepadDefaults.ProcessEditorStick(_currentGamepads[i].ThumbSticks.Left);
            }
        }

        return Vector2.Zero;
    }

    public Vector2 GetEditorRightStick()
    {
        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            if (_currentGamepads[i].IsConnected)
            {
                return GamepadDefaults.ProcessEditorStick(_currentGamepads[i].ThumbSticks.Right);
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

    public bool TryCaptureGamepadBinding(int deviceIndex, GameplayInputAction action, out string bindingToken)
    {
        bindingToken = string.Empty;
        if (deviceIndex < 0 || deviceIndex >= MaxLocalPlayers || !_currentGamepads[deviceIndex].IsConnected)
        {
            return false;
        }

        GamePadState current = _currentGamepads[deviceIndex];
        GamePadState previous = _previousGamepads[deviceIndex];

        if (GamepadActionBinding.TryCaptureAnyEdge(current, previous, out bindingToken))
        {
            return true;
        }

        if (IsGamepadPressed(current, previous, Buttons.Start))
        {
            bindingToken = GamepadBindingTokens.Default;
            return true;
        }

        foreach (Buttons button in GamepadDefaults.CaptureButtons)
        {
            if (button == Buttons.Start)
            {
                continue;
            }

            if (!IsGamepadPressed(current, previous, button))
            {
                continue;
            }

            bindingToken = button.ToString();
            return true;
        }

        return false;
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
        ReplayForceSavePressed = IsNewKeyPress(Keys.F10);
        ReplayBackgroundTogglePressed = IsNewKeyPress(Keys.F11);

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
        GamepadBackPressed = false;

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
            Vector2 processedLeftStick = GamepadDefaults.ProcessLeftStick(current.ThumbSticks.Left);

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

            if (IsGamepadPressed(current, previous, Buttons.Back))
            {
                GamepadBackPressed = true;
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

            if (processedLeftStick.X < -GamepadDefaults.MenuStickDirectionThreshold)
            {
                stickLeft = true;
                GamepadActivityThisFrame = true;
            }

            if (processedLeftStick.X > GamepadDefaults.MenuStickDirectionThreshold)
            {
                stickRight = true;
                GamepadActivityThisFrame = true;
            }

            if (processedLeftStick.Y > GamepadDefaults.MenuStickDirectionThreshold)
            {
                stickUp = true;
                GamepadActivityThisFrame = true;
            }

            if (processedLeftStick.Y < -GamepadDefaults.MenuStickDirectionThreshold)
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

        Vector2 processedStick = GamepadDefaults.ProcessLeftStick(current.ThumbSticks.Left);
        float horizontal = GamepadDefaults.ReadHorizontalMovement(
            processedStick,
            _gamepadBindings.MoveLeft,
            _gamepadBindings.MoveRight,
            current);
        bool fastFall = GamepadDefaults.ReadFastFallHeld(
            processedStick,
            _gamepadBindings.FastFall,
            current);
        bool pullRope = current.Triggers.Right > GamepadDefaults.PullRopeTriggerThreshold;

        GameColor? requestedColor = null;
        if (WasBindingPressed(current, previous, _gamepadBindings.Red, GameplayInputAction.Red))
        {
            requestedColor = GameColor.Red;
        }
        else if (WasBindingPressed(current, previous, _gamepadBindings.Green, GameplayInputAction.Green))
        {
            requestedColor = GameColor.Green;
        }
        else if (WasBindingPressed(current, previous, _gamepadBindings.Blue, GameplayInputAction.Blue))
        {
            requestedColor = GameColor.Blue;
        }

        return new PlayerInputState(
            horizontal,
            WasBindingPressed(current, previous, _gamepadBindings.Jump, GameplayInputAction.Jump),
            WasBindingPressed(current, previous, _gamepadBindings.Respawn, GameplayInputAction.Respawn),
            fastFall,
            pullRope,
            requestedColor);
    }

    private static bool WasBindingPressed(
        GamePadState current,
        GamePadState previous,
        GamepadActionBinding binding,
        GameplayInputAction action)
    {
        Vector2 processedCurrent = GamepadDefaults.ProcessLeftStick(current.ThumbSticks.Left);
        Vector2 processedPrevious = GamepadDefaults.ProcessLeftStick(previous.ThumbSticks.Left);
        return binding.IsActive(current, action, processedCurrent)
            && !binding.IsActive(previous, action, processedPrevious);
    }

    private static bool IsGamepadPressed(GamePadState current, GamePadState previous, Buttons button)
    {
        return current.IsButtonDown(button) && previous.IsButtonUp(button);
    }

    private void UpdateLastUsedPartyInput()
    {
        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            if (!_currentGamepads[i].IsConnected)
            {
                continue;
            }

            if (HasGamepadPartyActivity(_currentGamepads[i], _previousGamepads[i]))
            {
                LastUsedPartyInputSource = PartyInputSource.Gamepad;
                LastUsedPartyControllerId = i;
                return;
            }
        }

        if (KeyboardMenuActivityThisFrame || HasKeyboardGameplayActivity())
        {
            LastUsedPartyInputSource = PartyInputSource.Keyboard;
            LastUsedPartyControllerId = -1;
        }
    }

    private bool HasKeyboardGameplayActivity()
    {
        return _currentKeyboard.IsKeyDown(_keyboardBindings.MoveLeft)
            || _currentKeyboard.IsKeyDown(_keyboardBindings.MoveRight)
            || _currentKeyboard.IsKeyDown(_keyboardBindings.Jump)
            || _currentKeyboard.IsKeyDown(_keyboardBindings.FastFall)
            || _currentKeyboard.IsKeyDown(_keyboardBindings.PullRope)
            || IsNewKeyPress(_keyboardBindings.Red)
            || IsNewKeyPress(_keyboardBindings.Blue)
            || IsNewKeyPress(_keyboardBindings.Green)
            || IsNewKeyPress(_keyboardBindings.Respawn);
    }

    private static bool HasGamepadPartyActivity(GamePadState current, GamePadState previous)
    {
        if (GamepadDefaults.ProcessLeftStick(current.ThumbSticks.Left).LengthSquared() > 0.0001f
            || current.ThumbSticks.Right.LengthSquared() > GamepadMoveDeadZone * GamepadMoveDeadZone
            || current.Triggers.Left > GamepadDefaults.PullRopeTriggerThreshold
            || current.Triggers.Right > GamepadDefaults.PullRopeTriggerThreshold)
        {
            return true;
        }

        return current.Buttons != previous.Buttons
            || current.DPad != previous.DPad;
    }

    private GameColor? GetEditorColorRequest()
    {
        if (IsNewKeyPress(_keyboardBindings.Red))
        {
            return GameColor.Red;
        }

        if (IsNewKeyPress(_keyboardBindings.Blue))
        {
            return GameColor.Blue;
        }

        if (IsNewKeyPress(_keyboardBindings.Green))
        {
            return GameColor.Green;
        }

        for (int i = 0; i < MaxLocalPlayers; i++)
        {
            GamePadState current = _currentGamepads[i];
            GamePadState previous = _previousGamepads[i];
            if (!current.IsConnected)
            {
                continue;
            }

            if (WasBindingPressed(current, previous, _gamepadBindings.Red, GameplayInputAction.Red))
            {
                return GameColor.Red;
            }

            if (WasBindingPressed(current, previous, _gamepadBindings.Blue, GameplayInputAction.Blue))
            {
                return GameColor.Blue;
            }

            if (WasBindingPressed(current, previous, _gamepadBindings.Green, GameplayInputAction.Green))
            {
                return GameColor.Green;
            }
        }

        return null;
    }

    public bool TryGetEditorColorRequest(out GameColor color)
    {
        if (GetEditorColorRequest() is GameColor requested)
        {
            color = requested;
            return true;
        }

        color = default;
        return false;
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
        GamepadActionBinding MoveLeft,
        GamepadActionBinding MoveRight,
        GamepadActionBinding Jump,
        GamepadActionBinding Respawn,
        GamepadActionBinding FastFall,
        GamepadActionBinding Red,
        GamepadActionBinding Blue,
        GamepadActionBinding Green)
    {
        public static GamepadButtonBindings FromSettings(GameSettings settings)
        {
            return new GamepadButtonBindings(
                Resolve(settings, GameplayInputAction.MoveLeft),
                Resolve(settings, GameplayInputAction.MoveRight),
                Resolve(settings, GameplayInputAction.Jump),
                Resolve(settings, GameplayInputAction.Respawn),
                Resolve(settings, GameplayInputAction.FastFall),
                Resolve(settings, GameplayInputAction.Red),
                Resolve(settings, GameplayInputAction.Blue),
                Resolve(settings, GameplayInputAction.Green));
        }

        private static GamepadActionBinding Resolve(GameSettings settings, GameplayInputAction action)
        {
            settings.GamepadBindings.TryGetValue(action.ToString(), out string? stored);
            return GamepadActionBinding.Parse(stored, action);
        }
    }
}
