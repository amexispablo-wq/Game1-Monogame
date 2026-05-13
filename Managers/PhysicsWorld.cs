using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class PhysicsWorld
{
    public const float FixedTimeStep = 1f / 60f;
    public const float Gravity = 1600f;

    private readonly Level _level;

    public PhysicsWorld(Level level, List<Player> players)
    {
        _level = level;
        Players = players;

        for (int i = 0; i < Players.Count - 1; i++)
        {
            Ropes.Add(new Rope(Players[i], Players[i + 1], Players));
        }
    }

    public List<Player> Players { get; }
    public List<Rope> Ropes { get; } = new();

    public void UpdatePhysics(float dt, InputManager input)
    {
        if (dt <= 0f)
        {
            return;
        }

        foreach (Player player in Players)
        {
            PrepareBody(player, input.GetActionState(player.PlayerId), dt);
        }

        foreach (Player player in Players)
        {
            IntegrateBodyForces(player, dt);
        }

        SolveConstraints(dt);

        foreach (Player player in Players)
        {
            MoveAndSolveCollisions(player, dt);
        }

        foreach (Rope rope in Ropes)
        {
            rope.Simulate(
                dt,
                Gravity,
                _level.Platforms,
                input.GetActionState(rope.StartPlayer.PlayerId).PullRopeHeld,
                input.GetActionState(rope.EndPlayer.PlayerId).PullRopeHeld);
        }

        foreach (Player player in Players)
        {
            player.ClampVelocity();
        }
    }

    public void DrawRopes(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        foreach (Rope rope in Ropes)
        {
            rope.Draw(spriteBatch, pixel, debugDraw);
        }
    }

    private void PrepareBody(Player player, InputActionState input, float dt)
    {
        if (player.IsFrozen)
        {
            return;
        }

        player.BeginPhysicsStep(dt, _level);
        player.HandleInputState(input, _level);
        player.RefreshGroundedState(_level);
        player.ApplyMovementForces(input);
        player.ApplyJumpImpulse(input);
        player.ApplyGravity(Gravity, input);
    }

    private void IntegrateBodyForces(Player player, float dt)
    {
        if (player.IsFrozen)
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
        if (player.IsFrozen)
        {
            return;
        }

        player.IntegratePosition(new Vector2(player.Velocity.X * dt, 0f));
        ResolveHorizontalCollisions(player);

        player.IntegratePosition(new Vector2(0f, player.Velocity.Y * dt));
        player.IsGrounded = false;
        ResolveVerticalCollisions(player);

        player.FinishPhysicsStep();
    }

    private void ResolveHorizontalCollisions(Player player)
    {
        foreach (Platform platform in _level.GetCollidablePlatforms(player.CurrentColor))
        {
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
}
