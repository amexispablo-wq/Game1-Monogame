using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

/// <summary>
/// Deterministic phased physics pipeline.
/// Players are never fully integrated one-by-one before the next player starts.
/// </summary>
public sealed class PhysicsWorld
{
    public const float FixedTimeStep = 1f / 60f;
    public const float Gravity = 1600f;

    private readonly Level _level;
    private readonly Dictionary<int, float> _launchPadCooldowns = new();
    private readonly List<Player> _simulationOrder = new();
    private readonly List<Platform> _sortedPlatformsScratch = new();

    public PhysicsWorld(
        Level level,
        List<Player> players,
        GameSession session,
        RopeGameplayMode ropeGameplayMode = RopeGameplayMode.ColoredPhysics)
    {
        _level = level;
        Players = players;
        RopeGameplayMode = ropeGameplayMode;

        for (int i = 0; i < Players.Count - 1; i++)
        {
            int ropeNetworkId = session.AllocateNetworkId();
            NetworkEntityOwnership ownership = new(ropeNetworkId, session.HostOwnerId, session.IsHost, true);
            Rope rope = new(
                Players[i],
                Players[i + 1],
                Players,
                RopeGameplayMode,
                ownership);
            Ropes.Add(rope);
            MultiplayerDebug.LogRope(
                $"Create Rope NetworkId={ropeNetworkId} OwnerId={session.HostOwnerId} " +
                $"IsLocal={session.IsHost} IsHostControlled=true " +
                $"P{Players[i].PlayerIndex + 1}(N{Players[i].NetworkId})-P{Players[i + 1].PlayerIndex + 1}(N{Players[i + 1].NetworkId})");
        }

        MultiplayerDebug.LogRope($"Rope chain done count={Ropes.Count} for players={Players.Count}");
        RefreshSimulationOrder();
    }

    public List<Player> Players { get; }
    public List<Rope> Ropes { get; } = new();
    public RopeGameplayMode RopeGameplayMode { get; }
    public bool PlayerCollisionEnabled { get; set; }
    public Vector2 LastLaunchForce { get; private set; }
    public float LastSimulationStepSeconds { get; private set; }

    public void UpdatePhysics(float dt, IReadOnlyDictionary<int, PlayerInputState> inputStates)
    {
        if (dt <= 0f)
        {
            return;
        }

        LastSimulationStepSeconds = dt;
        RefreshSimulationOrder();
        ApplyGameplayTuning();
        UpdateLaunchPadCooldowns(dt);

        // Phase 1: read input and accumulate forces for every player.
        foreach (Player player in _simulationOrder)
        {
            PrepareBody(player, GetInputFor(player, inputStates), dt);
        }

        // Phase 2: integrate accumulated forces into velocity for every player.
        foreach (Player player in _simulationOrder)
        {
            IntegrateBodyForces(player, dt);
        }

        // Phase 3: resolve rope constraints and compute endpoint coupling forces.
        foreach (Rope rope in Ropes)
        {
            rope.Simulate(
                dt,
                Gravity,
                _level.Platforms,
                GetInputFor(rope.StartPlayer, inputStates).PullRopeHeld,
                GetInputFor(rope.EndPlayer, inputStates).PullRopeHeld);
        }

        // Phase 4: integrate horizontal movement and resolve horizontal collisions for every player.
        foreach (Player player in _simulationOrder)
        {
            MoveHorizontally(player, dt);
        }

        // Phase 5: integrate vertical movement and resolve vertical collisions for every player.
        foreach (Player player in _simulationOrder)
        {
            MoveVertically(player, dt);
        }

        // Phase 6: launch pads and final velocity clamp for every player.
        foreach (Player player in _simulationOrder)
        {
            ApplyLaunchPads(player);
            player.ClampGroundedMoveSpeed();
            player.ClampVelocity();
        }
    }

    public void ResetRopesForPlayer(Player player)
    {
        _launchPadCooldowns.Remove(player.NetworkId);

        foreach (Rope rope in Ropes)
        {
            if (ReferenceEquals(rope.StartPlayer, player) || ReferenceEquals(rope.EndPlayer, player))
            {
                rope.ResetBetweenPlayers();
            }
        }
    }

    public void ClearTransientState()
    {
        _launchPadCooldowns.Clear();
        LastLaunchForce = Vector2.Zero;

        foreach (Rope rope in Ropes)
        {
            rope.ResetBetweenPlayers();
        }
    }

    private void RefreshSimulationOrder()
    {
        _simulationOrder.Clear();
        _simulationOrder.AddRange(Players);
        _simulationOrder.Sort(static (a, b) => a.NetworkId.CompareTo(b.NetworkId));
    }

    private void ApplyGameplayTuning()
    {
        GameplayTuning tuning = GameplayTuning.Active;
        foreach (Player player in Players)
        {
            tuning.ApplyTo(player);
        }

        foreach (Rope rope in Ropes)
        {
            tuning.ApplyTo(rope);
        }
    }

    private void PrepareBody(Player player, PlayerInputState input, float dt)
    {
        if (!ShouldSimulate(player) || player.IsFrozen)
        {
            return;
        }

        player.BeginPhysicsStep(dt, _level, GetPlayerCollisionTargets());
        player.HandleInputState(input, _level, GetPlayerCollisionTargets());
        player.RefreshGroundedState(_level, GetPlayerCollisionTargets());
        player.ApplyMovementForces(input, dt);
        player.ApplyJumpImpulse(input);
        player.ApplyGravity(Gravity, input);
        player.ApplyEjectionForces();
    }

    private void IntegrateBodyForces(Player player, float dt)
    {
        if (!ShouldSimulate(player) || player.IsFrozen)
        {
            return;
        }

        player.IntegrateForces(dt);
    }

    private void MoveHorizontally(Player player, float dt)
    {
        if (!ShouldSimulate(player) || player.IsFrozen)
        {
            return;
        }

        player.IntegratePosition(new Vector2(player.Velocity.X * dt, 0f));
        ResolveHorizontalCollisions(player);
    }

    private void MoveVertically(Player player, float dt)
    {
        if (!ShouldSimulate(player) || player.IsFrozen)
        {
            return;
        }

        player.IntegratePosition(new Vector2(0f, player.Velocity.Y * dt));
        player.IsGrounded = false;
        ResolveVerticalCollisions(player);
        player.FinishPhysicsStep(_level, GetPlayerCollisionTargets());
    }

    private void ApplyLaunchPads(Player player)
    {
        if (!ShouldSimulate(player) || player.IsFrozen || IsLaunchPadCoolingDown(player))
        {
            return;
        }

        float launchMultiplier = MathF.Max(0f, GameplayTuning.Active.LaunchForceMultiplier);
        foreach (LaunchPad launchPad in _level.LaunchPads)
        {
            if (!CollisionHelper.Intersects(player.Position, player.Size, launchPad.TriggerBounds))
            {
                continue;
            }

            Vector2 direction = launchPad.LaunchDirection;
            Vector2 lateralVelocity = player.Velocity - (direction * Vector2.Dot(player.Velocity, direction));
            float maxLateralSpeed = MathF.Max(60f, LaunchPad.LaunchPadForce * 0.18f);
            if (lateralVelocity.LengthSquared() > maxLateralSpeed * maxLateralSpeed)
            {
                lateralVelocity = Vector2.Normalize(lateralVelocity) * maxLateralSpeed;
            }

            Vector2 launchVelocity = (direction * LaunchPad.LaunchPadForce * launchMultiplier) + (lateralVelocity * 0.25f);
            player.LaunchFromPad(launchVelocity);
            _launchPadCooldowns[player.NetworkId] = MathF.Max(0.01f, LaunchPad.LaunchPadCooldown);
            LastLaunchForce = direction * LaunchPad.LaunchPadForce * launchMultiplier;
            GameAudio.Play(SfxManager.LaunchPad);
            return;
        }
    }

    private bool IsLaunchPadCoolingDown(Player player)
    {
        return _launchPadCooldowns.TryGetValue(player.NetworkId, out float cooldown) && cooldown > 0f;
    }

    private void UpdateLaunchPadCooldowns(float dt)
    {
        if (_launchPadCooldowns.Count == 0)
        {
            return;
        }

        List<int> players = new(_launchPadCooldowns.Keys);
        List<int> expiredPlayers = new();
        foreach (int playerId in players)
        {
            float nextCooldown = _launchPadCooldowns[playerId] - dt;
            if (nextCooldown <= 0f)
            {
                expiredPlayers.Add(playerId);
            }
            else
            {
                _launchPadCooldowns[playerId] = nextCooldown;
            }
        }

        foreach (int playerId in expiredPlayers)
        {
            _launchPadCooldowns.Remove(playerId);
        }
    }

    private void ResolveHorizontalCollisions(Player player)
    {
        foreach (Platform platform in GetSortedCollidablePlatforms(player))
        {
            if (player.IsEjectingFrom(platform))
            {
                continue;
            }

            if (!CollisionHelper.Intersects(player.Position, player.Size, platform.Bounds))
            {
                continue;
            }

            Vector2 correction = Vector2.Zero;
            Vector2 normal = Vector2.Zero;

            if (player.Velocity.X > 0f)
            {
                correction.X = platform.Bounds.Left - (player.Position.X + player.Size.X);
                normal = new Vector2(-1f, 0f);
            }
            else if (player.Velocity.X < 0f)
            {
                correction.X = platform.Bounds.Right - player.Position.X;
                normal = new Vector2(1f, 0f);
            }
            else if (CollisionHelper.TryGetMinimumTranslationVector(
                player.Position,
                player.Size,
                platform.Bounds,
                out Vector2 escape,
                out Vector2 escapeDirection,
                out _))
            {
                correction = new Vector2(escape.X, 0f);
                normal = new Vector2(escapeDirection.X, 0f);
            }

            if (correction == Vector2.Zero)
            {
                continue;
            }

            player.ApplyCollisionCorrection(correction, normal);
            player.Velocity = new Vector2(0f, player.Velocity.Y);
        }

        if (PlayerCollisionEnabled)
        {
            ResolveHorizontalPlayerCollisions(player);
        }
    }

    private void ResolveHorizontalPlayerCollisions(Player player)
    {
        foreach (Player other in _simulationOrder)
        {
            if (ReferenceEquals(other, player))
            {
                continue;
            }

            if (other.CurrentColor != player.CurrentColor)
            {
                continue;
            }

            if (player.IsEjectingFrom(other) || other.IsEjectingFrom(player))
            {
                continue;
            }

            if (player.State == PlayerState.Ejecting || other.State == PlayerState.Ejecting)
            {
                // Let ejection force separate them — MTV teleport fights the eject feel.
                continue;
            }

            if (!CollisionHelper.Intersects(player.Position, player.Size, other.Bounds))
            {
                continue;
            }

            Vector2 correction = Vector2.Zero;
            Vector2 normal = Vector2.Zero;

            if (player.Velocity.X > 0f)
            {
                correction.X = other.Bounds.Left - (player.Position.X + player.Size.X);
                normal = new Vector2(-1f, 0f);
            }
            else if (player.Velocity.X < 0f)
            {
                correction.X = other.Bounds.Right - player.Position.X;
                normal = new Vector2(1f, 0f);
            }
            else if (CollisionHelper.TryGetMinimumTranslationVector(
                player.Position,
                player.Size,
                other.Bounds,
                out Vector2 escape,
                out Vector2 escapeDirection,
                out _))
            {
                correction = new Vector2(escape.X, 0f);
                normal = new Vector2(escapeDirection.X, 0f);
            }

            if (correction == Vector2.Zero)
            {
                continue;
            }

            player.ApplyCollisionCorrection(correction, normal);
            player.Velocity = new Vector2(0f, player.Velocity.Y);
        }
    }

    private void ResolveVerticalCollisions(Player player)
    {
        foreach (Platform platform in GetSortedCollidablePlatforms(player))
        {
            if (player.IsEjectingFrom(platform))
            {
                continue;
            }

            if (!CollisionHelper.Intersects(player.Position, player.Size, platform.Bounds))
            {
                continue;
            }

            Vector2 correction = Vector2.Zero;
            Vector2 normal = Vector2.Zero;

            if (player.Velocity.Y > 0f)
            {
                correction.Y = platform.Bounds.Top - (player.Position.Y + player.Size.Y);
                normal = new Vector2(0f, -1f);
                player.IsGrounded = true;
            }
            else if (player.Velocity.Y < 0f)
            {
                correction.Y = platform.Bounds.Bottom - player.Position.Y;
                normal = new Vector2(0f, 1f);
            }
            else if (CollisionHelper.TryGetMinimumTranslationVector(
                player.Position,
                player.Size,
                platform.Bounds,
                out Vector2 escape,
                out Vector2 escapeDirection,
                out _))
            {
                correction = new Vector2(0f, escape.Y);
                normal = new Vector2(0f, escapeDirection.Y);
                player.IsGrounded = escape.Y < 0f;
            }

            if (correction == Vector2.Zero)
            {
                continue;
            }

            player.ApplyCollisionCorrection(correction, normal);
            player.Velocity = new Vector2(player.Velocity.X, 0f);
        }

        if (PlayerCollisionEnabled)
        {
            ResolveVerticalPlayerCollisions(player);
        }
    }

    private void ResolveVerticalPlayerCollisions(Player player)
    {
        foreach (Player other in _simulationOrder)
        {
            if (ReferenceEquals(other, player))
            {
                continue;
            }

            if (other.CurrentColor != player.CurrentColor)
            {
                continue;
            }

            if (player.IsEjectingFrom(other) || other.IsEjectingFrom(player))
            {
                continue;
            }

            if (player.State == PlayerState.Ejecting || other.State == PlayerState.Ejecting)
            {
                continue;
            }

            if (!CollisionHelper.Intersects(player.Position, player.Size, other.Bounds))
            {
                continue;
            }

            Vector2 correction = Vector2.Zero;
            Vector2 normal = Vector2.Zero;

            if (player.Velocity.Y > 0f)
            {
                correction.Y = other.Bounds.Top - (player.Position.Y + player.Size.Y);
                normal = new Vector2(0f, -1f);
                player.IsGrounded = true;
            }
            else if (player.Velocity.Y < 0f)
            {
                correction.Y = other.Bounds.Bottom - player.Position.Y;
                normal = new Vector2(0f, 1f);
            }
            else if (CollisionHelper.TryGetMinimumTranslationVector(
                player.Position,
                player.Size,
                other.Bounds,
                out Vector2 escape,
                out Vector2 escapeDirection,
                out _))
            {
                correction = new Vector2(0f, escape.Y);
                normal = new Vector2(0f, escapeDirection.Y);
                player.IsGrounded = escape.Y < 0f;
            }

            if (correction == Vector2.Zero)
            {
                continue;
            }

            player.ApplyCollisionCorrection(correction, normal);
            player.Velocity = new Vector2(player.Velocity.X, 0f);
        }
    }

    private IReadOnlyList<Platform> GetSortedCollidablePlatforms(Player player)
    {
        _sortedPlatformsScratch.Clear();
        foreach (Platform platform in _level.GetCollidablePlatforms(player.CurrentColor))
        {
            _sortedPlatformsScratch.Add(platform);
        }

        _sortedPlatformsScratch.Sort(static (a, b) =>
        {
            int compareX = a.Bounds.X.CompareTo(b.Bounds.X);
            return compareX != 0 ? compareX : a.Bounds.Y.CompareTo(b.Bounds.Y);
        });

        return _sortedPlatformsScratch;
    }

    private IReadOnlyList<Player> GetPlayerCollisionTargets()
    {
        return PlayerCollisionEnabled ? Players : null;
    }

    private static PlayerInputState GetInputFor(
        Player player,
        IReadOnlyDictionary<int, PlayerInputState> inputStates)
    {
        if (!ShouldSimulate(player))
        {
            return PlayerInputState.Empty;
        }

        return inputStates.TryGetValue(player.NetworkId, out PlayerInputState input)
            ? input
            : PlayerInputState.Empty;
    }

    private static bool ShouldSimulate(Player player)
    {
        return player.IsLocal || player.IsHostControlled;
    }
}
