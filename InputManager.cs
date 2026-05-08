using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game1_Monogame;

public sealed class InputManager
{
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;
    private MouseState _currentMouse;
    private MouseState _previousMouse;

    public float HorizontalMovement { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool FastFallHeld { get; private set; }
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

        HorizontalMovement = 0f;
        if (_currentKeyboard.IsKeyDown(Keys.A))
        {
            HorizontalMovement -= 1f;
        }

        if (_currentKeyboard.IsKeyDown(Keys.D))
        {
            HorizontalMovement += 1f;
        }

        JumpPressed = IsNewKeyPress(Keys.Space) || IsNewKeyPress(Keys.W);
        FastFallHeld = _currentKeyboard.IsKeyDown(Keys.S);
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
        RequestedColor = GetRequestedColor();
    }

    public bool IsKeyDown(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key);
    }

    public bool IsNewKeyPress(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private GameColor? GetRequestedColor()
    {
        if (IsNewKeyPress(Keys.J) || IsNewKeyPress(Keys.R))
        {
            return GameColor.Red;
        }

        if (IsNewKeyPress(Keys.K) || IsNewKeyPress(Keys.B))
        {
            return GameColor.Blue;
        }

        if (IsNewKeyPress(Keys.L) || IsNewKeyPress(Keys.G))
        {
            return GameColor.Green;
        }

        return null;
    }
}
