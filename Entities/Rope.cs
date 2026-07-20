using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>
/// Simple verlet rope between two players.
/// Stretch-only distance constraints, optional color-platform collision, pull shortens rest length.
/// </summary>
public sealed class Rope : INetworkEntity
{
    private const int DefaultNodeCount = 24;
    public const int ColoredPhysicsMinNodeCount = 40;
    private const float DefaultBaseRestLength = 280f;
    private const float NodeRadius = 5f;
    private const float MaxNodeSpeed = 900f;
    private const float CollisionPushCap = 16f;
    private const float GravityScale = 0.9f;
    private static readonly Vector2 GravityBias = new(0f, 1f);

    private readonly IReadOnlyList<Player> _colorPlayers;
    private readonly List<Platform> _collidableScratch = new();
    private float _baseRestLength;
    private float _targetRestLength;
    private float _currentRestLength;
    private int _configuredNodeCount;
    private RopeColorState _colorState;
    private float _feedbackPhase;
    private Vector2 _startPinnedCorrection;
    private Vector2 _endPinnedCorrection;

    public Rope(Player startPlayer, Player endPlayer, IReadOnlyList<Player> colorPlayers, int nodeCount = DefaultNodeCount)
        : this(startPlayer, endPlayer, colorPlayers, RopeGameplayMode.ColoredPhysics, NetworkEntityOwnership.LocalHost(0), nodeCount)
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
        _configuredNodeCount = Math.Max(2, nodeCount);
        BaseRestLength = DefaultBaseRestLength;
        ConfigureNetworkOwnership(ownership);
        GenerateNodes(_configuredNodeCount);
        RefreshGameplayModeState();
        GameplayTuning.Active.ApplyTo(this);
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

    public float BaseRestLength { get; set; } = DefaultBaseRestLength;
    public float MinimumRopeLength { get; set; } = 120f;
    public float MaximumRopeLength { get; set; } = 280f;
    public float SlackDistance { get; set; } = 48f;
    public float RopeStiffness { get; set; } = 0.85f;
    public float RopeElasticity { get; set; }
    public float VerletDamping { get; set; } = 0.96f;
    public int SolverIterations { get; set; } = 8;
    public float NodeMass { get; set; }
    public float PullShorteningSpeed { get; set; } = 200f;
    public float PullRecoverySpeed { get; set; } = 140f;
    public float MaxRopeForce { get; set; } = 3800f;
    public float MaxPullForce { get; set; } = 2400f;
    public float ProgressiveTensionCurve { get; set; } = 2.1f;
    public float MaxCorrectionPerFrame { get; set; } = 10f;
    public float PullForceStrength { get; set; } = 24f;
    public int PullNodeRadius { get; set; } = 4;
    public float PullFalloff { get; set; } = 0.7f;
    public float PullDampingReduction { get; set; } = 1f;

    public float LastTension { get; private set; }
    public int LastCollisionCount { get; private set; }
    public bool IsTense => TensionPhase != RopeTensionPhase.Slack;
    public float LastPullIntensity { get; private set; }
    public int LastPulledNodeCount { get; private set; }
    public float CurrentPathLength { get; private set; }
    public float TargetRestLength => _targetRestLength;
    public float CurrentRestLength => _currentRestLength;
    public float SlackAmount { get; private set; }
    public float LastEndpointForce { get; private set; }
    public float FeedbackIntensity { get; private set; }
    public RopeTensionPhase TensionPhase { get; private set; } = RopeTensionPhase.Slack;
    public bool IsPulling { get; private set; }

    public void ConfigureNetworkOwnership(NetworkEntityOwnership ownership)
    {
        NetworkId = ownership.NetworkId;
        OwnerId = ownership.OwnerId;
        IsLocal = ownership.IsLocal;
        IsHostControlled = ownership.IsHostControlled;
    }

    public void EnsureNodeCount(int nodeCount)
    {
        int clamped = Math.Clamp(nodeCount, 2, 64);
        if (clamped == Nodes.Count && Constraints.Count == Math.Max(0, clamped - 1))
        {
            return;
        }

        _configuredNodeCount = clamped;
        GenerateNodes(clamped);
    }

    public void SyncRestLengthFromTuning()
    {
        _baseRestLength = MathF.Max(BaseRestLength, MinimumRopeLength);
        MaximumRopeLength = MathF.Max(MaximumRopeLength, _baseRestLength);
        _targetRestLength = MathF.Min(_targetRestLength <= 0f ? _baseRestLength : _targetRestLength, _baseRestLength);
        _currentRestLength = MathF.Min(_currentRestLength <= 0f ? _baseRestLength : _currentRestLength, _baseRestLength);
        DistributeSegmentRestLengths(_currentRestLength);
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

        if (snapshot.NodePositions.Count >= 2)
        {
            RebuildNodesFromSnapshot(snapshot.NodePositions);
        }

        LastTension = snapshot.Tension;
        LastPullIntensity = snapshot.PullIntensity;
        LastPulledNodeCount = snapshot.PulledNodeCount;
        RefreshGameplayModeState();
        UpdateDerivedMetrics(GetPlayerAnchor(StartPlayer), GetPlayerAnchor(EndPlayer));
    }

    public void RefreshVisualState()
    {
        RefreshGameplayModeState();
    }

    public void ResetBetweenPlayers()
    {
        GenerateNodes(_configuredNodeCount);
        LastTension = 0f;
        LastCollisionCount = 0;
        LastPullIntensity = 0f;
        LastPulledNodeCount = 0;
        TensionPhase = RopeTensionPhase.Slack;
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
        _collidableScratch.Clear();
        if (GameplayMode == RopeGameplayMode.ColoredPhysics)
        {
            foreach (Platform platform in platforms)
            {
                if (CanCollideWith(platform.PlatformColor))
                {
                    _collidableScratch.Add(platform);
                }
            }
        }

        _startPinnedCorrection = Vector2.Zero;
        _endPinnedCorrection = Vector2.Zero;
        LastTension = 0f;
        LastCollisionCount = 0;
        LastPullIntensity = 0f;
        LastPulledNodeCount = 0;
        LastEndpointForce = 0f;
        IsPulling = startPlayerPulling || endPlayerPulling;

        Vector2 startAnchor = GetPlayerAnchor(StartPlayer);
        Vector2 endAnchor = GetPlayerAnchor(EndPlayer);

        UpdateTargetRestLength(dt, startPlayerPulling, endPlayerPulling);
        SmoothCurrentRestLength(dt);
        DistributeSegmentRestLengths(_currentRestLength);

        SyncPinnedNodes(startAnchor, endAnchor);
        ClearNodeFlags();

        Integrate(dt, gravity);

        int iterations = Math.Clamp(SolverIterations, 1, 16);
        bool colored = _collidableScratch.Count > 0;
        for (int i = 0; i < iterations; i++)
        {
            SyncPinnedNodes(startAnchor, endAnchor);
            SolveConstraints();
            if (colored)
            {
                ResolveCollisions(_collidableScratch);
            }
        }

        // Pull only shortens rest length above. No direct node→player gather.
        // Shorter rests → constraints contract rope → endpoint coupling moves other player.

        if (colored)
        {
            for (int pass = 0; pass < 4; pass++)
            {
                ResolveCollisions(_collidableScratch);
            }

            SyncPinnedNodes(startAnchor, endAnchor);
            ResolveCollisions(_collidableScratch);
        }

        // Hard inextensible cap: path may not exceed current rest length.
        EnforceMaximumPathLength(startAnchor, endAnchor, colored ? _collidableScratch : null);
        if (colored)
        {
            ResolveCollisions(_collidableScratch);
            SyncPinnedNodes(startAnchor, endAnchor);
            EnforceMaximumPathLength(startAnchor, endAnchor, _collidableScratch);
        }

        SyncPinnedNodes(startAnchor, endAnchor);
        StabilizeFiniteNodes(startAnchor, endAnchor);
        UpdateDerivedMetrics(startAnchor, endAnchor);
        ApplyEndpointCoupling(startAnchor, endAnchor, dt);
        UpdateFeedback(dt);

        if (IsPulling)
        {
            LastPullIntensity = MathHelper.Clamp(
                (_baseRestLength - _targetRestLength) / MathF.Max(1f, _baseRestLength - MinimumRopeLength),
                0f,
                1f);
            LastPulledNodeCount = Math.Max(0, Nodes.Count - 2);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        Vector2 startAnchor = GetPlayerAnchor(StartPlayer);
        Vector2 endAnchor = GetPlayerAnchor(EndPlayer);
        if (Nodes.Count >= 2)
        {
            Nodes[0].Position = startAnchor;
            Nodes[^1].Position = endAnchor;
        }

        UpdateDerivedMetrics(startAnchor, endAnchor);

        Color ropeColor = GetVisibleRopeColor();
        float tensionVisual = MathHelper.Clamp(FeedbackIntensity, 0f, 1f);
        int thickness = 8 + (int)MathF.Round(tensionVisual * 3f);
        Color feedbackColor = ropeColor;

        if (Constraints.Count == 0)
        {
            DrawLine(spriteBatch, pixel, startAnchor, endAnchor, Color.Black * 0.8f, thickness + 2);
            DrawLine(spriteBatch, pixel, startAnchor, endAnchor, feedbackColor, thickness);
        }

        foreach (RopeConstraint constraint in Constraints)
        {
            DrawLine(spriteBatch, pixel, constraint.A.Position, constraint.B.Position, Color.Black * 0.8f, thickness + 2);
            DrawLine(spriteBatch, pixel, constraint.A.Position, constraint.B.Position, feedbackColor, thickness);
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

        Vector2 label = GetMidpoint() + new Vector2(8f, -30f);
        DrawDebugText(spriteBatch, pixel, $"NODES {Nodes.Count}", label, Color.White);
        DrawDebugText(spriteBatch, pixel, $"TARGET {(int)MathF.Round(_targetRestLength)}", label + new Vector2(0f, 10f), Color.White);
        DrawDebugText(spriteBatch, pixel, $"TENSION {(int)MathF.Round(LastTension * 100f)}", label + new Vector2(0f, 20f), Color.White);
        DrawDebugText(spriteBatch, pixel, TensionPhase.ToString().ToUpperInvariant(), label + new Vector2(0f, 30f), IsTense ? Color.Red : Color.Cyan);
        DrawDebugText(spriteBatch, pixel, $"PULL {(int)MathF.Round(LastPullIntensity * 100f)}", label + new Vector2(0f, 40f), Color.LimeGreen);
        DrawDebugText(spriteBatch, pixel, $"COLOR {_colorState.Name}", label + new Vector2(0f, 50f), _colorState.XnaColor);
        DrawDebugText(spriteBatch, pixel, $"HITS {LastCollisionCount}", label + new Vector2(0f, 60f), Color.White);
    }

    private void Integrate(float dt, float gravity)
    {
        float gScale = NodeMass > 0.0001f ? 1f : (_collidableScratch.Count > 0 ? GravityScale : 0.35f);
        Vector2 gravityStep = new(0f, gravity * gScale * dt * dt);
        float damping = MathHelper.Clamp(VerletDamping, 0.8f, 0.999f);
        if (IsPulling)
        {
            damping = MathHelper.Clamp(damping * 0.9f, 0.75f, 0.98f);
        }

        foreach (RopeNode node in Nodes)
        {
            if (node.IsPinned)
            {
                continue;
            }

            Vector2 velocity = node.Position - node.PreviousPosition;
            if (!IsFinite(velocity))
            {
                velocity = Vector2.Zero;
            }
            else if (velocity.LengthSquared() > MaxNodeSpeed * MaxNodeSpeed * dt * dt)
            {
                velocity = Vector2.Normalize(velocity) * MaxNodeSpeed * dt;
            }

            velocity *= damping;
            node.PreviousPosition = node.Position;
            node.Position += velocity + gravityStep;
        }
    }

    private void SolveConstraints()
    {
        float maxLen = MathF.Min(_currentRestLength, MaximumRopeLength);
        float pathLen = GetPathLength();
        bool overstretched = pathLen > maxLen + 0.5f;
        // Near-inextensible when over max — soft only while slack/sagging.
        float stiffness = overstretched
            ? 1f
            : MathHelper.Clamp(RopeStiffness, 0.2f, 1f);
        float maxCorrection = overstretched
            ? MathF.Max(MaxCorrectionPerFrame * 2.5f, 24f)
            : MathF.Max(1f, MaxCorrectionPerFrame);

        foreach (RopeConstraint constraint in Constraints)
        {
            constraint.Solve(
                stiffness,
                maxCorrection,
                out Vector2 pinnedA,
                out Vector2 pinnedB);
            AccumulatePinned(constraint.A, pinnedA);
            AccumulatePinned(constraint.B, pinnedB);
            LastTension = MathF.Max(LastTension, MathHelper.Clamp(constraint.CurrentTension, 0f, 1f));
        }
    }

    private void ResolveCollisions(IReadOnlyList<Platform> platforms)
    {
        ResolveNodeCollisions(platforms);
        ResolveSegmentCollisions(platforms);
    }

    private void ResolveNodeCollisions(IReadOnlyList<Platform> platforms)
    {
        foreach (RopeNode node in Nodes)
        {
            if (node.IsPinned)
            {
                continue;
            }

            Vector2 center = node.Position;
            foreach (Platform platform in platforms)
            {
                if (!CollisionHelper.TrySnapCircleToRectanglePerimeter(
                    ref center,
                    NodeRadius,
                    platform.Bounds,
                    GravityBias,
                    out Vector2 normal))
                {
                    continue;
                }

                Vector2 push = center - node.Position;
                float len = push.Length();
                if (len > CollisionPushCap)
                {
                    center = node.Position + ((push / len) * CollisionPushCap);
                }

                node.Position = center;
                MarkCollision(node, normal);
            }
        }
    }

    private void ResolveSegmentCollisions(IReadOnlyList<Platform> platforms)
    {
        foreach (RopeConstraint constraint in Constraints)
        {
            RopeNode a = constraint.A;
            RopeNode b = constraint.B;
            Vector2 start = a.Position;
            Vector2 end = b.Position;
            float segmentLength = Vector2.Distance(start, end);
            int samples = CollisionHelper.GetSegmentCollisionSampleCount(segmentLength, NodeRadius);

            foreach (Platform platform in platforms)
            {
                if (TryEjectThroughCut(start, end, platform.Bounds, out Vector2 cutPush, out Vector2 cutNormal))
                {
                    if (a.IsPinned != b.IsPinned)
                    {
                        // One end is player: route free node around nearest exterior corner.
                        RopeNode free = a.IsPinned ? b : a;
                        Vector2 pinnedPos = a.IsPinned ? a.Position : b.Position;
                        free.Position = RouteAroundRectangle(pinnedPos, free.Position, platform.Bounds, NodeRadius);
                        MarkCollision(free, cutNormal);
                    }
                    else
                    {
                        float len = cutPush.Length();
                        if (len > CollisionPushCap * 2.5f)
                        {
                            cutPush = (cutPush / len) * (CollisionPushCap * 2.5f);
                        }

                        if (!a.IsPinned)
                        {
                            a.Position += cutPush;
                            MarkCollision(a, cutNormal);
                        }

                        if (!b.IsPinned)
                        {
                            b.Position += cutPush;
                            MarkCollision(b, cutNormal);
                        }
                    }

                    start = a.Position;
                    end = b.Position;
                    continue;
                }

                float bestT = -1f;
                Vector2 bestPush = Vector2.Zero;
                Vector2 bestNormal = Vector2.Zero;
                float bestDepth = 0f;

                for (int s = 1; s < samples; s++)
                {
                    float t = s / (float)samples;
                    Vector2 point = Vector2.Lerp(start, end, t);
                    Vector2 pushed = point;
                    if (!CollisionHelper.TrySnapCircleToRectanglePerimeter(
                        ref pushed,
                        NodeRadius,
                        platform.Bounds,
                        GravityBias,
                        out Vector2 normal))
                    {
                        continue;
                    }

                    float depth = (pushed - point).LengthSquared();
                    if (depth <= 0.0001f || (bestT >= 0f && depth <= bestDepth))
                    {
                        continue;
                    }

                    bestT = t;
                    bestDepth = depth;
                    bestPush = pushed - point;
                    bestNormal = normal;
                }

                if (bestT < 0f)
                {
                    continue;
                }

                ApplySegmentPush(a, b, bestT, bestPush, bestNormal, CollisionPushCap);
                start = a.Position;
                end = b.Position;
            }
        }
    }

    private static bool TryEjectThroughCut(
        Vector2 start,
        Vector2 end,
        Rectangle rect,
        out Vector2 push,
        out Vector2 normal)
    {
        push = Vector2.Zero;
        normal = Vector2.Zero;

        if (!CollisionHelper.SegmentIntersectsExpandedRectangle(start, end, rect, NodeRadius))
        {
            return false;
        }

        float pad = NodeRadius;
        bool startAbove = start.Y < rect.Top - pad * 0.2f;
        bool startBelow = start.Y > rect.Bottom + pad * 0.2f;
        bool endAbove = end.Y < rect.Top - pad * 0.2f;
        bool endBelow = end.Y > rect.Bottom + pad * 0.2f;
        bool verticalCut = (startAbove && endBelow) || (startBelow && endAbove);

        bool startLeft = start.X < rect.Left - pad * 0.2f;
        bool startRight = start.X > rect.Right + pad * 0.2f;
        bool endLeft = end.X < rect.Left - pad * 0.2f;
        bool endRight = end.X > rect.Right + pad * 0.2f;
        bool horizontalCut = (startLeft && endRight) || (startRight && endLeft);

        Vector2 mid = (start + end) * 0.5f;
        float absDx = MathF.Abs(end.X - start.X);
        float absDy = MathF.Abs(end.Y - start.Y);
        bool midInside = mid.X > rect.Left - pad
            && mid.X < rect.Right + pad
            && mid.Y > rect.Top - pad
            && mid.Y < rect.Bottom + pad;

        if (!verticalCut && midInside && absDy >= absDx * 0.6f)
        {
            verticalCut = true;
        }

        if (!horizontalCut && midInside && absDx >= absDy * 0.6f && !verticalCut)
        {
            horizontalCut = true;
        }

        if (!verticalCut && !horizontalCut)
        {
            return false;
        }

        if (verticalCut)
        {
            if (mid.X - rect.Left <= rect.Right - mid.X)
            {
                push = new Vector2((rect.Left - pad) - mid.X, 0f);
                normal = new Vector2(-1f, 0f);
            }
            else
            {
                push = new Vector2((rect.Right + pad) - mid.X, 0f);
                normal = new Vector2(1f, 0f);
            }
        }
        else if (mid.Y - rect.Top <= rect.Bottom - mid.Y)
        {
            push = new Vector2(0f, (rect.Top - pad) - mid.Y);
            normal = new Vector2(0f, -1f);
        }
        else
        {
            push = new Vector2(0f, (rect.Bottom + pad) - mid.Y);
            normal = new Vector2(0f, 1f);
        }

        return push.LengthSquared() > 0.01f;
    }

    private static Vector2 RouteAroundRectangle(Vector2 fromPinned, Vector2 free, Rectangle rect, float pad)
    {
        // Keep free node on the same exterior side as the pinned anchor first,
        // otherwise pinned(top)→free(bottom-corner) still chords through the solid.
        bool pinnedAbove = fromPinned.Y <= rect.Top + pad;
        bool pinnedBelow = fromPinned.Y >= rect.Bottom - pad;
        bool pinnedLeft = fromPinned.X <= rect.Left + pad;
        bool pinnedRight = fromPinned.X >= rect.Right - pad;

        Vector2[] candidates;
        if (pinnedAbove)
        {
            candidates = new[]
            {
                new Vector2(rect.Left - pad, rect.Top - pad),
                new Vector2(rect.Right + pad, rect.Top - pad)
            };
        }
        else if (pinnedBelow)
        {
            candidates = new[]
            {
                new Vector2(rect.Left - pad, rect.Bottom + pad),
                new Vector2(rect.Right + pad, rect.Bottom + pad)
            };
        }
        else if (pinnedLeft)
        {
            candidates = new[]
            {
                new Vector2(rect.Left - pad, rect.Top - pad),
                new Vector2(rect.Left - pad, rect.Bottom + pad)
            };
        }
        else if (pinnedRight)
        {
            candidates = new[]
            {
                new Vector2(rect.Right + pad, rect.Top - pad),
                new Vector2(rect.Right + pad, rect.Bottom + pad)
            };
        }
        else
        {
            candidates = new[]
            {
                new Vector2(rect.Left - pad, rect.Top - pad),
                new Vector2(rect.Right + pad, rect.Top - pad),
                new Vector2(rect.Left - pad, rect.Bottom + pad),
                new Vector2(rect.Right + pad, rect.Bottom + pad)
            };
        }

        Vector2 best = candidates[0];
        float bestScore = float.MaxValue;
        foreach (Vector2 corner in candidates)
        {
            float score = Vector2.DistanceSquared(fromPinned, corner) + (Vector2.DistanceSquared(corner, free) * 0.35f);
            if (score < bestScore)
            {
                bestScore = score;
                best = corner;
            }
        }

        return best;
    }

    private void ApplySegmentPush(RopeNode a, RopeNode b, float t, Vector2 push, Vector2 normal, float pushCap)
    {
        float len = push.Length();
        if (len > pushCap)
        {
            push = (push / len) * pushCap;
        }

        float weightA = a.IsPinned ? 0f : (1f - t);
        float weightB = b.IsPinned ? 0f : t;
        float sum = weightA + weightB;
        if (sum <= 0.0001f)
        {
            return;
        }

        weightA /= sum;
        weightB /= sum;

        if (!a.IsPinned)
        {
            a.Position += push * weightA;
            MarkCollision(a, normal);
        }

        if (!b.IsPinned)
        {
            b.Position += push * weightB;
            MarkCollision(b, normal);
        }
    }

    private void EnforceMaximumPathLength(
        Vector2 startAnchor,
        Vector2 endAnchor,
        IReadOnlyList<Platform>? platforms)
    {
        float maxLen = MathF.Min(_currentRestLength, MaximumRopeLength);
        if (maxLen <= 0f || Nodes.Count < 2)
        {
            return;
        }

        for (int pass = 0; pass < 3; pass++)
        {
            float pathLen = GetPathLength();
            if (pathLen <= maxLen + 0.25f)
            {
                break;
            }

            float scale = maxLen / MathF.Max(pathLen, 0.01f);
            SyncPinnedNodes(startAnchor, endAnchor);
            for (int i = 1; i < Nodes.Count; i++)
            {
                RopeNode prev = Nodes[i - 1];
                RopeNode cur = Nodes[i];
                if (cur.IsPinned)
                {
                    SyncPinnedNodes(startAnchor, endAnchor);
                    continue;
                }

                Vector2 delta = cur.Position - prev.Position;
                float dist = delta.Length();
                if (dist <= 0.0001f)
                {
                    continue;
                }

                cur.Position = prev.Position + ((delta / dist) * (dist * scale));
            }

            SyncPinnedNodes(startAnchor, endAnchor);
            for (int i = Nodes.Count - 2; i >= 0; i--)
            {
                RopeNode next = Nodes[i + 1];
                RopeNode cur = Nodes[i];
                if (cur.IsPinned)
                {
                    continue;
                }

                Vector2 delta = cur.Position - next.Position;
                float dist = delta.Length();
                if (dist <= 0.0001f)
                {
                    continue;
                }

                cur.Position = next.Position + ((delta / dist) * (dist * scale));
            }

            SyncPinnedNodes(startAnchor, endAnchor);
            if (platforms != null && platforms.Count > 0)
            {
                ResolveCollisions(platforms);
                SyncPinnedNodes(startAnchor, endAnchor);
            }
        }
    }

    private void ApplyEndpointCoupling(Vector2 startAnchor, Vector2 endAnchor, float dt)
    {
        float maxLen = MathF.Min(_currentRestLength, MaximumRopeLength);
        float pathLen = GetPathLength();
        float endpointDistance = Vector2.Distance(startAnchor, endAnchor);
        float pathOverstretch = pathLen - maxLen;
        float endpointOverstretch = endpointDistance - maxLen;
        float overstretch = MathF.Max(pathOverstretch, endpointOverstretch);

        UpdateTensionPhase(
            pathOverstretch > 0f ? maxLen + pathOverstretch : endpointDistance,
            maxLen);

        if (overstretch <= 0f)
        {
            return;
        }

        Vector2 startVel = StartPlayer.Velocity;
        Vector2 endVel = EndPlayer.Velocity;
        bool coMoving =
            MathF.Abs(startVel.X) > 40f
            && MathF.Abs(endVel.X) > 40f
            && MathF.Sign(startVel.X) == MathF.Sign(endVel.X)
            && MathF.Abs(startVel.X - endVel.X) <= 80f;
        if (coMoving && endpointOverstretch <= 8f)
        {
            // Same-direction walk: don't fight players with rope tangents.
            return;
        }

        // Pull along rope tangents (into the chain), not chord — wrap still couples players.
        Vector2 startTangent = Nodes.Count > 1
            ? Nodes[1].Position - startAnchor
            : endAnchor - startAnchor;
        Vector2 endTangent = Nodes.Count > 1
            ? Nodes[^2].Position - endAnchor
            : startAnchor - endAnchor;

        if (startTangent.LengthSquared() <= 0.0001f || endTangent.LengthSquared() <= 0.0001f)
        {
            return;
        }

        startTangent = Vector2.Normalize(startTangent);
        endTangent = Vector2.Normalize(endTangent);

        float tension = MathHelper.Clamp(overstretch / MathF.Max(1f, maxLen), 0.2f, 1f);
        float force = MathF.Min(overstretch * MaxRopeForce * tension / MathF.Max(20f, maxLen), MaxRopeForce);
        LastEndpointForce = force;
        LastTension = MathF.Max(LastTension, tension);

        float impulseScale = force * dt;
        Vector2 startImpulse = startTangent * impulseScale * StartPlayer.Mass;
        Vector2 endImpulse = endTangent * impulseScale * EndPlayer.Mass;

        // Rope mass/self-weight must not kill jumps. Keep player↔player coupling (hang weight),
        // but strip downward impulse while a player is moving upward.
        startImpulse = FilterJumpMitigation(startImpulse, StartPlayer);
        endImpulse = FilterJumpMitigation(endImpulse, EndPlayer);

        StartPlayer.AddImpulse(startImpulse);
        EndPlayer.AddImpulse(endImpulse);

        // Pinned constraint corrections = rope tugging players (consequence of rope).
        if (_startPinnedCorrection != Vector2.Zero)
        {
            Vector2 pinned = FilterJumpMitigation(_startPinnedCorrection * StartPlayer.Mass * 0.5f, StartPlayer);
            StartPlayer.AddImpulse(pinned);
        }

        if (_endPinnedCorrection != Vector2.Zero)
        {
            Vector2 pinned = FilterJumpMitigation(_endPinnedCorrection * EndPlayer.Mass * 0.5f, EndPlayer);
            EndPlayer.AddImpulse(pinned);
        }
    }

    private static Vector2 FilterJumpMitigation(Vector2 impulse, Player player)
    {
        // Up is negative Y. Kill downward (positive Y) rope force while airborne upward.
        if (player.Velocity.Y < -40f && impulse.Y > 0f)
        {
            impulse.Y = 0f;
        }

        return impulse;
    }

    private float EvaluateTension(float endpointDistance, float maxLen)
    {
        float slackStart = MathF.Max(MinimumRopeLength, maxLen - SlackDistance);
        if (endpointDistance <= slackStart)
        {
            return 0f;
        }

        if (endpointDistance <= maxLen)
        {
            float span = MathF.Max(1f, maxLen - slackStart);
            float amount = MathHelper.Clamp((endpointDistance - slackStart) / span, 0f, 1f);
            return MathF.Pow(amount, MathF.Max(0.5f, ProgressiveTensionCurve)) * 0.5f;
        }

        float hard = MathHelper.Clamp((endpointDistance - maxLen) / 24f, 0f, 1f);
        return 0.55f + (hard * 0.45f);
    }

    private void UpdateTensionPhase(float endpointDistance, float maxLen)
    {
        float slackStart = MathF.Max(MinimumRopeLength, maxLen - SlackDistance);
        if (endpointDistance <= slackStart)
        {
            TensionPhase = RopeTensionPhase.Slack;
            FeedbackIntensity = 0f;
            return;
        }

        if (endpointDistance <= maxLen)
        {
            TensionPhase = RopeTensionPhase.SoftTension;
            float span = MathF.Max(1f, maxLen - slackStart);
            FeedbackIntensity = MathHelper.Clamp((endpointDistance - slackStart) / span, 0f, 1f) * 0.55f;
            return;
        }

        TensionPhase = RopeTensionPhase.HardTension;
        FeedbackIntensity = 1f;
        LastTension = MathF.Max(LastTension, MathHelper.Clamp((endpointDistance - maxLen) / MathF.Max(1f, maxLen), 0f, 1f));
    }

    private void UpdateTargetRestLength(float dt, bool startPull, bool endPull)
    {
        if (startPull || endPull)
        {
            float speed = PullShorteningSpeed * ((startPull ? 1f : 0f) + (endPull ? 1f : 0f));
            _targetRestLength = MathF.Max(MinimumRopeLength, _targetRestLength - (speed * dt));
            return;
        }

        _targetRestLength = MathF.Min(_baseRestLength, _targetRestLength + (PullRecoverySpeed * dt));
    }

    private void SmoothCurrentRestLength(float dt)
    {
        float rate = IsPulling ? 12f : 6f;
        _currentRestLength = MathHelper.Lerp(_currentRestLength, _targetRestLength, MathHelper.Clamp(rate * dt, 0f, 1f));
        _currentRestLength = MathHelper.Clamp(_currentRestLength, MinimumRopeLength, MathF.Max(MaximumRopeLength, _baseRestLength));
    }

    private void DistributeSegmentRestLengths(float totalLength)
    {
        if (Constraints.Count == 0)
        {
            return;
        }

        float segment = MathF.Max(0.01f, totalLength / Constraints.Count);
        foreach (RopeConstraint constraint in Constraints)
        {
            constraint.RestLength = segment;
        }
    }

    private void GenerateNodes(int nodeCount)
    {
        Nodes.Clear();
        Constraints.Clear();

        Vector2 start = GetPlayerAnchor(StartPlayer);
        Vector2 end = GetPlayerAnchor(EndPlayer);
        float distance = Vector2.Distance(start, end);
        _baseRestLength = MathF.Max(BaseRestLength, MathF.Max(distance, MinimumRopeLength));
        _targetRestLength = _baseRestLength;
        _currentRestLength = _baseRestLength;
        MaximumRopeLength = MathF.Max(MaximumRopeLength, _baseRestLength);

        for (int i = 0; i < nodeCount; i++)
        {
            float t = nodeCount <= 1 ? 0f : i / (float)(nodeCount - 1);
            Nodes.Add(new RopeNode(Vector2.Lerp(start, end, t), i == 0 || i == nodeCount - 1));
        }

        RebuildConstraints();
        DistributeSegmentRestLengths(_currentRestLength);
    }

    private void RebuildNodesFromSnapshot(IReadOnlyList<NetworkVector2> positions)
    {
        Nodes.Clear();
        Constraints.Clear();
        for (int i = 0; i < positions.Count; i++)
        {
            Nodes.Add(new RopeNode(positions[i].ToVector2(), i == 0 || i == positions.Count - 1));
        }

        RebuildConstraints();
        DistributeSegmentRestLengths(_currentRestLength > 0f ? _currentRestLength : BaseRestLength);
    }

    private void RebuildConstraints()
    {
        Constraints.Clear();
        for (int i = 0; i < Nodes.Count - 1; i++)
        {
            Constraints.Add(new RopeConstraint(Nodes[i], Nodes[i + 1], 1f));
        }
    }

    private void SyncPinnedNodes(Vector2 start, Vector2 end)
    {
        if (Nodes.Count < 2)
        {
            return;
        }

        Nodes[0].PreviousPosition = Nodes[0].Position;
        Nodes[0].Position = start;
        Nodes[^1].PreviousPosition = Nodes[^1].Position;
        Nodes[^1].Position = end;
    }

    private void ClearNodeFlags()
    {
        foreach (RopeNode node in Nodes)
        {
            node.IsColliding = false;
            node.LastCollisionNormal = Vector2.Zero;
            node.PullVelocityDelta = Vector2.Zero;
            node.PullWeight = 0f;
            node.LastPullForce = Vector2.Zero;
        }
    }

    private void MarkCollision(RopeNode node, Vector2 normal)
    {
        if (node.IsPinned)
        {
            return;
        }

        node.IsColliding = true;
        node.LastCollisionNormal = normal;
        LastCollisionCount++;

        if (normal == Vector2.Zero)
        {
            return;
        }

        Vector2 velocity = node.Position - node.PreviousPosition;
        float into = Vector2.Dot(velocity, normal);
        if (into >= 0f)
        {
            return;
        }

        Vector2 tangent = velocity - (normal * into);
        node.PreviousPosition = node.Position - (tangent + (normal * into * 0.35f));
    }

    private void AccumulatePinned(RopeNode node, Vector2 correction)
    {
        if (correction == Vector2.Zero)
        {
            return;
        }

        if (node == Nodes[0])
        {
            _startPinnedCorrection += correction;
        }
        else if (Nodes.Count > 0 && node == Nodes[^1])
        {
            _endPinnedCorrection += correction;
        }
    }

    private void StabilizeFiniteNodes(Vector2 start, Vector2 end)
    {
        for (int i = 0; i < Nodes.Count; i++)
        {
            RopeNode node = Nodes[i];
            if (IsFinite(node.Position) && IsFinite(node.PreviousPosition))
            {
                continue;
            }

            float t = Nodes.Count <= 1 ? 0f : i / (float)(Nodes.Count - 1);
            node.Position = Vector2.Lerp(start, end, t);
            node.PreviousPosition = node.Position;
        }
    }

    private void UpdateDerivedMetrics(Vector2 start, Vector2 end)
    {
        float endpoint = Vector2.Distance(start, end);
        CurrentPathLength = MathF.Max(GetPathLength(), endpoint);
        SlackAmount = MathF.Max(0f, _currentRestLength - endpoint);
    }

    private float GetPathLength()
    {
        float length = 0f;
        for (int i = 1; i < Nodes.Count; i++)
        {
            length += Vector2.Distance(Nodes[i - 1].Position, Nodes[i].Position);
        }

        return length;
    }

    private void UpdateFeedback(float dt)
    {
        if (TensionPhase == RopeTensionPhase.Slack)
        {
            _feedbackPhase = 0f;
            return;
        }

        _feedbackPhase += dt * (8f + (FeedbackIntensity * 10f));
    }

    private void RefreshGameplayModeState()
    {
        if (GameplayMode == RopeGameplayMode.Neutral)
        {
            _colorState = RopeColorState.CreateNeutral();
            return;
        }

        _colorState = RopeColorState.CreateFromEndpointColors(
            StartPlayer.PlayerColor,
            EndPlayer.PlayerColor);
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

    private Color GetVisibleRopeColor()
    {
        if (GameplayMode == RopeGameplayMode.Neutral)
        {
            return ColorPaletteManager.Get(ColorType.Rope);
        }

        return _colorState.XnaColor;
    }

    private Vector2 GetMidpoint()
    {
        return Nodes.Count == 0
            ? (GetPlayerAnchor(StartPlayer) + GetPlayerAnchor(EndPlayer)) * 0.5f
            : Nodes[Nodes.Count / 2].Position;
    }

    private static Vector2 GetPlayerAnchor(Player player)
    {
        return player.Position + (player.Size * 0.5f);
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
        public Color XnaColor =>
            IsNeutral
                ? ColorPaletteManager.Get(ColorType.Rope)
                : ColorPaletteManager.MixFlags(HasRed, HasGreen, HasBlue);

        public static RopeColorState CreateFromEndpointColors(GameColor startColor, GameColor endColor)
        {
            bool hasRed = startColor == GameColor.Red || endColor == GameColor.Red;
            bool hasGreen = startColor == GameColor.Green || endColor == GameColor.Green;
            bool hasBlue = startColor == GameColor.Blue || endColor == GameColor.Blue;
            string name = (hasRed ? "R" : "") + (hasGreen ? "G" : "") + (hasBlue ? "B" : "");
            if (string.IsNullOrEmpty(name))
            {
                name = "NONE";
            }

            return new RopeColorState(hasRed, hasGreen, hasBlue, name, IsNeutral: false);
        }

        public static RopeColorState CreateNeutral()
        {
            return new RopeColorState(false, false, false, "NEUTRAL", IsNeutral: true);
        }
    }
}
