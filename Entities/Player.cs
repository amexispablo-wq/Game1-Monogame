using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class Player
{
    private const float MoveSpeed = 230f;
    private const float GroundAcceleration = 20f;
    private const float AirAcceleration = 9f;
    private const float Gravity = 1600f;
    private const float FastFallGravity = 2400f;
    private const float JumpForce = 560f;
    private const float MaxLaunchSpeed = 4000f;
    private const float LaunchControlSeconds = 0.24f;
    private const float LaunchInputAcceleration = 650f;
    private const float MaxHorizontalVelocity = 760f;
    private const float MaxVerticalVelocity = 1100f;
    private const float ResolveLaunchCooldownSeconds = 0.12f;
    private const float DebugEscapeVectorSeconds = 0.5f;
    private const int MaxOverlapResolveIterations = 10;

    private bool _justLaunched;
    private float _launchControlRemaining;
    private bool _justResolvedCollision;
    private float _resolveLaunchCooldownRemaining;
    private float _debugEscapeVectorTimeRemaining;
    private Vector2 _debugEscapeVectorStart;
    private Vector2 _debugEscapeVector;

    public Player(Vector2 startPosition)
    {
        Position = startPosition;
        CurrentColor = GameColor.Red;
    }

    public Vector2 Position { get; private set; }
    public Vector2 Velocity { get; private set; }
    public Vector2 Size { get; } = new(40f, 40f);
    public GameColor CurrentColor { get; private set; }
    public bool IsOnGround { get; private set; }
    public Rectangle Bounds => CollisionHelper.ToRectangle(Position, Size);

    public void Update(GameTime gameTime, InputManager input, Level level)
    {
        float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f);

        UpdateResolvedCollisionState(dt, level);
        UpdateLaunchState(dt);
        UpdateDebugEscapeVector(dt);
        HandleColorChange(input, level);

        IsOnGround = CollisionHelper.HasGroundBelow(Position, Size, level, CurrentColor);

        ApplyHorizontalMovement(input, dt);

        if (input.JumpPressed && IsOnGround)
        {
            Velocity = new Vector2(Velocity.X, -JumpForce);
            IsOnGround = false;
        }

        float gravity = input.FastFallHeld && Velocity.Y > 0f ? FastFallGravity : Gravity;
        Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * dt);
        ClampVelocity();

        MoveAndCollideHorizontally(dt, level);
        MoveAndCollideVertically(dt, level);
        UpdateGroundedLaunchEnd();
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        Rectangle bounds = Bounds;
        spriteBatch.Draw(pixel, bounds, CurrentColor.ToXnaColor());
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 3);

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.White, 1);
            DrawDebugEscapeVector(spriteBatch, pixel);
        }
    }

    private void HandleColorChange(InputManager input, Level level)
    {
        if (input.RequestedColor is not { } requestedColor || requestedColor == CurrentColor)
        {
            return;
        }

        CurrentColor = requestedColor;
        ResolveEmbeddedOverlaps(level);
    }

    private void ResolveEmbeddedOverlaps(Level level)
    {
        IsOnGround = false;
        Vector2 launchDirection = Vector2.Zero;
        float launchSpeed = 0f;
        bool resolvedOverlap = false;

        for (int iteration = 0; iteration < MaxOverlapResolveIterations; iteration++)
        {
            Vector2 bestEscape = Vector2.Zero;
            Vector2 bestEscapeDirection = Vector2.Zero;
            float bestPenetrationDepth = 0f;
            float bestPlatformSize = 1f;
            float bestDistanceSquared = float.MaxValue;

            foreach (Platform platform in level.GetCollidablePlatforms(CurrentColor))
            {
                if (!CollisionHelper.TryGetMinimumTranslationVector(
                    Position,
                    Size,
                    platform.Bounds,
                    out Vector2 escape,
                    out Vector2 escapeDirection,
                    out float penetrationDepth))
                {
                    continue;
                }

                float distanceSquared = escape.LengthSquared();
                if (distanceSquared < bestDistanceSquared)
                {
                    bestEscape = escape;
                    bestEscapeDirection = escapeDirection;
                    bestPenetrationDepth = penetrationDepth;
                    bestPlatformSize = MathF.Max(platform.Bounds.Width, platform.Bounds.Height);
                    bestDistanceSquared = distanceSquared;
                }
            }

            if (bestEscape == Vector2.Zero)
            {
                break;
            }

            Position += bestEscape;
            resolvedOverlap = true;

            float normalizedDepth = bestPenetrationDepth / bestPlatformSize;
            float candidateLaunchSpeed = MathF.Min(normalizedDepth * MaxLaunchSpeed, MaxLaunchSpeed);
            if (candidateLaunchSpeed > launchSpeed)
            {
                launchSpeed = candidateLaunchSpeed;
                launchDirection = bestEscapeDirection;
            }

            IsOnGround = bestEscape.Y < 0f;
        }

        if (!resolvedOverlap || _justResolvedCollision || launchDirection == Vector2.Zero)
        {
            return;
        }

        Velocity += launchDirection * launchSpeed;
        ClampVelocity();
        _justLaunched = true;
        _launchControlRemaining = LaunchControlSeconds;
        _justResolvedCollision = true;
        _resolveLaunchCooldownRemaining = ResolveLaunchCooldownSeconds;
        _debugEscapeVectorTimeRemaining = DebugEscapeVectorSeconds;
        _debugEscapeVectorStart = Position + (Size * 0.5f);
        _debugEscapeVector = launchDirection * MathHelper.Clamp(launchSpeed * 0.16f, 20f, 96f);
    }

    private void ApplyHorizontalMovement(InputManager input, float dt)
    {
        if (_justLaunched)
        {
            float inputAcceleration = LaunchInputAcceleration * input.HorizontalMovement * dt;
            Velocity = new Vector2(Velocity.X + inputAcceleration, Velocity.Y);
            return;
        }

        float acceleration = IsOnGround ? GroundAcceleration : AirAcceleration;
        float targetVelocityX = input.HorizontalMovement * MoveSpeed;
        float smoothing = 1f - MathF.Exp(-acceleration * dt);
        Velocity = new Vector2(MathHelper.Lerp(Velocity.X, targetVelocityX, smoothing), Velocity.Y);
    }

    private void UpdateLaunchState(float dt)
    {
        if (!_justLaunched)
        {
            return;
        }

        _launchControlRemaining = MathF.Max(0f, _launchControlRemaining - dt);
        if (_launchControlRemaining <= 0f)
        {
            _justLaunched = false;
        }
    }

    private void UpdateGroundedLaunchEnd()
    {
        if (_justLaunched && IsOnGround && _launchControlRemaining < LaunchControlSeconds - 0.08f)
        {
            _justLaunched = false;
        }
    }

    private void ClampVelocity()
    {
        Velocity = Vector2.Clamp(
            Velocity,
            new Vector2(-MaxHorizontalVelocity, -MaxVerticalVelocity),
            new Vector2(MaxHorizontalVelocity, MaxVerticalVelocity));
    }

    private void UpdateResolvedCollisionState(float dt, Level level)
    {
        if (!_justResolvedCollision)
        {
            return;
        }

        _resolveLaunchCooldownRemaining = MathF.Max(0f, _resolveLaunchCooldownRemaining - dt);
        if (_resolveLaunchCooldownRemaining <= 0f || !IsOverlappingCurrentColorPlatform(level))
        {
            _justResolvedCollision = false;
        }
    }

    private bool IsOverlappingCurrentColorPlatform(Level level)
    {
        foreach (Platform platform in level.GetCollidablePlatforms(CurrentColor))
        {
            if (CollisionHelper.Intersects(Position, Size, platform.Bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateDebugEscapeVector(float dt)
    {
        if (_debugEscapeVectorTimeRemaining > 0f)
        {
            _debugEscapeVectorTimeRemaining = MathF.Max(0f, _debugEscapeVectorTimeRemaining - dt);
        }
    }

    private void DrawDebugEscapeVector(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (_debugEscapeVectorTimeRemaining <= 0f || _debugEscapeVector == Vector2.Zero)
        {
            return;
        }

        Vector2 end = _debugEscapeVectorStart + _debugEscapeVector;
        const int thickness = 4;

        if (MathF.Abs(_debugEscapeVector.X) >= MathF.Abs(_debugEscapeVector.Y))
        {
            int left = (int)MathF.Round(MathF.Min(_debugEscapeVectorStart.X, end.X));
            int width = (int)MathF.Round(MathF.Abs(_debugEscapeVector.X)) + thickness;
            int y = (int)MathF.Round(_debugEscapeVectorStart.Y - (thickness * 0.5f));
            spriteBatch.Draw(pixel, new Rectangle(left, y, width, thickness), Color.Yellow);
        }
        else
        {
            int x = (int)MathF.Round(_debugEscapeVectorStart.X - (thickness * 0.5f));
            int top = (int)MathF.Round(MathF.Min(_debugEscapeVectorStart.Y, end.Y));
            int height = (int)MathF.Round(MathF.Abs(_debugEscapeVector.Y)) + thickness;
            spriteBatch.Draw(pixel, new Rectangle(x, top, thickness, height), Color.Yellow);
        }

        spriteBatch.Draw(pixel, new Rectangle((int)MathF.Round(end.X) - 5, (int)MathF.Round(end.Y) - 5, 10, 10), Color.Yellow);
    }

    private void MoveAndCollideHorizontally(float dt, Level level)
    {
        Position += new Vector2(Velocity.X * dt, 0f);

        foreach (Platform platform in level.GetCollidablePlatforms(CurrentColor))
        {
            if (!CollisionHelper.Intersects(Position, Size, platform.Bounds))
            {
                continue;
            }

            if (Velocity.X > 0f)
            {
                Position = new Vector2(platform.Bounds.Left - Size.X, Position.Y);
            }
            else if (Velocity.X < 0f)
            {
                Position = new Vector2(platform.Bounds.Right, Position.Y);
            }

            Velocity = new Vector2(0f, Velocity.Y);
        }
    }

    private void MoveAndCollideVertically(float dt, Level level)
    {
        Position += new Vector2(0f, Velocity.Y * dt);
        IsOnGround = false;

        foreach (Platform platform in level.GetCollidablePlatforms(CurrentColor))
        {
            if (!CollisionHelper.Intersects(Position, Size, platform.Bounds))
            {
                continue;
            }

            if (Velocity.Y > 0f)
            {
                Position = new Vector2(Position.X, platform.Bounds.Top - Size.Y);
                IsOnGround = true;
            }
            else if (Velocity.Y < 0f)
            {
                Position = new Vector2(Position.X, platform.Bounds.Bottom);
            }

            Velocity = new Vector2(Velocity.X, 0f);
        }
    }
}
