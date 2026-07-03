using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class Rope : INetworkEntity
{
    private const int DefaultNodeCount = 24;
    private const float MinimumRopeLength = 280f;
    private const float MaximumInitialSag = 190f;
    private const float EndpointImpulseScale = 0.055f;
    private const float EndpointImpulseTensionStart = 0.055f;
    private const float EndpointImpulseTensionFull = 0.22f;
    private const float MaxEndpointVelocityDelta = 190f;
    private const float MaxNodeVelocity = 950f;
    private const float NodeCollisionRadius = 5f;
    private const float NodeCollisionSoftness = 0.72f;
    private const float MaxNodeCollisionCorrection = 9f;
    private const float NodeCollisionDamping = 0.42f;
    private const float MaxConstraintCorrection = 10f;
    private const float SlackStiffness = 0.16f;
    private const float TenseStretchRange = 0.14f;
    private const float TenseStateThreshold = 0.08f;
    private const int MaxPullNodeRadius = 8;
    private const float PullingEndpointImpulseScale = 0.72f;
    private readonly IReadOnlyList<Player> _colorPlayers;
    private Vector2 _startPinnedCorrection;
    private Vector2 _endPinnedCorrection;
    private RopeColorState _colorState;
    private bool _startPlayerPulling;
    private bool _endPlayerPulling;

    public Rope(Player startPlayer, Player endPlayer, IReadOnlyList<Player> colorPlayers, int nodeCount = DefaultNodeCount)
        : this(startPlayer, endPlayer, colorPlayers, RopeGameplayMode.ColoredPhysics, NetworkEntityOwnership.LocalHost(0), nodeCount)
    {
    }

    public Rope(
        Player startPlayer,
        Player endPlayer,
        IReadOnlyList<Player> colorPlayers,
        RopeGameplayMode gameplayMode,
        int nodeCount = DefaultNodeCount)
        : this(startPlayer, endPlayer, colorPlayers, gameplayMode, NetworkEntityOwnership.LocalHost(0), nodeCount)
    {
    }

    public Rope(
        Player startPlayer,
        Player endPlayer,
        IReadOnlyList<Player> colorPlayers,
        RopeGameplayMode gameplayMode,
        NetworkEntityOwnership ownership,
        int nodeCount = DefaultNodeCount)
    {
        StartPlayer = startPlayer;
        EndPlayer = endPlayer;
        _colorPlayers = colorPlayers;
        GameplayMode = gameplayMode;
        ConfigureNetworkOwnership(ownership);
        GenerateNodes(Math.Max(2, nodeCount));
        RefreshGameplayModeState();
    }

    public int NetworkId { get; private set; }
    public int OwnerId { get; private set; }
    public bool IsLocal { get; private set; }
    public bool IsRemote => !IsLocal;
    public bool IsHostControlled { get; private set; }
    public List<RopeNode> Nodes { get; } = new();
    public List<RopeConstraint> Constraints { get; } = new();
    public Player StartPlayer { get; }
    public Player EndPlayer { get; }
    public RopeGameplayMode GameplayMode { get; }
    public float RopeStiffness { get; set; } = 0.86f;
    public float RopeElasticity { get; set; } = 0.075f;
    public float VerletDamping { get; set; } = 0.998f;
    public int SolverIterations { get; set; } = 10;
    public float LastTension { get; private set; }
    public int LastCollisionCount { get; private set; }
    public bool IsTense => LastTension >= TenseStateThreshold;
    public float PullForceStrength { get; set; } = 24f;
    public int PullNodeRadius { get; set; } = 4;
    public float PullFalloff { get; set; } = 0.7f;
    public float PullDampingReduction { get; set; } = 1f;
    public float LastPullIntensity { get; private set; }
    public int LastPulledNodeCount { get; private set; }

    public void ConfigureNetworkOwnership(NetworkEntityOwnership ownership)
    {
        NetworkId = ownership.NetworkId;
        OwnerId = ownership.OwnerId;
        IsLocal = ownership.IsLocal;
        IsHostControlled = ownership.IsHostControlled;
    }

    public RopeSnapshot CreateSnapshot()
    {
        RopeSnapshot snapshot = new()
        {
            NetworkId = NetworkId,
            OwnerId = OwnerId,
            StartPlayerNetworkId = StartPlayer.NetworkId,
            EndPlayerNetworkId = EndPlayer.NetworkId,
            RopeMode = GameplayMode,
            Tension = LastTension,
            IsTense = IsTense,
            PullIntensity = LastPullIntensity,
            PulledNodeCount = LastPulledNodeCount
        };

        foreach (RopeNode node in Nodes)
        {
            snapshot.NodePositions.Add(NetworkVector2.FromVector2(node.Position));
        }

        return snapshot;
    }

    public void ApplySnapshot(RopeSnapshot snapshot)
    {
        ConfigureNetworkOwnership(new NetworkEntityOwnership(
            snapshot.NetworkId,
            snapshot.OwnerId,
            IsLocal,
            IsHostControlled));

        if (snapshot.NodePositions.Count != Nodes.Count)
        {
            RebuildNodesFromSnapshot(snapshot.NodePositions);
        }
        else
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                Vector2 position = snapshot.NodePositions[i].ToVector2();
                Nodes[i].PreviousPosition = position;
                Nodes[i].Position = position;
            }
        }

        LastTension = snapshot.Tension;
        LastPullIntensity = snapshot.PullIntensity;
        LastPulledNodeCount = snapshot.PulledNodeCount;
    }

    public void ResetBetweenPlayers()
    {
        int nodeCount = Math.Max(2, Nodes.Count);
        GenerateNodes(nodeCount);
        _startPinnedCorrection = Vector2.Zero;
        _endPinnedCorrection = Vector2.Zero;
        LastTension = 0f;
        LastCollisionCount = 0;
        LastPullIntensity = 0f;
        LastPulledNodeCount = 0;
    }

    public void Simulate(
        float dt,
        float gravity,
        IEnumerable<Platform> platforms,
        bool startPlayerPulling,
        bool endPlayerPulling)
    {
        if (!IsHostControlled || dt <= 0f || Nodes.Count < 2)
        {
            return;
        }

        RefreshGameplayModeState();
        List<Platform> collidablePlatforms = GetCollidablePlatforms(platforms);

        _startPinnedCorrection = Vector2.Zero;
        _endPinnedCorrection = Vector2.Zero;
        LastTension = 0f;
        LastCollisionCount = 0;
        LastPullIntensity = 0f;
        LastPulledNodeCount = 0;
        _startPlayerPulling = startPlayerPulling;
        _endPlayerPulling = endPlayerPulling;

        SyncPinnedNodesToPlayers();
        ClearNodeCollisionState();
        ClearPullDebugState();
        ApplyPullForces(dt, startPlayerPulling, endPlayerPulling);
        IntegrateNodes(dt, gravity);
        ResolveNodeCollisions(collidablePlatforms);

        int iterations = Math.Clamp(SolverIterations, 1, 32);
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            SyncPinnedNodesToPlayers();
            foreach (RopeConstraint constraint in Constraints)
            {
                constraint.Solve(
                    SlackStiffness,
                    RopeStiffness,
                    RopeElasticity,
                    TenseStretchRange,
                    MaxConstraintCorrection,
                    out Vector2 pinnedACorrection,
                    out Vector2 pinnedBCorrection);

                AccumulatePinnedCorrection(constraint.A, pinnedACorrection, constraint.CurrentTension);
                AccumulatePinnedCorrection(constraint.B, pinnedBCorrection, constraint.CurrentTension);
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
            Color segmentColor = GameplayMode == RopeGameplayMode.Neutral
                ? ropeColor
                : debugDraw
                    ? Color.Lerp(new Color(104, 210, 255), Color.Red, GetTenseAmount(constraint.CurrentTension))
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
            Color fill;
            if (node.IsPinned)
            {
                fill = Color.White;
            }
            else if (node.PullWeight > 0.001f)
            {
                fill = Color.Lerp(Color.Yellow, Color.LimeGreen, MathHelper.Clamp(node.PullWeight, 0f, 1f));
            }
            else
            {
                fill = node.IsColliding ? new Color(255, 168, 64) : Color.Yellow;
            }
            spriteBatch.Draw(pixel, bounds, fill);
            DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 1);
        }

        Vector2 labelPosition = GetMidpoint() + new Vector2(8f, -30f);
        DrawDebugText(spriteBatch, pixel, $"NODES {Nodes.Count}", labelPosition, Color.White);
        DrawDebugText(spriteBatch, pixel, $"LINKS {Constraints.Count}", labelPosition + new Vector2(0f, 10f), Color.White);
        DrawDebugText(spriteBatch, pixel, $"TENSION {(int)MathF.Round(LastTension * 100f)}", labelPosition + new Vector2(0f, 20f), Color.White);
        DrawDebugText(spriteBatch, pixel, IsTense ? "STATE TENSE" : "STATE SLACK", labelPosition + new Vector2(0f, 30f), IsTense ? Color.Red : Color.Cyan);
        DrawDebugText(spriteBatch, pixel, $"PULL {(int)MathF.Round(LastPullIntensity * 100f)} N{LastPulledNodeCount}", labelPosition + new Vector2(0f, 40f), Color.LimeGreen);
        DrawDebugText(spriteBatch, pixel, $"COLOR {_colorState.Name}", labelPosition + new Vector2(0f, 50f), _colorState.XnaColor);
        DrawDebugText(spriteBatch, pixel, $"HITS {LastCollisionCount}", labelPosition + new Vector2(0f, 60f), Color.White);

        DrawPullDebug(spriteBatch, pixel);
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

    private void RebuildNodesFromSnapshot(IReadOnlyList<NetworkVector2> nodePositions)
    {
        Nodes.Clear();
        Constraints.Clear();

        for (int i = 0; i < nodePositions.Count; i++)
        {
            Nodes.Add(new RopeNode(nodePositions[i].ToVector2(), i == 0 || i == nodePositions.Count - 1));
        }

        for (int i = 0; i < Nodes.Count - 1; i++)
        {
            float restLength = Vector2.Distance(Nodes[i].Position, Nodes[i + 1].Position);
            Constraints.Add(new RopeConstraint(Nodes[i], Nodes[i + 1], restLength));
        }
    }

    private void RefreshGameplayModeState()
    {
        if (GameplayMode == RopeGameplayMode.Neutral)
        {
            _colorState = RopeColorState.CreateNeutral();
            return;
        }

        RefreshColorState();
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
        if (GameplayMode == RopeGameplayMode.Neutral)
        {
            return collidablePlatforms;
        }

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

    private void ClearPullDebugState()
    {
        foreach (RopeNode node in Nodes)
        {
            node.PullVelocityDelta = Vector2.Zero;
            node.PullWeight = 0f;
            node.LastPullForce = Vector2.Zero;
        }
    }

    private void ApplyPullForces(float dt, bool startPlayerPulling, bool endPlayerPulling)
    {
        if (startPlayerPulling)
        {
            ApplyPullForce(StartPlayer, dt);
        }

        if (endPlayerPulling)
        {
            ApplyPullForce(EndPlayer, dt);
        }
    }

    private void ApplyPullForce(Player player, float dt)
    {
        float pullStrength = MathF.Max(0f, PullForceStrength);
        if (pullStrength <= 0.01f)
        {
            return;
        }

        int closestIndex = FindClosestFreeNode(GetPlayerAnchor(player));
        if (closestIndex < 0)
        {
            return;
        }

        int radius = Math.Clamp(PullNodeRadius, 1, MaxPullNodeRadius);
        float falloff = MathHelper.Clamp(PullFalloff, 0f, 1f);
        Vector2 target = GetPlayerAnchor(player);

        for (int offset = -radius; offset <= radius; offset++)
        {
            int nodeIndex = closestIndex + offset;
            if (nodeIndex <= 0 || nodeIndex >= Nodes.Count - 1)
            {
                continue;
            }

            RopeNode node = Nodes[nodeIndex];
            float weight = offset == 0 ? 1f : MathF.Pow(falloff, MathF.Abs(offset));
            if (weight <= 0.0001f)
            {
                continue;
            }

            Vector2 toPlayer = target - node.Position;
            float distance = toPlayer.Length();
            if (distance <= 0.001f)
            {
                continue;
            }

            float step = MathF.Min(pullStrength * weight, distance);
            Vector2 velocityDelta = (toPlayer / distance) * step;

            node.PullVelocityDelta += velocityDelta;
            float maxTotalDelta = pullStrength * 1.6f;
            if (node.PullVelocityDelta.LengthSquared() > maxTotalDelta * maxTotalDelta)
            {
                node.PullVelocityDelta = Vector2.Normalize(node.PullVelocityDelta) * maxTotalDelta;
            }

            bool wasPulled = node.PullWeight > 0.0001f;
            node.PullWeight = MathF.Max(node.PullWeight, weight);
            node.LastPullForce += velocityDelta;
            LastPullIntensity = MathF.Max(LastPullIntensity, node.PullWeight);
            if (!wasPulled)
            {
                LastPulledNodeCount++;
            }
        }
    }

    private int FindClosestFreeNode(Vector2 target)
    {
        int closestIndex = -1;
        float closestDistanceSquared = float.MaxValue;

        for (int i = 1; i < Nodes.Count - 1; i++)
        {
            float distanceSquared = Vector2.DistanceSquared(Nodes[i].Position, target);
            if (distanceSquared >= closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestIndex = i;
        }

        return closestIndex;
    }

    private void IntegrateNodes(float dt, float gravity)
    {
        Vector2 gravityStep = new(0f, gravity * dt * dt);
        float pullDampingReduction = MathHelper.Clamp(PullDampingReduction, 0f, 1f);
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

            float pullWeight = MathHelper.Clamp(node.PullWeight, 0f, 1f);
            float damping = MathHelper.Lerp(VerletDamping, 1f, pullDampingReduction * pullWeight);
            velocity *= damping;
            velocity += node.PullVelocityDelta;

            node.PreviousPosition = node.Position;
            node.Position += velocity + gravityStep;
            node.PullVelocityDelta = Vector2.Zero;
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

    private void AccumulatePinnedCorrection(RopeNode node, Vector2 correction, float tension)
    {
        if (correction == Vector2.Zero)
        {
            return;
        }

        float transfer = GetEndpointImpulseTransfer(tension);
        if (transfer <= 0f)
        {
            return;
        }

        correction *= transfer;

        if (node == Nodes[0])
        {
            if (_startPlayerPulling)
            {
                correction *= PullingEndpointImpulseScale;
            }

            _startPinnedCorrection += correction;
        }
        else if (node == Nodes[^1])
        {
            if (_endPlayerPulling)
            {
                correction *= PullingEndpointImpulseScale;
            }

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

    private static float GetEndpointImpulseTransfer(float tension)
    {
        if (tension <= EndpointImpulseTensionStart)
        {
            return 0f;
        }

        float range = MathF.Max(0.001f, EndpointImpulseTensionFull - EndpointImpulseTensionStart);
        float amount = MathHelper.Clamp((tension - EndpointImpulseTensionStart) / range, 0f, 1f);
        return amount * amount * (3f - (2f * amount));
    }

    private static float GetTenseAmount(float tension)
    {
        return MathHelper.Clamp(tension / TenseStateThreshold, 0f, 1f);
    }

    private void DrawPullDebug(SpriteBatch spriteBatch, Texture2D pixel)
    {
        foreach (RopeNode node in Nodes)
        {
            if (node.LastPullForce == Vector2.Zero)
            {
                continue;
            }

            Vector2 vector = node.LastPullForce * 2.2f;
            if (vector.LengthSquared() > 70f * 70f)
            {
                vector = Vector2.Normalize(vector) * 70f;
            }

            DrawLine(spriteBatch, pixel, node.Position, node.Position + vector, Color.LimeGreen, 3);
        }
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
        string Name,
        bool IsNeutral)
    {
        public Color XnaColor => IsNeutral
            ? ColorPaletteManager.Get(ColorType.Rope)
            : ColorPaletteManager.MixFlags(HasRed, HasGreen, HasBlue);

        public static RopeColorState Create(bool hasRed, bool hasGreen, bool hasBlue)
        {
            string name = GetMixName(hasRed, hasGreen, hasBlue);
            return new RopeColorState(hasRed, hasGreen, hasBlue, name, IsNeutral: false);
        }

        private static string GetMixName(bool hasRed, bool hasGreen, bool hasBlue)
        {
            if (hasRed && hasGreen && hasBlue)
            {
                return "WHITE";
            }

            if (hasRed && hasGreen)
            {
                return "YELLOW";
            }

            if (hasRed && hasBlue)
            {
                return "MAGENTA";
            }

            if (hasGreen && hasBlue)
            {
                return "CYAN";
            }

            if (hasRed)
            {
                return "RED";
            }

            if (hasGreen)
            {
                return "GREEN";
            }

            if (hasBlue)
            {
                return "BLUE";
            }

            return "NONE";
        }

        public static RopeColorState CreateNeutral()
        {
            return new RopeColorState(false, false, false, "NEUTRAL", IsNeutral: true);
        }
    }
}
