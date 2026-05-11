using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class Rope
{
    private const int DefaultNodeCount = 24;
    private const float MinimumRopeLength = 280f;
    private const float MaximumInitialSag = 190f;
    private const float EndpointImpulseScale = 0.16f;
    private const float MaxEndpointVelocityDelta = 420f;
    private const float MaxNodeVelocity = 950f;
    private const float NodeCollisionRadius = 5f;
    private const float NodeCollisionSoftness = 0.72f;
    private const float MaxNodeCollisionCorrection = 9f;
    private const float NodeCollisionDamping = 0.42f;
    private const float MaxConstraintCorrection = 14f;

    private readonly IReadOnlyList<Player> _colorPlayers;
    private Vector2 _startPinnedCorrection;
    private Vector2 _endPinnedCorrection;
    private RopeColorState _colorState;

    public Rope(Player startPlayer, Player endPlayer, IReadOnlyList<Player> colorPlayers, int nodeCount = DefaultNodeCount)
    {
        StartPlayer = startPlayer;
        EndPlayer = endPlayer;
        _colorPlayers = colorPlayers;
        GenerateNodes(Math.Max(2, nodeCount));
        RefreshColorState();
    }

    public List<RopeNode> Nodes { get; } = new();
    public List<RopeConstraint> Constraints { get; } = new();
    public Player StartPlayer { get; }
    public Player EndPlayer { get; }
    public float RopeStiffness { get; set; } = 0.9f;
    public float RopeElasticity { get; set; } = 0.035f;
    public float VerletDamping { get; set; } = 0.992f;
    public int SolverIterations { get; set; } = 16;
    public float LastTension { get; private set; }
    public int LastCollisionCount { get; private set; }

    public void Simulate(float dt, float gravity, IEnumerable<Platform> platforms)
    {
        if (dt <= 0f || Nodes.Count < 2)
        {
            return;
        }

        RefreshColorState();
        List<Platform> collidablePlatforms = GetCollidablePlatforms(platforms);

        _startPinnedCorrection = Vector2.Zero;
        _endPinnedCorrection = Vector2.Zero;
        LastTension = 0f;
        LastCollisionCount = 0;

        SyncPinnedNodesToPlayers();
        ClearNodeCollisionState();
        IntegrateNodes(dt, gravity);
        ResolveNodeCollisions(collidablePlatforms);

        int iterations = Math.Clamp(SolverIterations, 1, 32);
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            SyncPinnedNodesToPlayers();
            foreach (RopeConstraint constraint in Constraints)
            {
                constraint.Solve(
                    RopeStiffness,
                    RopeElasticity,
                    MaxConstraintCorrection,
                    out Vector2 pinnedACorrection,
                    out Vector2 pinnedBCorrection);

                AccumulatePinnedCorrection(constraint.A, pinnedACorrection);
                AccumulatePinnedCorrection(constraint.B, pinnedBCorrection);
                LastTension = MathF.Max(LastTension, constraint.CurrentTension);
            }

            ResolveNodeCollisions(collidablePlatforms);
        }

        StabilizeNodes();
        SyncPinnedNodesToPlayers();
        ApplyEndpointImpulses(dt);
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        if (Nodes.Count < 2)
        {
            return;
        }

        Color ropeColor = _colorState.XnaColor;
        for (int i = 0; i < Constraints.Count; i++)
        {
            RopeConstraint constraint = Constraints[i];
            Vector2 start = constraint.A.Position;
            Vector2 end = constraint.B.Position;
            Vector2 midpoint = (start + end) * 0.5f;
            Color segmentColor = debugDraw
                ? Color.Lerp(ropeColor, Color.Red, MathHelper.Clamp(constraint.CurrentTension * 2.2f, 0f, 1f))
                : ropeColor;

            DrawLine(spriteBatch, pixel, start, midpoint, segmentColor, debugDraw ? 4 : 6);
            DrawLine(spriteBatch, pixel, midpoint, end, segmentColor, debugDraw ? 4 : 6);
        }

        if (!debugDraw)
        {
            return;
        }

        foreach (RopeNode node in Nodes)
        {
            int size = node.IsPinned ? 8 : 6;
            Rectangle bounds = new(
                (int)MathF.Round(node.Position.X) - (size / 2),
                (int)MathF.Round(node.Position.Y) - (size / 2),
                size,
                size);
            Color fill = node.IsPinned ? Color.White : node.IsColliding ? new Color(255, 168, 64) : Color.Yellow;
            spriteBatch.Draw(pixel, bounds, fill);
            DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 1);
        }

        Vector2 labelPosition = GetMidpoint() + new Vector2(8f, -30f);
        DrawDebugText(spriteBatch, pixel, $"NODES {Nodes.Count}", labelPosition, Color.White);
        DrawDebugText(spriteBatch, pixel, $"LINKS {Constraints.Count}", labelPosition + new Vector2(0f, 10f), Color.White);
        DrawDebugText(spriteBatch, pixel, $"TENSION {(int)MathF.Round(LastTension * 100f)}", labelPosition + new Vector2(0f, 20f), Color.White);
        DrawDebugText(spriteBatch, pixel, $"COLOR {_colorState.Name}", labelPosition + new Vector2(0f, 30f), _colorState.XnaColor);
        DrawDebugText(spriteBatch, pixel, $"HITS {LastCollisionCount}", labelPosition + new Vector2(0f, 40f), Color.White);
    }

    private void GenerateNodes(int nodeCount)
    {
        Nodes.Clear();
        Constraints.Clear();

        Vector2 start = GetPlayerAnchor(StartPlayer);
        Vector2 end = GetPlayerAnchor(EndPlayer);
        float distance = Vector2.Distance(start, end);
        float targetLength = MathF.Max(distance, MinimumRopeLength);
        float sag = distance < targetLength
            ? MathF.Min(MaximumInitialSag, MathF.Sqrt(MathF.Max(0f, (targetLength * targetLength * 0.25f) - (distance * distance * 0.25f))))
            : 0f;

        for (int i = 0; i < nodeCount; i++)
        {
            float t = nodeCount <= 1 ? 0f : i / (float)(nodeCount - 1);
            Vector2 position = Vector2.Lerp(start, end, t) + new Vector2(0f, MathF.Sin(t * MathF.PI) * sag);
            Nodes.Add(new RopeNode(position, i == 0 || i == nodeCount - 1));
        }

        for (int i = 0; i < Nodes.Count - 1; i++)
        {
            float restLength = Vector2.Distance(Nodes[i].Position, Nodes[i + 1].Position);
            Constraints.Add(new RopeConstraint(Nodes[i], Nodes[i + 1], restLength));
        }
    }

    private void RefreshColorState()
    {
        bool hasRed = false;
        bool hasGreen = false;
        bool hasBlue = false;

        foreach (Player player in _colorPlayers)
        {
            switch (player.PlayerColor)
            {
                case GameColor.Red:
                    hasRed = true;
                    break;
                case GameColor.Green:
                    hasGreen = true;
                    break;
                case GameColor.Blue:
                    hasBlue = true;
                    break;
            }
        }

        _colorState = RopeColorState.Create(hasRed, hasGreen, hasBlue);
    }

    private List<Platform> GetCollidablePlatforms(IEnumerable<Platform> platforms)
    {
        List<Platform> collidablePlatforms = new();
        foreach (Platform platform in platforms)
        {
            if (CanCollideWith(platform.PlatformColor))
            {
                collidablePlatforms.Add(platform);
            }
        }

        return collidablePlatforms;
    }

    private bool CanCollideWith(GameColor platformColor)
    {
        return platformColor switch
        {
            GameColor.Red => _colorState.HasRed,
            GameColor.Green => _colorState.HasGreen,
            GameColor.Blue => _colorState.HasBlue,
            _ => false
        };
    }

    private void SyncPinnedNodesToPlayers()
    {
        SyncPinnedNode(Nodes[0], GetPlayerAnchor(StartPlayer));
        SyncPinnedNode(Nodes[^1], GetPlayerAnchor(EndPlayer));
    }

    private static void SyncPinnedNode(RopeNode node, Vector2 targetPosition)
    {
        node.PreviousPosition = node.Position;
        node.Position = targetPosition;
    }

    private void ClearNodeCollisionState()
    {
        foreach (RopeNode node in Nodes)
        {
            node.IsColliding = false;
            node.LastCollisionNormal = Vector2.Zero;
        }
    }

    private void IntegrateNodes(float dt, float gravity)
    {
        Vector2 gravityStep = new(0f, gravity * dt * dt);
        foreach (RopeNode node in Nodes)
        {
            if (node.IsPinned)
            {
                continue;
            }

            Vector2 velocity = node.Position - node.PreviousPosition;
            if (!IsFinite(velocity) || velocity.LengthSquared() > MaxNodeVelocity * MaxNodeVelocity)
            {
                velocity = velocity == Vector2.Zero || !IsFinite(velocity)
                    ? Vector2.Zero
                    : Vector2.Normalize(velocity) * MaxNodeVelocity;
            }

            velocity *= VerletDamping;
            node.PreviousPosition = node.Position;
            node.Position += velocity + gravityStep;
        }
    }

    private void ResolveNodeCollisions(IReadOnlyList<Platform> platforms)
    {
        if (platforms.Count == 0)
        {
            return;
        }

        Vector2 nodeSize = new(NodeCollisionRadius * 2f);
        foreach (RopeNode node in Nodes)
        {
            if (node.IsPinned)
            {
                continue;
            }

            Vector2 nodePosition = node.Position - new Vector2(NodeCollisionRadius);
            foreach (Platform platform in platforms)
            {
                if (!CollisionHelper.TryGetMinimumTranslationVector(
                    nodePosition,
                    nodeSize,
                    platform.Bounds,
                    out Vector2 escape,
                    out Vector2 escapeDirection,
                    out _))
                {
                    continue;
                }

                Vector2 correction = escape * NodeCollisionSoftness;
                float correctionLength = correction.Length();
                if (correctionLength > MaxNodeCollisionCorrection)
                {
                    correction = Vector2.Normalize(correction) * MaxNodeCollisionCorrection;
                }

                node.Position += correction;
                node.IsColliding = true;
                node.LastCollisionNormal = escapeDirection;
                LastCollisionCount++;
                StabilizeCollisionVelocity(node, escapeDirection);
                nodePosition = node.Position - new Vector2(NodeCollisionRadius);
            }
        }
    }

    private static void StabilizeCollisionVelocity(RopeNode node, Vector2 normal)
    {
        if (normal == Vector2.Zero)
        {
            return;
        }

        Vector2 velocity = node.Position - node.PreviousPosition;
        float normalVelocity = Vector2.Dot(velocity, normal);
        if (normalVelocity >= 0f)
        {
            return;
        }

        Vector2 tangentVelocity = velocity - (normal * normalVelocity);
        Vector2 stabilizedVelocity = tangentVelocity + (normal * normalVelocity * NodeCollisionDamping);
        node.PreviousPosition = node.Position - stabilizedVelocity;
    }

    private void StabilizeNodes()
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            RopeNode node = Nodes[i];
            if (IsFinite(node.Position) && IsFinite(node.PreviousPosition))
            {
                continue;
            }

            float t = Nodes.Count <= 1 ? 0f : i / (float)(Nodes.Count - 1);
            node.Position = Vector2.Lerp(GetPlayerAnchor(StartPlayer), GetPlayerAnchor(EndPlayer), t);
            node.PreviousPosition = node.Position;
            node.IsColliding = false;
            node.LastCollisionNormal = Vector2.Zero;
        }
    }

    private void AccumulatePinnedCorrection(RopeNode node, Vector2 correction)
    {
        if (correction == Vector2.Zero)
        {
            return;
        }

        if (node == Nodes[0])
        {
            _startPinnedCorrection += correction;
        }
        else if (node == Nodes[^1])
        {
            _endPinnedCorrection += correction;
        }
    }

    private void ApplyEndpointImpulses(float dt)
    {
        ApplyEndpointImpulse(StartPlayer, _startPinnedCorrection, dt);
        ApplyEndpointImpulse(EndPlayer, _endPinnedCorrection, dt);
    }

    private static void ApplyEndpointImpulse(Player player, Vector2 correction, float dt)
    {
        if (correction == Vector2.Zero || dt <= 0f)
        {
            return;
        }

        Vector2 velocityDelta = correction / dt * EndpointImpulseScale;
        if (!IsFinite(velocityDelta))
        {
            return;
        }

        if (velocityDelta.LengthSquared() > MaxEndpointVelocityDelta * MaxEndpointVelocityDelta)
        {
            velocityDelta = Vector2.Normalize(velocityDelta) * MaxEndpointVelocityDelta;
        }

        player.AddImpulse(velocityDelta * player.Mass);
    }

    private static Vector2 GetPlayerAnchor(Player player)
    {
        return player.Position + (player.Size * 0.5f);
    }

    private Vector2 GetMidpoint()
    {
        return Nodes.Count == 0
            ? (GetPlayerAnchor(StartPlayer) + GetPlayerAnchor(EndPlayer)) * 0.5f
            : Nodes[Nodes.Count / 2].Position;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
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

    private readonly record struct RopeColorState(
        bool HasRed,
        bool HasGreen,
        bool HasBlue,
        Color XnaColor,
        string Name)
    {
        public static RopeColorState Create(bool hasRed, bool hasGreen, bool hasBlue)
        {
            Color color;
            string name;

            if (hasRed && hasGreen && hasBlue)
            {
                color = Color.White;
                name = "WHITE";
            }
            else if (hasRed && hasGreen)
            {
                color = Color.Yellow;
                name = "YELLOW";
            }
            else if (hasRed && hasBlue)
            {
                color = Color.Magenta;
                name = "MAGENTA";
            }
            else if (hasGreen && hasBlue)
            {
                color = Color.Cyan;
                name = "CYAN";
            }
            else if (hasRed)
            {
                color = GameColor.Red.ToXnaColor();
                name = "RED";
            }
            else if (hasGreen)
            {
                color = GameColor.Green.ToXnaColor();
                name = "GREEN";
            }
            else if (hasBlue)
            {
                color = GameColor.Blue.ToXnaColor();
                name = "BLUE";
            }
            else
            {
                color = Color.White;
                name = "NONE";
            }

            return new RopeColorState(hasRed, hasGreen, hasBlue, color, name);
        }
    }
}
