using System;

namespace ColorBlocks;

/// <summary>
/// Live gameplay tuning values. Shared between physics, ropes, and the developer panel.
/// Defaults match shipped player/rope properties.
/// </summary>
public sealed class GameplayTuning
{
    public static GameplayTuning Active { get; } = new();

    public float PlayerMass { get; set; } = 1f;
    public float GroundFriction { get; set; } = 16f;
    public float AirAcceleration { get; set; } = 1050f;
    public float GroundAcceleration { get; set; } = 2600f;
    public float JumpImpulse { get; set; } = 560f;
    public float LaunchForceMultiplier { get; set; } = 1f;

    public float RopeRestLength { get; set; } = 280f;
    public float MinimumRopeLength { get; set; } = 90f;
    public float MaximumRopeLength { get; set; } = 280f;
    public float SlackDistance { get; set; } = 48f;
    public float RopeStiffness { get; set; } = 0.62f;
    public float RopeDamping { get; set; } = 0.94f;
    public int ConstraintIterations { get; set; } = 8;
    public float NodeMass { get; set; } = 0f;
    public float PullShorteningSpeed { get; set; } = 280f;
    public float PullRecoverySpeed { get; set; } = 140f;
    public float MaxRopeForce { get; set; } = 3800f;
    public float MaxPullForce { get; set; } = 3200f;
    public float ProgressiveTensionCurve { get; set; } = 2.1f;
    public float MaxCorrectionPerFrame { get; set; } = 7.5f;
    public int NodeCount { get; set; } = 24;

    public void ApplyTo(Player player)
    {
        player.Mass = PlayerMass;
        player.GroundFriction = GroundFriction;
        player.AirAcceleration = AirAcceleration;
        player.GroundAcceleration = GroundAcceleration;
        player.JumpImpulse = JumpImpulse;
    }

    public void ApplyTo(Rope rope)
    {
        rope.RopeStiffness = RopeStiffness;
        rope.VerletDamping = RopeDamping;
        rope.SolverIterations = Math.Clamp(ConstraintIterations, 1, 32);
        rope.NodeMass = NodeMass;
        rope.PullShorteningSpeed = PullShorteningSpeed;
        rope.PullRecoverySpeed = PullRecoverySpeed;
        rope.MaxRopeForce = MaxRopeForce;
        rope.MaxPullForce = MaxPullForce;
        rope.ProgressiveTensionCurve = ProgressiveTensionCurve;
        rope.MaxCorrectionPerFrame = MaxCorrectionPerFrame;
        rope.SlackDistance = SlackDistance;
        rope.MinimumRopeLength = MinimumRopeLength;
        rope.MaximumRopeLength = MaximumRopeLength;
        rope.BaseRestLength = RopeRestLength;
        rope.SyncRestLengthFromTuning();
        rope.EnsureNodeCount(
            rope.GameplayMode == RopeGameplayMode.ColoredPhysics
                ? Math.Max(NodeCount, Rope.ColoredPhysicsMinNodeCount)
                : NodeCount);
    }
}
