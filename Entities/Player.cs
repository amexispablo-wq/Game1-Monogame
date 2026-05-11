using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class Player
{
    private const float MinMass = 0.01f;
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
    private Vector2 _forceAccumulator;
    private float _mass = 1f;

    public Player(PlayerId playerId, int playerIndex, Vector2 startPosition, InputDevice assignedInput)
    {
        PlayerId = playerId;
        PlayerIndex = playerIndex;
        Position = startPosition;
        AssignedInput = assignedInput;
        CurrentColor = GameColor.Red;
    }

    public PlayerId PlayerId { get; }
    public int PlayerIndex { get; }
    public InputDevice AssignedInput { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 Acceleration { get; private set; }
    public Vector2 Size { get; } = new(40f, 40f);
    public GameColor CurrentColor { get; private set; }
    public GameColor PlayerColor => CurrentColor;
    public bool IsGrounded { get; internal set; }
    public bool IsOnGround => IsGrounded;
    public bool IsFrozen { get; private set; }
    public Vector2 LastCollisionNormal { get; internal set; }
    public Vector2 LastCollisionCorrection { get; internal set; }
    public float GravityScale { get; set; } = 1f;
    public float FastFallGravityMultiplier { get; set; } = 1.5f;
    public float GroundAcceleration { get; set; } = 2600f;
    public float AirAcceleration { get; set; } = 1050f;
    public float GroundFriction { get; set; } = 16f;
    public float AirDrag { get; set; } = 0.35f;
    public float MaxMoveSpeed { get; set; } = 260f;
    public float MaxHorizontalVelocity { get; set; } = 760f;
    public float MaxVerticalVelocity { get; set; } = 1150f;
    public float JumpImpulse { get; set; } = 560f;
    public float MaxLaunchImpulse { get; set; } = 4000f;
    public float LaunchControlSeconds { get; set; } = 0.24f;
    public float LaunchControlAcceleration { get; set; } = 650f;
    public float Mass
    {
        get => _mass;
        set => _mass = MathF.Max(MinMass, value);
    }

    public Rectangle Bounds => CollisionHelper.ToRectangle(Position, Size);

    public void AddForce(Vector2 force)
    {
        if (!IsFrozen)
        {
            _forceAccumulator += force;
        }
    }

    public void AddImpulse(Vector2 impulse)
    {
        if (!IsFrozen)
        {
            Velocity += impulse / Mass;
        }
    }

    public void Freeze()
    {
        IsFrozen = true;
        Velocity = Vector2.Zero;
        Acceleration = Vector2.Zero;
        _forceAccumulator = Vector2.Zero;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw, bool drawIndicator = true)
    {
        Rectangle bounds = Bounds;
        spriteBatch.Draw(pixel, bounds, CurrentColor.ToXnaColor());
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 3);

        if (drawIndicator)
        {
            DrawPlayerIndicator(spriteBatch, pixel, bounds);
        }

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, bounds, IsGrounded ? Color.LimeGreen : Color.White, 1);
            DrawDebugPhysics(spriteBatch, pixel);
            DrawDebugEscapeVector(spriteBatch, pixel);
        }
    }

    internal void BeginPhysicsStep(float dt, Level level)
    {
        LastCollisionNormal = Vector2.Zero;
        LastCollisionCorrection = Vector2.Zero;
        UpdateResolvedCollisionState(dt, level);
        UpdateLaunchState(dt);
        UpdateDebugEscapeVector(dt);
    }

    internal void HandleInputState(InputActionState input, Level level)
    {
        HandleColorChange(input, level);
    }

    internal void RefreshGroundedState(Level level)
    {
        IsGrounded = CollisionHelper.HasGroundBelow(Position, Size, level, CurrentColor);
    }

    internal void ApplyMovementForces(InputActionState input)
    {
        float horizontalInput = MathHelper.Clamp(input.HorizontalMovement, -1f, 1f);

        if (_justLaunched)
        {
            AddForce(new Vector2(LaunchControlAcceleration * horizontalInput * Mass, 0f));
            ApplyHorizontalDrag(IsGrounded ? GroundFriction * 0.15f : AirDrag);
            return;
        }

        if (MathF.Abs(horizontalInput) > 0.01f)
        {
            float acceleration = IsGrounded ? GroundAcceleration : AirAcceleration;
            bool alreadyPastMoveSpeed = MathF.Abs(Velocity.X) >= MaxMoveSpeed
                && MathF.Sign(Velocity.X) == MathF.Sign(horizontalInput);

            if (!alreadyPastMoveSpeed)
            {
                AddForce(new Vector2(horizontalInput * acceleration * Mass, 0f));
            }
        }

        if (MathF.Abs(horizontalInput) <= 0.01f || !IsGrounded)
        {
            ApplyHorizontalDrag(IsGrounded ? GroundFriction : AirDrag);
        }
    }

    internal void ApplyJumpImpulse(InputActionState input)
    {
        if (!input.JumpPressed || !IsGrounded)
        {
            return;
        }

        AddImpulse(new Vector2(0f, -JumpImpulse * Mass));
        IsGrounded = false;
    }

    internal void ApplyGravity(float gravity, InputActionState input)
    {
        float gravityMultiplier = input.FastFallHeld && Velocity.Y > 0f
            ? GravityScale * FastFallGravityMultiplier
            : GravityScale;

        AddForce(new Vector2(0f, gravity * gravityMultiplier * Mass));
    }

    internal void IntegrateForces(float dt)
    {
        Acceleration = _forceAccumulator / Mass;
        Velocity += Acceleration * dt;
        _forceAccumulator = Vector2.Zero;
    }

    internal void IntegratePosition(Vector2 delta)
    {
        Position += delta;
    }

    internal void ApplyCollisionCorrection(Vector2 correction, Vector2 normal)
    {
        Position += correction;
        LastCollisionCorrection = correction;
        LastCollisionNormal = normal;
    }

    internal void ClampVelocity()
    {
        Velocity = Vector2.Clamp(
            Velocity,
            new Vector2(-MaxHorizontalVelocity, -MaxVerticalVelocity),
            new Vector2(MaxHorizontalVelocity, MaxVerticalVelocity));
    }

    internal void FinishPhysicsStep()
    {
        UpdateGroundedLaunchEnd();
    }

    private void ApplyHorizontalDrag(float drag)
    {
        if (MathF.Abs(Velocity.X) <= 0.01f)
        {
            return;
        }

        AddForce(new Vector2(-Velocity.X * drag * Mass, 0f));
    }

    private void HandleColorChange(InputActionState input, Level level)
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
        IsGrounded = false;
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

            ApplyCollisionCorrection(bestEscape, bestEscapeDirection);
            resolvedOverlap = true;

            float normalizedDepth = bestPenetrationDepth / bestPlatformSize;
            float candidateLaunchSpeed = MathF.Min(normalizedDepth * MaxLaunchImpulse, MaxLaunchImpulse);
            if (candidateLaunchSpeed > launchSpeed)
            {
                launchSpeed = candidateLaunchSpeed;
                launchDirection = bestEscapeDirection;
            }

            IsGrounded = bestEscape.Y < 0f;
        }

        if (!resolvedOverlap || _justResolvedCollision || launchDirection == Vector2.Zero)
        {
            return;
        }

        AddImpulse(launchDirection * launchSpeed * Mass);
        ClampVelocity();
        _justLaunched = true;
        _launchControlRemaining = LaunchControlSeconds;
        _justResolvedCollision = true;
        _resolveLaunchCooldownRemaining = ResolveLaunchCooldownSeconds;
        _debugEscapeVectorTimeRemaining = DebugEscapeVectorSeconds;
        _debugEscapeVectorStart = Position + (Size * 0.5f);
        _debugEscapeVector = launchDirection * MathHelper.Clamp(launchSpeed * 0.16f, 20f, 96f);
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
        if (_justLaunched && IsGrounded && _launchControlRemaining < LaunchControlSeconds - 0.08f)
        {
            _justLaunched = false;
        }
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

    private void DrawDebugPhysics(SpriteBatch spriteBatch, Texture2D pixel)
    {
        Vector2 center = Position + (Size * 0.5f);
        DrawDebugVector(spriteBatch, pixel, center, Velocity * 0.12f, Color.Cyan);
        DrawDebugVector(spriteBatch, pixel, center, Acceleration * 0.015f, new Color(255, 168, 64));

        if (LastCollisionNormal != Vector2.Zero)
        {
            DrawDebugVector(spriteBatch, pixel, center, LastCollisionNormal * 28f, Color.LimeGreen);
        }
    }

    private void DrawDebugVector(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 vector, Color color)
    {
        if (vector == Vector2.Zero)
        {
            return;
        }

        Vector2 corner = start + new Vector2(vector.X, 0f);
        DrawAxisSegment(spriteBatch, pixel, start, corner, color, 3);
        DrawAxisSegment(spriteBatch, pixel, corner, start + vector, color, 3);
        Vector2 end = start + vector;
        spriteBatch.Draw(pixel, new Rectangle((int)MathF.Round(end.X) - 3, (int)MathF.Round(end.Y) - 3, 6, 6), color);
    }

    private void DrawDebugEscapeVector(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (_debugEscapeVectorTimeRemaining <= 0f || _debugEscapeVector == Vector2.Zero)
        {
            return;
        }

        Vector2 end = _debugEscapeVectorStart + _debugEscapeVector;
        DrawAxisSegment(spriteBatch, pixel, _debugEscapeVectorStart, end, Color.Yellow, 4);
        spriteBatch.Draw(pixel, new Rectangle((int)MathF.Round(end.X) - 5, (int)MathF.Round(end.Y) - 5, 10, 10), Color.Yellow);
    }

    private static void DrawAxisSegment(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
    {
        if (MathF.Abs(end.X - start.X) >= MathF.Abs(end.Y - start.Y))
        {
            int left = (int)MathF.Round(MathF.Min(start.X, end.X));
            int width = (int)MathF.Round(MathF.Abs(end.X - start.X)) + thickness;
            int y = (int)MathF.Round(start.Y - (thickness * 0.5f));
            spriteBatch.Draw(pixel, new Rectangle(left, y, width, thickness), color);
        }
        else
        {
            int x = (int)MathF.Round(start.X - (thickness * 0.5f));
            int top = (int)MathF.Round(MathF.Min(start.Y, end.Y));
            int height = (int)MathF.Round(MathF.Abs(end.Y - start.Y)) + thickness;
            spriteBatch.Draw(pixel, new Rectangle(x, top, thickness, height), color);
        }
    }

    private void DrawPlayerIndicator(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds)
    {
        string label = (PlayerIndex + 1).ToString();
        const int scale = 2;
        Point textSize = SimpleTextRenderer.MeasureString(label, scale);
        Vector2 position = new(
            bounds.Center.X - (textSize.X * 0.5f),
            bounds.Top - textSize.Y - 6);

        SimpleTextRenderer.DrawString(spriteBatch, pixel, label, position + new Vector2(1f, 1f), scale, Color.Black);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, label, position, scale, Color.White);
    }
}
