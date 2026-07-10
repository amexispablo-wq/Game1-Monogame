using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class Player : INetworkEntity
{
    private const float MinMass = 0.01f;
    private const float DebugEscapeVectorSeconds = 0.5f;
    private const float MinimumEjectionFallbackLengthSquared = 0.0001f;

    private bool _justLaunched;
    private float _launchControlRemaining;
    private float _debugEscapeVectorTimeRemaining;
    private Vector2 _debugEscapeVectorStart;
    private Vector2 _debugEscapeVector;
    private Vector2 _forceAccumulator;
    private Platform _ejectionPlatform;
    private Vector2 _ejectionBaseDirection;
    private Vector2 _ejectionForceDirection;
    private Vector2 _ejectionPlatformCenter;
    private float _ejectionTimer;
    private float _ejectionRampAmount;
    private float _ejectionForce;
    private float _ejectionPenetrationDepth;
    private float _ejectionCenterInfluence;
    private bool _ejectionPeakRaised;
    private float _mass = 1f;

    public Player(
        PlayerId playerId,
        int playerIndex,
        PartyMemberId partyMemberId,
        Vector2 startPosition,
        InputDevice assignedInput,
        NetworkEntityOwnership ownership,
        string displayLabel)
    {
        PlayerId = playerId;
        PlayerIndex = playerIndex;
        PartyMemberId = partyMemberId;
        Position = startPosition;
        AssignedInput = assignedInput;
        DisplayLabel = displayLabel;
        CurrentColor = GameColor.Red;
        ConfigureNetworkOwnership(ownership);
    }

    public int NetworkId { get; private set; }
    public int OwnerId { get; private set; }
    public bool IsLocal { get; private set; }
    public bool IsRemote => !IsLocal;
    public bool IsHostControlled { get; private set; }
    public PlayerId PlayerId { get; }
    public int PlayerIndex { get; }
    public PartyMemberId PartyMemberId { get; }
    public string DisplayLabel { get; }
    public InputDevice AssignedInput { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public Vector2 Acceleration { get; private set; }
    public Vector2 Size { get; } = new(40f, 40f);
    public GameColor CurrentColor { get; private set; }
    public GameColor PlayerColor => CurrentColor;
    private PlayerSkinData? _cosmeticSkin;
    private string _cosmeticSkinId = string.Empty;

    public void SetCosmeticSkin(PlayerSkinData? skin, string? skinId = null)
    {
        _cosmeticSkin = skin?.Clone();
        _cosmeticSkinId = skinId ?? string.Empty;
    }

    internal PlayerSkinData? GetCosmeticSkinForDraw() => _cosmeticSkin;
    public bool IsGrounded { get; internal set; }
    public bool IsOnGround => IsGrounded;
    public bool IsFrozen { get; private set; }
    public PlayerState State { get; private set; } = PlayerState.Normal;
    public bool IsEjecting => State == PlayerState.Ejecting;
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
    public float LaunchControlSeconds { get; set; } = 0.24f;
    public float LaunchControlAcceleration { get; set; } = 650f;
    public float EjectionAcceleration { get; set; } = 4400f;
    public float EjectionMaxSpeed { get; set; } = 820f;
    public float EjectionRampSpeed { get; set; } = 9f;
    public float EjectionDuration { get; set; } = 0.28f;
    public float EjectionControlFactor { get; set; } = 0.35f;
    public float EjectionCenterForceMultiplier { get; set; } = 0.75f;
    public float Mass
    {
        get => _mass;
        set => _mass = MathF.Max(MinMass, value);
    }

    public Rectangle Bounds => CollisionHelper.ToRectangle(Position, Size);

    public event Action<Player> OnEjectionStart;
    public event Action<Player> OnEjectionPeak;
    public event Action<Player> OnEjectionEnd;

    public void ConfigureNetworkOwnership(NetworkEntityOwnership ownership)
    {
        NetworkId = ownership.NetworkId;
        OwnerId = ownership.OwnerId;
        IsLocal = ownership.IsLocal;
        IsHostControlled = ownership.IsHostControlled;
    }

    public PlayerSnapshot CreateSnapshot()
    {
        return new PlayerSnapshot(
            NetworkId,
            OwnerId,
            PlayerIndex,
            PlayerId,
            NetworkVector2.FromVector2(Position),
            NetworkVector2.FromVector2(Velocity),
            NetworkVector2.FromVector2(Acceleration),
            CurrentColor,
            State,
            IsGrounded,
            IsFrozen,
            _cosmeticSkinId);
    }

    public void ApplySnapshot(PlayerSnapshot snapshot)
    {
        ConfigureNetworkOwnership(new NetworkEntityOwnership(
            snapshot.NetworkId,
            snapshot.OwnerId,
            IsLocal,
            IsHostControlled));

        Position = snapshot.Position.ToVector2();
        Velocity = snapshot.Velocity.ToVector2();
        Acceleration = snapshot.Acceleration.ToVector2();
        CurrentColor = snapshot.Color;
        State = snapshot.State;
        IsGrounded = snapshot.IsGrounded;
        IsFrozen = snapshot.IsFrozen;
        _forceAccumulator = Vector2.Zero;
        ApplyCosmeticSkinFromSnapshot(snapshot);
    }

    private void ApplyCosmeticSkinFromSnapshot(PlayerSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.CosmeticSkinId))
        {
            return;
        }

        PlayerSkinEntry? entry = SkinLibraryStorage.FindSkin(snapshot.CosmeticSkinId);
        if (entry is not null)
        {
            SetCosmeticSkin(entry.ToSkinData(), snapshot.CosmeticSkinId);
        }
    }

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

    public void RespawnAt(Vector2 position)
    {
        Position = position;
        Velocity = Vector2.Zero;
        Acceleration = Vector2.Zero;
        IsGrounded = false;
        LastCollisionNormal = Vector2.Zero;
        LastCollisionCorrection = Vector2.Zero;
        _forceAccumulator = Vector2.Zero;
        _debugEscapeVectorTimeRemaining = 0f;
        _debugEscapeVectorStart = Vector2.Zero;
        _debugEscapeVector = Vector2.Zero;
        ClearTransientMotionState();
    }

    public void LaunchFromPad(Vector2 launchVelocity)
    {
        if (IsFrozen)
        {
            return;
        }

        ClearTransientMotionState();
        Velocity = launchVelocity;
        IsGrounded = false;
        _justLaunched = true;
        _launchControlRemaining = LaunchControlSeconds;
    }

    public void Freeze()
    {
        IsFrozen = true;
        Velocity = Vector2.Zero;
        Acceleration = Vector2.Zero;
        _forceAccumulator = Vector2.Zero;
        ClearTransientMotionState();
    }

    public void Revive(Vector2 position)
    {
        IsFrozen = false;
        RespawnAt(position);
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw, bool drawIndicator = true)
    {
        Rectangle bounds = Bounds;
        DrawEjectionFeedback(spriteBatch, pixel, bounds);

        Rectangle bodyBounds = GetVisualBodyBounds(bounds);
        PlayerSkinRenderer.DrawBody(spriteBatch, pixel, bodyBounds, CurrentColor.ToXnaColor(), _cosmeticSkin);

        if (drawIndicator)
        {
            DrawPlayerIndicator(spriteBatch, pixel, bodyBounds);
        }

        if (debugDraw)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, bounds, IsGrounded ? Color.LimeGreen : Color.White, 1);
            DrawDebugPhysics(spriteBatch, pixel);
            DrawDebugEjection(spriteBatch, pixel);
            DrawDebugEscapeVector(spriteBatch, pixel);
        }
    }

    internal void BeginPhysicsStep(float dt, Level level)
    {
        LastCollisionNormal = Vector2.Zero;
        LastCollisionCorrection = Vector2.Zero;
        UpdateEjectionState(dt);
        UpdateLaunchState(dt);
        UpdateDebugEscapeVector(dt);
        TryStartEjectionFromOverlaps(level);
    }

    internal void HandleInputState(PlayerInputState input, Level level)
    {
        HandleColorChange(input, level);
    }

    internal void RefreshGroundedState(Level level)
    {
        IsGrounded = HasGroundBelow(level);
    }

    internal void ApplyMovementForces(PlayerInputState input, float dt)
    {
        float horizontalInput = NormalizeMovementInput(MathHelper.Clamp(input.HorizontalMovement, -1f, 1f));
        float controlFactor = State == PlayerState.Ejecting
            ? MathHelper.Clamp(EjectionControlFactor, 0f, 1f)
            : 1f;

        if (_justLaunched)
        {
            bool inputMatchesLaunchVelocity = MathF.Abs(horizontalInput) > 0.01f
                && MathF.Abs(Velocity.X) > MaxMoveSpeed
                && MathF.Sign(horizontalInput) == MathF.Sign(Velocity.X);

            if (!inputMatchesLaunchVelocity)
            {
                AddForce(new Vector2(LaunchControlAcceleration * horizontalInput * controlFactor * Mass, 0f));
            }
            else
            {
                BleedExcessHorizontalSpeed(dt, bleedRate: 320f);
            }

            ApplyHorizontalDrag((IsGrounded ? GroundFriction * 0.2f : AirDrag) * controlFactor);
            return;
        }

        if (MathF.Abs(horizontalInput) > 0.01f)
        {
            float acceleration = (IsGrounded ? GroundAcceleration : AirAcceleration) * controlFactor;
            bool alreadyPastMoveSpeed = MathF.Abs(Velocity.X) >= MaxMoveSpeed
                && MathF.Sign(Velocity.X) == MathF.Sign(horizontalInput);

            if (!alreadyPastMoveSpeed)
            {
                AddForce(new Vector2(horizontalInput * acceleration * Mass, 0f));
            }
            else if (!IsGrounded)
            {
                BleedExcessHorizontalSpeed(dt, bleedRate: 420f);
            }
        }

        if (MathF.Abs(horizontalInput) <= 0.01f || !IsGrounded)
        {
            ApplyHorizontalDrag((IsGrounded ? GroundFriction : AirDrag) * controlFactor);
        }
    }

    internal void ApplyJumpImpulse(PlayerInputState input)
    {
        if (!input.JumpPressed || !IsGrounded)
        {
            return;
        }

        AddImpulse(new Vector2(0f, -JumpImpulse * Mass));
        IsGrounded = false;
    }

    internal void ApplyGravity(float gravity, PlayerInputState input)
    {
        float gravityMultiplier = input.FastFallHeld && Velocity.Y > 0f
            ? GravityScale * FastFallGravityMultiplier
            : GravityScale;

        AddForce(new Vector2(0f, gravity * gravityMultiplier * Mass));
    }

    internal void ApplyEjectionForces()
    {
        if (State != PlayerState.Ejecting || _ejectionPlatform == null)
        {
            _ejectionForce = 0f;
            return;
        }

        if (!RefreshEjectionInfoFromTarget())
        {
            FinishEjection();
            return;
        }

        float ramp = GetEjectionRampAmount();
        float strength = GetEjectionStrengthMultiplier(_ejectionCenterInfluence);
        float acceleration = MathF.Max(0f, EjectionAcceleration) * strength * ramp;
        float maxOutwardSpeed = MathF.Max(1f, EjectionMaxSpeed) * strength;
        float outwardSpeed = Vector2.Dot(Velocity, _ejectionForceDirection);

        if (outwardSpeed >= maxOutwardSpeed)
        {
            _ejectionForce = 0f;
            return;
        }

        float remainingSpeedRatio = MathHelper.Clamp(
            (maxOutwardSpeed - outwardSpeed) / MathF.Max(1f, maxOutwardSpeed * 0.45f),
            0.18f,
            1f);

        _ejectionForce = acceleration * remainingSpeedRatio;
        AddForce(_ejectionForceDirection * _ejectionForce * Mass);
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

    internal void ClampGroundedMoveSpeed()
    {
        if (!IsGrounded)
        {
            return;
        }

        float absSpeed = MathF.Abs(Velocity.X);
        if (absSpeed <= MaxMoveSpeed)
        {
            return;
        }

        Velocity = new Vector2(MathF.Sign(Velocity.X) * MaxMoveSpeed, Velocity.Y);
    }

    internal static float NormalizeMovementInput(float horizontalInput)
    {
        if (MathF.Abs(horizontalInput) >= 0.55f)
        {
            return MathF.Sign(horizontalInput);
        }

        return horizontalInput;
    }

    internal void FinishPhysicsStep(Level level)
    {
        UpdateGroundedLaunchEnd();
        if (State == PlayerState.Ejecting && !IsEjectionTargetStillSolidAndOverlapping())
        {
            FinishEjection();
            IsGrounded = HasGroundBelow(level);
        }
    }

    private void ApplyHorizontalDrag(float drag)
    {
        if (MathF.Abs(Velocity.X) <= 0.01f)
        {
            return;
        }

        AddForce(new Vector2(-Velocity.X * drag * Mass, 0f));
    }

    private void BleedExcessHorizontalSpeed(float dt, float bleedRate)
    {
        float cap = MaxMoveSpeed;
        float absSpeed = MathF.Abs(Velocity.X);
        if (absSpeed <= cap)
        {
            return;
        }

        float sign = MathF.Sign(Velocity.X);
        float target = sign * cap;
        float step = bleedRate * dt;
        float delta = absSpeed - cap;
        if (delta <= step)
        {
            Velocity = new Vector2(target, Velocity.Y);
            return;
        }

        Velocity = new Vector2(Velocity.X - (sign * step), Velocity.Y);
    }

    private void HandleColorChange(PlayerInputState input, Level level)
    {
        if (input.RequestedColor is not { } requestedColor || requestedColor == CurrentColor)
        {
            return;
        }

        CurrentColor = requestedColor;

        if (State == PlayerState.Ejecting)
        {
            FinishEjection(enableLaunchControl: false);
        }

        TryStartEjectionFromOverlaps(level);
    }

    internal bool IsEjectingFrom(Platform platform)
    {
        return State == PlayerState.Ejecting && ReferenceEquals(platform, _ejectionPlatform);
    }

    private void TryStartEjectionFromOverlaps(Level level)
    {
        if (State == PlayerState.Ejecting || IsFrozen)
        {
            return;
        }

        if (!TryFindBestEjectionCandidate(
            level,
            out Platform platform,
            out Vector2 direction,
            out Vector2 platformCenter,
            out float penetrationDepth,
            out float centerInfluence))
        {
            return;
        }

        StartEjection(platform, direction, platformCenter, penetrationDepth, centerInfluence);
    }

    private bool TryFindBestEjectionCandidate(
        Level level,
        out Platform bestPlatform,
        out Vector2 bestDirection,
        out Vector2 bestPlatformCenter,
        out float bestPenetrationDepth,
        out float bestCenterInfluence)
    {
        bestPlatform = null;
        bestDirection = Vector2.Zero;
        bestPlatformCenter = Vector2.Zero;
        bestPenetrationDepth = 0f;
        bestCenterInfluence = 0f;

        float bestScore = float.MinValue;
        Vector2 fallbackDirection = GetFallbackEjectionDirection();

        foreach (Platform platform in level.GetCollidablePlatforms(CurrentColor))
        {
            if (!TryCalculateEjectionInfo(
                platform,
                fallbackDirection,
                out Vector2 direction,
                out Vector2 platformCenter,
                out float penetrationDepth,
                out float centerInfluence))
            {
                continue;
            }

            float score = (centerInfluence * 1000f) + penetrationDepth;
            if (score <= bestScore)
            {
                continue;
            }

            bestPlatform = platform;
            bestDirection = direction;
            bestPlatformCenter = platformCenter;
            bestPenetrationDepth = penetrationDepth;
            bestCenterInfluence = centerInfluence;
            bestScore = score;
        }

        return bestPlatform != null;
    }

    private void StartEjection(
        Platform platform,
        Vector2 direction,
        Vector2 platformCenter,
        float penetrationDepth,
        float centerInfluence)
    {
        State = PlayerState.Ejecting;
        _ejectionPlatform = platform;
        _ejectionBaseDirection = direction;
        _ejectionForceDirection = direction;
        _ejectionPlatformCenter = platformCenter;
        _ejectionTimer = 0f;
        _ejectionRampAmount = GetEjectionRampAmount();
        _ejectionForce = 0f;
        _ejectionPenetrationDepth = penetrationDepth;
        _ejectionCenterInfluence = centerInfluence;
        _ejectionPeakRaised = false;
        _justLaunched = false;
        _launchControlRemaining = 0f;
        IsGrounded = false;

        _debugEscapeVectorTimeRemaining = DebugEscapeVectorSeconds;
        _debugEscapeVectorStart = platformCenter;
        _debugEscapeVector = direction * MathHelper.Clamp(EjectionMaxSpeed * 0.12f, 28f, 110f);

        OnEjectionStart?.Invoke(this);
    }

    private void UpdateEjectionState(float dt)
    {
        if (State != PlayerState.Ejecting)
        {
            return;
        }

        _ejectionTimer += dt;
        if (!RefreshEjectionInfoFromTarget())
        {
            FinishEjection();
            return;
        }

        _ejectionRampAmount = GetEjectionRampAmount();
        if (!_ejectionPeakRaised && _ejectionTimer >= MathF.Max(0.001f, EjectionDuration))
        {
            _ejectionPeakRaised = true;
            OnEjectionPeak?.Invoke(this);
        }
    }

    private bool RefreshEjectionInfoFromTarget()
    {
        if (!IsEjectionTargetStillSolidAndOverlapping())
        {
            return false;
        }

        Vector2 fallbackDirection = _ejectionBaseDirection == Vector2.Zero
            ? GetFallbackEjectionDirection()
            : _ejectionBaseDirection;

        if (!TryCalculateEjectionInfo(
            _ejectionPlatform,
            fallbackDirection,
            out Vector2 direction,
            out Vector2 platformCenter,
            out float penetrationDepth,
            out float centerInfluence))
        {
            return false;
        }

        if (_ejectionBaseDirection != Vector2.Zero)
        {
            float alignment = Vector2.Dot(direction, _ejectionBaseDirection);
            direction = alignment < 0.15f
                ? _ejectionBaseDirection
                : NormalizeOrFallback(Vector2.Lerp(_ejectionBaseDirection, direction, 0.35f), _ejectionBaseDirection);
        }

        _ejectionForceDirection = direction;
        _ejectionPlatformCenter = platformCenter;
        _ejectionPenetrationDepth = penetrationDepth;
        _ejectionCenterInfluence = centerInfluence;
        return true;
    }

    private bool TryCalculateEjectionInfo(
        Platform platform,
        Vector2 fallbackDirection,
        out Vector2 direction,
        out Vector2 platformCenter,
        out float penetrationDepth,
        out float centerInfluence)
    {
        direction = Vector2.Zero;
        platformCenter = GetPlatformCenter(platform);
        penetrationDepth = 0f;
        centerInfluence = 0f;

        if (!TryGetPenetrationDepth(platform.Bounds, out penetrationDepth))
        {
            return false;
        }

        Vector2 playerCenter = Position + (Size * 0.5f);
        Vector2 centerDelta = playerCenter - platformCenter;

        direction = NormalizeOrFallback(centerDelta, fallbackDirection);
        centerInfluence = CalculateCenterInfluence(centerDelta, platform.Bounds);
        return true;
    }

    private bool IsEjectionTargetStillSolidAndOverlapping()
    {
        return _ejectionPlatform != null
            && _ejectionPlatform.PlatformColor == CurrentColor
            && CollisionHelper.Intersects(Position, Size, _ejectionPlatform.Bounds);
    }

    private void FinishEjection(bool enableLaunchControl = true)
    {
        if (State != PlayerState.Ejecting)
        {
            return;
        }

        Vector2 finalDirection = _ejectionForceDirection;
        State = PlayerState.Normal;
        _ejectionPlatform = null;
        _ejectionBaseDirection = Vector2.Zero;
        _ejectionForceDirection = Vector2.Zero;
        _ejectionForce = 0f;
        _ejectionRampAmount = 0f;
        _ejectionPenetrationDepth = 0f;
        _ejectionCenterInfluence = 0f;
        _ejectionPeakRaised = false;

        if (enableLaunchControl)
        {
            _justLaunched = true;
            _launchControlRemaining = LaunchControlSeconds;
        }

        if (finalDirection != Vector2.Zero)
        {
            _debugEscapeVectorTimeRemaining = DebugEscapeVectorSeconds;
            _debugEscapeVectorStart = Position + (Size * 0.5f);
            _debugEscapeVector = finalDirection * MathHelper.Clamp(MathF.Abs(Vector2.Dot(Velocity, finalDirection)) * 0.16f, 20f, 110f);
        }

        OnEjectionEnd?.Invoke(this);
    }

    private void ClearTransientMotionState()
    {
        State = PlayerState.Normal;
        _ejectionPlatform = null;
        _ejectionBaseDirection = Vector2.Zero;
        _ejectionForceDirection = Vector2.Zero;
        _ejectionPlatformCenter = Vector2.Zero;
        _ejectionTimer = 0f;
        _ejectionRampAmount = 0f;
        _ejectionForce = 0f;
        _ejectionPenetrationDepth = 0f;
        _ejectionCenterInfluence = 0f;
        _ejectionPeakRaised = false;
        _justLaunched = false;
        _launchControlRemaining = 0f;
    }

    private bool HasGroundBelow(Level level)
    {
        Vector2 probePosition = Position + new Vector2(0f, 2f);

        foreach (Platform platform in level.GetCollidablePlatforms(CurrentColor))
        {
            if (IsEjectingFrom(platform))
            {
                continue;
            }

            if (CollisionHelper.Intersects(probePosition, Size, platform.Bounds))
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 GetFallbackEjectionDirection()
    {
        if (_ejectionBaseDirection.LengthSquared() > MinimumEjectionFallbackLengthSquared)
        {
            return Vector2.Normalize(_ejectionBaseDirection);
        }

        if (Velocity.LengthSquared() > MinimumEjectionFallbackLengthSquared)
        {
            return Vector2.Normalize(Velocity);
        }

        if (LastCollisionNormal.LengthSquared() > MinimumEjectionFallbackLengthSquared)
        {
            return Vector2.Normalize(LastCollisionNormal);
        }

        return new Vector2(0f, -1f);
    }

    private Vector2 NormalizeOrFallback(Vector2 value, Vector2 fallback)
    {
        if (value.LengthSquared() > MinimumEjectionFallbackLengthSquared)
        {
            return Vector2.Normalize(value);
        }

        if (fallback.LengthSquared() > MinimumEjectionFallbackLengthSquared)
        {
            return Vector2.Normalize(fallback);
        }

        return new Vector2(0f, -1f);
    }

    private float GetEjectionRampAmount()
    {
        float duration = MathF.Max(0.001f, EjectionDuration);
        float durationProgress = MathHelper.Clamp(_ejectionTimer / duration, 0f, 1f);
        float easedDuration = durationProgress * durationProgress * (3f - (2f * durationProgress));
        float rampProgress = 1f - MathF.Exp(-MathF.Max(0.001f, EjectionRampSpeed) * _ejectionTimer);
        float combinedRamp = MathHelper.Clamp(easedDuration * rampProgress, 0f, 1f);

        return MathHelper.Lerp(0.16f, 1f, combinedRamp);
    }

    private float GetEjectionStrengthMultiplier(float centerInfluence)
    {
        float centerAmount = MathHelper.Clamp(centerInfluence, 0f, 1f);
        float centerBoost = MathF.Max(0f, EjectionCenterForceMultiplier);

        return MathHelper.Lerp(0.65f, 1f + centerBoost, centerAmount);
    }

    private static Vector2 GetPlatformCenter(Platform platform)
    {
        return new Vector2(
            platform.Bounds.X + (platform.Bounds.Width * 0.5f),
            platform.Bounds.Y + (platform.Bounds.Height * 0.5f));
    }

    private bool TryGetPenetrationDepth(Rectangle obstacle, out float penetrationDepth)
    {
        penetrationDepth = 0f;

        float left = Position.X;
        float right = Position.X + Size.X;
        float top = Position.Y;
        float bottom = Position.Y + Size.Y;

        float overlapLeft = right - obstacle.Left;
        float overlapRight = obstacle.Right - left;
        float overlapTop = bottom - obstacle.Top;
        float overlapBottom = obstacle.Bottom - top;

        if (overlapLeft <= 0f || overlapRight <= 0f || overlapTop <= 0f || overlapBottom <= 0f)
        {
            return false;
        }

        penetrationDepth = MathF.Min(
            MathF.Min(overlapLeft, overlapRight),
            MathF.Min(overlapTop, overlapBottom));
        return true;
    }

    private float CalculateCenterInfluence(Vector2 centerDelta, Rectangle platformBounds)
    {
        float combinedHalfWidth = MathF.Max(1f, (platformBounds.Width * 0.5f) + (Size.X * 0.5f));
        float combinedHalfHeight = MathF.Max(1f, (platformBounds.Height * 0.5f) + (Size.Y * 0.5f));
        float normalizedDistance = MathHelper.Clamp(
            MathF.Max(MathF.Abs(centerDelta.X) / combinedHalfWidth, MathF.Abs(centerDelta.Y) / combinedHalfHeight),
            0f,
            1f);

        return 1f - normalizedDistance;
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

    private void UpdateDebugEscapeVector(float dt)
    {
        if (_debugEscapeVectorTimeRemaining > 0f)
        {
            _debugEscapeVectorTimeRemaining = MathF.Max(0f, _debugEscapeVectorTimeRemaining - dt);
        }
    }

    private Rectangle GetVisualBodyBounds(Rectangle bounds)
    {
        if (State != PlayerState.Ejecting || _ejectionForceDirection == Vector2.Zero)
        {
            return bounds;
        }

        float amount = MathHelper.Clamp(_ejectionRampAmount, 0f, 1f);
        Rectangle visualBounds = bounds;
        visualBounds.Inflate(
            (int)MathF.Round(MathF.Abs(_ejectionForceDirection.X) * 4f * amount),
            (int)MathF.Round(MathF.Abs(_ejectionForceDirection.Y) * 4f * amount));
        visualBounds.Offset(
            (int)MathF.Round(_ejectionForceDirection.X * 2f * amount),
            (int)MathF.Round(_ejectionForceDirection.Y * 2f * amount));

        return visualBounds;
    }

    private void DrawEjectionFeedback(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds)
    {
        if (State != PlayerState.Ejecting || _ejectionForceDirection == Vector2.Zero)
        {
            return;
        }

        float amount = MathHelper.Clamp(_ejectionRampAmount, 0f, 1f);
        Color glowColor = Color.Lerp(CurrentColor.ToXnaColor(), ColorPaletteManager.Get(ColorType.White), 0.35f) * (0.12f + (0.12f * amount));

        Rectangle glowBounds = bounds;
        glowBounds.Inflate(3 + (int)MathF.Round(4f * amount), 3 + (int)MathF.Round(4f * amount));
        spriteBatch.Draw(pixel, glowBounds, glowColor);

        for (int i = 1; i <= 2; i++)
        {
            Vector2 offset = -_ejectionForceDirection * (i * (6f + (amount * 5f)));
            Rectangle streakBounds = bounds;
            streakBounds.Offset((int)MathF.Round(offset.X), (int)MathF.Round(offset.Y));
            streakBounds.Inflate(-i * 4, -i * 4);
            spriteBatch.Draw(pixel, streakBounds, glowColor * (0.45f / i));
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

    private void DrawDebugEjection(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (State != PlayerState.Ejecting)
        {
            return;
        }

        Vector2 center = Position + (Size * 0.5f);
        Rectangle platformCenterMarker = new(
            (int)MathF.Round(_ejectionPlatformCenter.X) - 4,
            (int)MathF.Round(_ejectionPlatformCenter.Y) - 4,
            8,
            8);

        spriteBatch.Draw(pixel, platformCenterMarker, Color.Magenta);
        DrawLine(spriteBatch, pixel, _ejectionPlatformCenter, center, Color.Yellow, 3);
        DrawDebugVector(spriteBatch, pixel, center, _ejectionForceDirection * 52f, Color.Yellow);
        DrawDebugVector(
            spriteBatch,
            pixel,
            center,
            _ejectionForceDirection * MathHelper.Clamp(_ejectionForce * 0.018f, 18f, 124f),
            new Color(255, 128, 48));

        Vector2 textPosition = center + new Vector2(30f, -54f);
        DrawDebugText(spriteBatch, pixel, "STATE EJECTING", textPosition, Color.Yellow);
        DrawDebugText(spriteBatch, pixel, $"TIMER {_ejectionTimer:0.00}/{EjectionDuration:0.00}", textPosition + new Vector2(0f, 10f), Color.White);
        DrawDebugText(spriteBatch, pixel, $"FORCE {_ejectionForce:0} RAMP {_ejectionRampAmount:0.00}", textPosition + new Vector2(0f, 20f), new Color(255, 168, 64));
        DrawDebugText(spriteBatch, pixel, $"PEN {_ejectionPenetrationDepth:0.0} CENTER {_ejectionCenterInfluence:0.00}", textPosition + new Vector2(0f, 30f), Color.White);
        DrawDebugText(spriteBatch, pixel, $"DIR {_ejectionForceDirection.X:0.00},{_ejectionForceDirection.Y:0.00}", textPosition + new Vector2(0f, 40f), Color.White);
        DrawDebugText(spriteBatch, pixel, $"PC {_ejectionPlatformCenter.X:0},{_ejectionPlatformCenter.Y:0}", textPosition + new Vector2(0f, 50f), Color.Magenta);
    }

    private void DrawDebugVector(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 vector, Color color)
    {
        if (vector == Vector2.Zero)
        {
            return;
        }

        Vector2 end = start + vector;
        DrawLine(spriteBatch, pixel, start, end, color, 3);
        spriteBatch.Draw(pixel, new Rectangle((int)MathF.Round(end.X) - 3, (int)MathF.Round(end.Y) - 3, 6, 6), color);
    }

    private void DrawDebugEscapeVector(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (_debugEscapeVectorTimeRemaining <= 0f || _debugEscapeVector == Vector2.Zero)
        {
            return;
        }

        Vector2 end = _debugEscapeVectorStart + _debugEscapeVector;
        DrawLine(spriteBatch, pixel, _debugEscapeVectorStart, end, Color.Yellow, 4);
        spriteBatch.Draw(pixel, new Rectangle((int)MathF.Round(end.X) - 5, (int)MathF.Round(end.Y) - 5, 10, 10), Color.Yellow);
    }

    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 start, Vector2 end, Color color, int thickness)
    {
        Vector2 delta = end - start;
        float length = delta.Length();
        if (length <= 0.01f)
        {
            return;
        }

        float rotation = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(
            pixel,
            start,
            null,
            color,
            rotation,
            new Vector2(0f, 0.5f),
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    private static void DrawDebugText(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 position, Color color)
    {
        SimpleTextRenderer.DrawString(spriteBatch, pixel, text, position + new Vector2(1f, 1f), 1, Color.Black);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, text, position, 1, color);
    }

    private void DrawPlayerIndicator(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds)
    {
        string label = DisplayLabel;
        const int scale = 2;
        Point textSize = SimpleTextRenderer.MeasureString(label, scale);
        Vector2 position = new(
            MathF.Round(bounds.Center.X - (textSize.X * 0.5f)),
            MathF.Round(bounds.Top - textSize.Y - 6));

        SimpleTextRenderer.DrawString(spriteBatch, pixel, label, position + new Vector2(1f, 1f), scale, Color.Black);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, label, position, scale, Color.White);
    }
}
