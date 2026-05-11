using System;
using Microsoft.Xna.Framework;

namespace Game1_Monogame;

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
    public float RestLength { get; }
    public float CurrentTension { get; private set; }

    public void Solve(
        float stiffness,
        float elasticity,
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
        float allowedError = RestLength * MathHelper.Clamp(elasticity, 0f, 0.4f);
        if (MathF.Abs(error) <= allowedError)
        {
            return;
        }

        float solvedError = error > 0f
            ? error - allowedError
            : error + allowedError;
        Vector2 correction = (delta / distance) * solvedError * MathHelper.Clamp(stiffness, 0f, 1f);
        float maxSafeCorrection = MathF.Max(0.01f, maxCorrection);
        if (correction.LengthSquared() > maxSafeCorrection * maxSafeCorrection)
        {
            correction = Vector2.Normalize(correction) * maxSafeCorrection;
        }

        CurrentTension = MathF.Abs(solvedError) / RestLength;

        float aWeight = A.IsPinned ? 0f : 1f;
        float bWeight = B.IsPinned ? 0f : 1f;
        float totalWeight = aWeight + bWeight;

        if (totalWeight > 0f)
        {
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

            return;
        }

        pinnedACorrection += correction * 0.5f;
        pinnedBCorrection -= correction * 0.5f;
    }
}
