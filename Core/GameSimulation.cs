#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Game1_Monogame;

public sealed class GameSimulation
{
    private const float MaxFrameTime = 0.25f;
    private const int MaxTicksPerFrame = 5;
    private const int InputBufferRetentionTicks = 180;

    private readonly GameSession _session;
    private readonly NetworkInputBuffer _inputBuffer = new();
    private float _fixedTimeAccumulator;

    public GameSimulation(GameSession session, Level level, PlayerManager playerManager)
    {
        _session = session;
        Level = level;
        PlayerManager = playerManager;
        TickRate = new TickRate(session.Settings.SimulationTicksPerSecond);
        PhysicsWorld = new PhysicsWorld(level, playerManager.Players, session, session.RopeGameplayMode);
        TimerRunning = true;
        _session.State = GameSessionState.Playing;
        LastSnapshot = CreateSnapshot(SimulationTick.Zero);
    }

    public Level Level { get; }
    public PlayerManager PlayerManager { get; }
    public PhysicsWorld PhysicsWorld { get; }
    public TickRate TickRate { get; }
    public SimulationTick CurrentTick { get; private set; } = SimulationTick.Zero;
    public NetworkInputBuffer InputBuffer => _inputBuffer;
    public IReadOnlyList<Player> Players => PlayerManager.Players;
    public IReadOnlyList<Rope> Ropes => PhysicsWorld.Ropes;
    public float ElapsedTime { get; private set; }
    public bool TimerRunning { get; private set; }
    public bool IsLevelComplete { get; private set; }
    public float FinalTime { get; private set; }
    public bool NewRecord { get; private set; }
    public int SnapshotCount { get; private set; }
    public GameSnapshot LastSnapshot { get; private set; }

    public int Advance(float frameSeconds, ILocalPlayerInputSource localInputSource)
    {
        if (frameSeconds <= 0f)
        {
            return 0;
        }

        _fixedTimeAccumulator += MathF.Min(frameSeconds, MaxFrameTime);
        int steps = 0;
        float fixedDelta = TickRate.FixedDeltaSeconds;

        while (_fixedTimeAccumulator >= fixedDelta && steps < MaxTicksPerFrame)
        {
            _fixedTimeAccumulator -= fixedDelta;
            StepFixedTick(localInputSource);
            steps++;

            if (IsLevelComplete)
            {
                _fixedTimeAccumulator = 0f;
                break;
            }
        }

        if (steps >= MaxTicksPerFrame)
        {
            _fixedTimeAccumulator = 0f;
        }

        return steps;
    }

    public void ApplySnapshot(GameSnapshot snapshot)
    {
        foreach (PlayerSnapshot playerSnapshot in snapshot.Players)
        {
            Player? player = FindPlayer(playerSnapshot.NetworkId);
            player?.ApplySnapshot(playerSnapshot);
        }

        foreach (RopeSnapshot ropeSnapshot in snapshot.Ropes)
        {
            Rope? rope = FindRope(ropeSnapshot.NetworkId);
            rope?.ApplySnapshot(ropeSnapshot);
        }

        ElapsedTime = snapshot.Timer.ElapsedTime;
        TimerRunning = snapshot.Timer.IsRunning;
        IsLevelComplete = snapshot.Timer.IsComplete;
        FinalTime = snapshot.Timer.FinalTime;
        NewRecord = snapshot.Timer.NewRecord;
        LastSnapshot = snapshot;
        SnapshotCount = Math.Max(SnapshotCount, snapshot.Sequence);
        CurrentTick = new SimulationTick(Math.Max(CurrentTick.Value, snapshot.Tick));
    }

    private void StepFixedTick(ILocalPlayerInputSource localInputSource)
    {
        SimulationTick tick = CurrentTick;
        InputFrame localFrame = CaptureLocalInputFrame(tick, localInputSource);
        _inputBuffer.StoreFrame(localFrame);

        if (!IsLevelComplete)
        {
            PhysicsWorld.UpdatePhysics(TickRate.FixedDeltaSeconds, _inputBuffer.GetInputs(tick));

            if (TimerRunning)
            {
                ElapsedTime += TickRate.FixedDeltaSeconds;
            }

            if (IsAnyPlayerTouchingGoal())
            {
                CompleteLevel();
            }
        }

        CurrentTick = CurrentTick.Next();
        LastSnapshot = CreateSnapshot(tick);
        SnapshotCount++;
        _inputBuffer.TrimBefore(CurrentTick, InputBufferRetentionTicks);
    }

    private InputFrame CaptureLocalInputFrame(SimulationTick tick, ILocalPlayerInputSource localInputSource)
    {
        InputFrame frame = new(tick, _session.LocalOwnerId);
        foreach (Player player in Players)
        {
            if (!player.IsLocal)
            {
                continue;
            }

            frame.AddPlayerInput(player.NetworkId, localInputSource.GetPlayerInput(player.PlayerId));
        }

        return frame;
    }

    private bool IsAnyPlayerTouchingGoal()
    {
        foreach (Player player in Players)
        {
            Rectangle playerBounds = player.Bounds;
            foreach (Goal goal in Level.Goals)
            {
                if (playerBounds.Intersects(goal.TriggerBounds))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void CompleteLevel()
    {
        if (IsLevelComplete)
        {
            return;
        }

        TimerRunning = false;
        IsLevelComplete = true;
        FinalTime = BestTimeStorage.RoundToCentiseconds(ElapsedTime);
        ElapsedTime = FinalTime;
        NewRecord = BestTimeStorage.SaveIfRecord(_session.SelectedLevelId, FinalTime);
        _session.State = GameSessionState.Completed;

        foreach (Player player in Players)
        {
            player.Freeze();
        }
    }

    private GameSnapshot CreateSnapshot(SimulationTick tick)
    {
        GameSnapshot snapshot = new()
        {
            Tick = tick.Value,
            Sequence = SnapshotCount + 1,
            RopeMode = _session.RopeGameplayMode,
            Level = CreateLevelSnapshot(),
            Timer = new TimerSnapshot(
                ElapsedTime,
                TimerRunning,
                IsLevelComplete,
                FinalTime,
                NewRecord)
        };

        foreach (Player player in Players)
        {
            snapshot.Players.Add(player.CreateSnapshot());
        }

        foreach (Rope rope in Ropes)
        {
            snapshot.Ropes.Add(rope.CreateSnapshot());
        }

        return snapshot;
    }

    private LevelSnapshot CreateLevelSnapshot()
    {
        LevelSnapshot snapshot = new()
        {
            LevelId = _session.SelectedLevelId,
            Name = Level.Name,
            PlayerSpawn = NetworkVector2.FromVector2(Level.PlayerStart),
            WorldWidth = Level.WorldSize.X,
            WorldHeight = Level.WorldSize.Y
        };

        foreach (Platform platform in Level.Platforms)
        {
            snapshot.Platforms.Add(new PlatformSnapshot(
                platform.Bounds.X,
                platform.Bounds.Y,
                platform.Bounds.Width,
                platform.Bounds.Height,
                platform.PlatformColor));
        }

        foreach (Goal goal in Level.Goals)
        {
            snapshot.Goals.Add(new GoalSnapshot(goal.Position.X, goal.Position.Y));
        }

        return snapshot;
    }

    private Player? FindPlayer(int networkId)
    {
        foreach (Player player in Players)
        {
            if (player.NetworkId == networkId)
            {
                return player;
            }
        }

        return null;
    }

    private Rope? FindRope(int networkId)
    {
        foreach (Rope rope in Ropes)
        {
            if (rope.NetworkId == networkId)
            {
                return rope;
            }
        }

        return null;
    }
}
