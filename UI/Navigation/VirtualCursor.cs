using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

public sealed class VirtualCursor
{
    private const float BaseSpeedPixelsPerSecond = 920f;
    private const float Acceleration = 2.4f;
    private const float DeadZone = 0.18f;

    private Point _position;
    private Vector2 _velocity;
    private bool _initialized;

    public bool IsActive { get; private set; }
    public Point Position => _position;

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
            _position = new Point(viewport.Width / 2, viewport.Height / 2);
            _initialized = true;
        }
    }

    public void Update(GameTime gameTime, InputManager input, Viewport viewport)
    {
        if (!IsActive)
        {
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Vector2 stick = input.GetMenuRightStick();
        stick.Y = -stick.Y;
        float magnitude = stick.Length();
        if (magnitude < DeadZone)
        {
            _velocity = Vector2.Lerp(_velocity, Vector2.Zero, 1f - MathF.Exp(-12f * dt));
        }
        else
        {
            Vector2 direction = stick / magnitude;
            float speed = BaseSpeedPixelsPerSecond * MathF.Pow(magnitude, Acceleration);
            _velocity = direction * speed;
        }

        Vector2 next = new Vector2(_position.X, _position.Y) + (_velocity * dt);
        int x = (int)MathF.Round(Math.Clamp(next.X, 0f, viewport.Width - 1));
        int y = (int)MathF.Round(Math.Clamp(next.Y, 0f, viewport.Height - 1));
        _position = new Point(x, y);
    }

    public void Reset()
    {
        _initialized = false;
        _velocity = Vector2.Zero;
        IsActive = false;
    }
}
