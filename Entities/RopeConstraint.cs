using System;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public sealed class RopeConstraint
{
    public RopeConstraint(RopeNode a, RopeNode b, float restLength)
    {
        A = a;
        B = b;
        RestLength = MathF.Max(0.01f, restLength);
    }

    public RopeNode A { get; }
    public RopeNode B { get; }
    public float RestLength { get; set; }
    public float CurrentTension { get; private set; }

    /// <summary>
    /// Distance constraint: resist stretch strongly, allow soft compression (sag).
    /// </summary>
    public void Solve(
        float stiffness,
        float maxCorrection,
        out Vector2 pinnedACorrection,
        out Vector2 pinnedBCorrection)
    {
        pinnedACorrection = Vector2.Zero;
        pinnedBCorrection = Vector2.Zero;
        CurrentTension = 0f;

        Vector2 delta = B.Position - A.Position;
        float distance = delta.Length();
        if (distance <= 0.0001f)
        {
            return;
        }

        float error = distance - RestLength;
        // Only fight stretch. Compression = slack/sag, leave alone.
        if (error <= 0f)
        {
            return;
        }

        CurrentTension = error / RestLength;
        float clampedStiffness = MathHelper.Clamp(stiffness, 0f, 1f);
        Vector2 correction = (delta / distance) * error * clampedStiffness;
        float maxSafe = MathF.Max(0.01f, maxCorrection);
        if (correction.LengthSquared() > maxSafe * maxSafe)
        {
            correction = Vector2.Normalize(correction) * maxSafe;
        }

        float aWeight = A.IsPinned ? 0f : 1f;
        float bWeight = B.IsPinned ? 0f : 1f;
        float totalWeight = aWeight + bWeight;
        if (totalWeight <= 0f)
        {
            pinnedACorrection += correction * 0.5f;
            pinnedBCorrection -= correction * 0.5f;
            return;
        }

        if (!A.IsPinned)
        {
            A.Position += correction * (aWeight / totalWeight);
        }
        else
        {
            pinnedACorrection += correction;
        }

        if (!B.IsPinned)
        {
            B.Position -= correction * (bWeight / totalWeight);
        }
        else
        {
            pinnedBCorrection -= correction;
        }
    }
}
