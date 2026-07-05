using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class VirtualCursor
{
    private Vector2 _position;
    private Vector2 _velocity;
    private bool _initialized;

    public bool IsActive { get; private set; }
    public Point Position => new((int)MathF.Round(_position.X), (int)MathF.Round(_position.Y));

    public void BeginFrame(Viewport viewport, InputManager input)
    {
        IsActive = input.IsAnyGamepadConnected()
            && input.Navigation.IsGamepadActive
            && !input.Navigation.IsMouseActive;
        if (!IsActive)
        {
            _velocity = Vector2.Zero;
            return;
        }

        if (!_initialized)
        {
            _position = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
            _initialized = true;
        }
    }

    public void Update(GameTime gameTime, InputManager input, Viewport viewport)
    {
        if (!IsActive)
        {
            return;
        }

        float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 0.05f);
        Vector2 stick = input.GetEditorRightStick();
        stick.Y = -stick.Y;

        if (stick.LengthSquared() <= 0.0001f)
        {
            _velocity = Vector2.Lerp(_velocity, Vector2.Zero, 1f - MathF.Exp(-10f * dt));
        }
        else
        {
            _velocity = stick * GamepadDefaults.EditorCursorSpeedPixelsPerSecond;
        }

        _position += _velocity * dt;
        _position.X = Math.Clamp(_position.X, 0f, viewport.Width - 1f);
        _position.Y = Math.Clamp(_position.Y, 0f, viewport.Height - 1f);
    }

    public Vector2 PositionF => _position;

    public void Reset()
    {
        _initialized = false;
        _velocity = Vector2.Zero;
        IsActive = false;
    }
}
