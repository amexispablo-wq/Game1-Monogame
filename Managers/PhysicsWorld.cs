using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public sealed class PhysicsWorld
{
    public const float FixedTimeStep = 1f / 60f;
    public const float Gravity = 1600f;

    private readonly Level _level;
    private readonly Dictionary<int, float> _launchPadCooldowns = new();

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
            Ropes.Add(new Rope(
                Players[i],
                Players[i + 1],
                Players,
                RopeGameplayMode,
                new NetworkEntityOwnership(session.AllocateNetworkId(), session.HostOwnerId, session.IsHost, true)));
        }
    }

    public List<Player> Players { get; }
    public List<Rope> Ropes { get; } = new();
    public RopeGameplayMode RopeGameplayMode { get; }
    public Vector2 LastLaunchForce { get; private set; }

    public void UpdatePhysics(float dt, IReadOnlyDictionary<int, PlayerInputState> inputStates)
    {
        if (dt <= 0f)
        {
            return;
        }

        UpdateLaunchPadCooldowns(dt);

        foreach (Player player in Players)
        {
            PrepareBody(player, GetInputFor(player, inputStates), dt);
        }

        foreach (Player player in Players)
        {
            IntegrateBodyForces(player, dt);
        }

        SolveConstraints(dt);

        foreach (Player player in Players)
        {
            MoveAndSolveCollisions(player, dt);
            ApplyLaunchPads(player);
        }

        foreach (Rope rope in Ropes)
        {
            rope.Simulate(
                dt,
                Gravity,
                _level.Platforms,
                GetInputFor(rope.StartPlayer, inputStates).PullRopeHeld,
                GetInputFor(rope.EndPlayer, inputStates).PullRopeHeld);
        }

        foreach (Player player in Players)
        {
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

    private void PrepareBody(Player player, PlayerInputState input, float dt)
    {
        if (!ShouldSimulate(player) || player.IsFrozen)
        {
            return;
        }

        player.BeginPhysicsStep(dt, _level);
        player.HandleInputState(input, _level);
        player.RefreshGroundedState(_level);
        player.ApplyMovementForces(input);
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
        player.ClampVelocity();
    }

    private void SolveConstraints(float dt)
    {
        _ = dt;
        // Future rope constraints will apply forces and impulses here.
    }

    private void MoveAndSolveCollisions(Player player, float dt)
    {
        if (!ShouldSimulate(player) || player.IsFrozen)
        {
            return;
        }

        player.IntegratePosition(new Vector2(player.Velocity.X * dt, 0f));
        ResolveHorizontalCollisions(player);

        player.IntegratePosition(new Vector2(0f, player.Velocity.Y * dt));
        player.IsGrounded = false;
        ResolveVerticalCollisions(player);

        player.FinishPhysicsStep(_level);
    }

    private void ApplyLaunchPads(Player player)
    {
        if (!ShouldSimulate(player) || player.IsFrozen || IsLaunchPadCoolingDown(player))
        {
            return;
        }

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

            Vector2 launchVelocity = (direction * LaunchPad.LaunchPadForce) + (lateralVelocity * 0.25f);
            player.LaunchFromPad(launchVelocity);
            _launchPadCooldowns[player.NetworkId] = MathF.Max(0.01f, LaunchPad.LaunchPadCooldown);
            LastLaunchForce = direction * LaunchPad.LaunchPadForce;
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
        foreach (Platform platform in _level.GetCollidablePlatforms(player.CurrentColor))
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
    }

    private void ResolveVerticalCollisions(Player player)
    {
        foreach (Platform platform in _level.GetCollidablePlatforms(player.CurrentColor))
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
