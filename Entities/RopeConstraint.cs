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
        float slackStiffness,
        float tenseStiffness,
        float slackStretchTolerance,
        float tenseStretchRange,
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
        if (error <= 0f)
        {
            return;
        }

        float allowedError = RestLength * MathHelper.Clamp(slackStretchTolerance, 0f, 0.65f);
        if (error <= allowedError)
        {
            return;
        }

        float solvedError = error - allowedError;
        CurrentTension = solvedError / RestLength;

        float tenseRange = MathF.Max(0.001f, tenseStretchRange);
        float tenseAmount = MathHelper.Clamp(CurrentTension / tenseRange, 0f, 1f);
        tenseAmount = tenseAmount * tenseAmount * (3f - (2f * tenseAmount));
        float stiffness = MathHelper.Lerp(
            MathHelper.Clamp(slackStiffness, 0f, 1f),
            MathHelper.Clamp(tenseStiffness, 0f, 1f),
            tenseAmount);

        Vector2 correction = (delta / distance) * solvedError * stiffness;
        float maxSafeCorrection = MathF.Max(0.01f, maxCorrection);
        if (correction.LengthSquared() > maxSafeCorrection * maxSafeCorrection)
        {
            correction = Vector2.Normalize(correction) * maxSafeCorrection;
        }

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
