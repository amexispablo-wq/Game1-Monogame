#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks;

public sealed class GameSimulation
{
    private const float MaxFrameTime = 0.25f;
    private const int MaxTicksPerFrame = 5;
    private const int InputBufferRetentionTicks = 180;

    private readonly GameSession _session;
    private readonly NetworkInputBuffer _inputBuffer = new();
    private readonly Dictionary<int, PlayerInputState> _latchedLocalInput = new();
    private float _fixedTimeAccumulator;
    private readonly float _lavaStartSurfaceY;

    public GameSimulation(GameSession session, Level level, PlayerManager playerManager, bool lavaRiseEnabled = false)
    {
        _session = session;
        Level = level;
        PlayerManager = playerManager;
        TickRate = new TickRate(session.Settings.SimulationTicksPerSecond);
        PhysicsWorld = new PhysicsWorld(level, playerManager.Players, session, session.RopeGameplayMode);
        TimerRunning = true;
        _session.State = GameSessionState.Playing;

        LavaActive = level.Lava is not null;
        if (level.Lava is not null)
        {
            _lavaStartSurfaceY = level.Lava.SurfaceY;
            LavaSurfaceY = level.Lava.SurfaceY;
            LavaRiseSpeed = level.Lava.RiseSpeed;
            LavaRiseEnabled = lavaRiseEnabled;
        }

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
    public bool LavaActive { get; }
    public bool LavaRiseEnabled { get; }
    public float LavaRiseSpeed { get; }
    public float LavaSurfaceY { get; private set; }
    public bool IsPlayerDead { get; private set; }
    public bool HasCheckpoint => PlayerManager.HasCheckpoint;
    public bool IsPaused { get; private set; }
    public int SnapshotCount { get; private set; }
    public GameSnapshot LastSnapshot { get; private set; }

    public int Advance(float frameSeconds, ILocalPlayerInputSource localInputSource)
    {
        if (IsPaused)
        {
            return 0;
        }

        // Latch edge-triggered inputs every render frame so presses survive until a
        // fixed tick consumes them. Without this, at high/unlocked FPS most render
        // frames run no tick and one-frame edges (jump, color, respawn) get dropped.
        AccumulateLocalInput(localInputSource);

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
            StepFixedTick();
            steps++;

            if (IsLevelComplete || IsPlayerDead)
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

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
    }

    /// <summary>Pause-menu respawn: checkpoint if available, else spawn. Timer and lava unchanged.</summary>
    public void PauseMenuRespawn()
    {
        if (IsPlayerDead || IsLevelComplete)
        {
            return;
        }

        foreach (Player player in Players)
        {
            if (!player.IsLocal && !player.IsHostControlled)
            {
                continue;
            }

            PlayerManager.RespawnPlayer(player);
            PhysicsWorld.ResetRopesForPlayer(player);
        }
    }

    public void RestartLevel()
    {
        foreach (CheckpointFlag checkpoint in Level.CheckpointFlags)
        {
            checkpoint.IsActive = false;
        }

        PlayerManager.ClearCheckpoint();
        PlayerManager.ReviveAllAtStart();

        foreach (Player player in Players)
        {
            PhysicsWorld.ResetRopesForPlayer(player);
        }

        PhysicsWorld.ClearTransientState();

        ElapsedTime = 0f;
        TimerRunning = true;
        IsLevelComplete = false;
        IsPlayerDead = false;
        FinalTime = 0f;
        NewRecord = false;
        LavaSurfaceY = _lavaStartSurfaceY;
        IsPaused = false;
        _session.State = GameSessionState.Playing;
        _fixedTimeAccumulator = 0f;
        _latchedLocalInput.Clear();
        CurrentTick = SimulationTick.Zero;
        SnapshotCount = 0;
        LastSnapshot = CreateSnapshot(SimulationTick.Zero);
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

    private void StepFixedTick()
    {
        SimulationTick tick = CurrentTick;
        InputFrame localFrame = CaptureLocalInputFrame(tick);
        _inputBuffer.StoreFrame(localFrame);

        if (!IsLevelComplete && !IsPlayerDead)
        {
            IReadOnlyDictionary<int, PlayerInputState> inputs = _inputBuffer.GetInputs(tick);
            HandleRespawnInputs(inputs);
            PhysicsWorld.UpdatePhysics(TickRate.FixedDeltaSeconds, inputs);
            UpdateCheckpointActivation();
            UpdateLava();

            if (TimerRunning)
            {
                ElapsedTime += TickRate.FixedDeltaSeconds;
            }

            if (CheckLavaDeath())
            {
                // Player died this tick; skip goal evaluation.
            }
            else if (IsAnyPlayerTouchingGoal())
            {
                CompleteLevel();
            }
        }

        CurrentTick = CurrentTick.Next();
        LastSnapshot = CreateSnapshot(tick);
        SnapshotCount++;
        _inputBuffer.TrimBefore(CurrentTick, InputBufferRetentionTicks);
        FixedTickCompleted?.Invoke();
    }

    /// <summary>Fired once per completed fixed simulation tick, after snapshot is produced.</summary>
    public event Action? FixedTickCompleted;

    private void AccumulateLocalInput(ILocalPlayerInputSource localInputSource)
    {
        foreach (Player player in Players)
        {
            if (!player.IsLocal)
            {
                continue;
            }

            PlayerInputState current = localInputSource.GetPlayerInput(player.NetworkId);
            if (_latchedLocalInput.TryGetValue(player.NetworkId, out PlayerInputState latched))
            {
                // Level-triggered axes use the freshest value; edge-triggered flags
                // accumulate (OR) until the next tick consumes them.
                _latchedLocalInput[player.NetworkId] = new PlayerInputState(
                    current.HorizontalMovement,
                    latched.JumpPressed || current.JumpPressed,
                    latched.RespawnPressed || current.RespawnPressed,
                    current.FastFallHeld,
                    current.PullRopeHeld,
                    current.RequestedColor ?? latched.RequestedColor);
            }
            else
            {
                _latchedLocalInput[player.NetworkId] = current;
            }
        }
    }

    private InputFrame CaptureLocalInputFrame(SimulationTick tick)
    {
        InputFrame frame = new(tick, _session.LocalOwnerId);
        foreach (Player player in Players)
        {
            if (!player.IsLocal)
            {
                continue;
            }

            PlayerInputState input = _latchedLocalInput.TryGetValue(player.NetworkId, out PlayerInputState latched)
                ? latched
                : PlayerInputState.Empty;
            frame.AddPlayerInput(player.NetworkId, input);

            // Clear edge-triggered flags now that they are consumed; keep level axes.
            _latchedLocalInput[player.NetworkId] = new PlayerInputState(
                input.HorizontalMovement,
                false,
                false,
                input.FastFallHeld,
                input.PullRopeHeld,
                null);
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

    private void UpdateLava()
    {
        if (!LavaActive || !LavaRiseEnabled)
        {
            return;
        }

        // Smooth, sub-pixel rise so the surface never jumps in block-sized steps.
        LavaSurfaceY -= LavaRiseSpeed * TickRate.FixedDeltaSeconds;
    }

    private bool CheckLavaDeath()
    {
        if (!LavaActive || IsPlayerDead)
        {
            return false;
        }

        foreach (Player player in Players)
        {
            if (LavaLine.IsLethal(player.Bounds, LavaSurfaceY))
            {
                TriggerDeath();
                return true;
            }
        }

        return false;
    }

    private void TriggerDeath()
    {
        IsPlayerDead = true;
        TimerRunning = false;
        _session.State = GameSessionState.Dead;
        _latchedLocalInput.Clear();

        foreach (Player player in Players)
        {
            player.Freeze();
        }
    }

    public void RespawnFromStart()
    {
        PlayerManager.ClearCheckpoint();
        PlayerManager.ReviveAllAtStart();
        ElapsedTime = 0f;
        ResetAfterRespawn();
    }

    public void RespawnFromCheckpoint()
    {
        if (!HasCheckpoint)
        {
            return;
        }

        PlayerManager.ReviveAllAtCheckpoint();
        ResetAfterRespawn();
    }

    private void ResetAfterRespawn()
    {
        IsPlayerDead = false;
        TimerRunning = true;
        LavaSurfaceY = _lavaStartSurfaceY;
        _session.State = GameSessionState.Playing;
        _latchedLocalInput.Clear();

        foreach (Player player in Players)
        {
            PhysicsWorld.ResetRopesForPlayer(player);
        }
    }

    private void HandleRespawnInputs(IReadOnlyDictionary<int, PlayerInputState> inputs)
    {
        foreach (Player player in Players)
        {
            if (!player.IsLocal && !player.IsHostControlled)
            {
                continue;
            }

            if (!inputs.TryGetValue(player.NetworkId, out PlayerInputState input) || !input.RespawnPressed)
            {
                continue;
            }

            PlayerManager.RespawnPlayer(player);
            PhysicsWorld.ResetRopesForPlayer(player);
        }
    }

    private void UpdateCheckpointActivation()
    {
        foreach (Player player in Players)
        {
            Rectangle playerBounds = player.Bounds;
            foreach (CheckpointFlag checkpoint in Level.CheckpointFlags)
            {
                if (!playerBounds.Intersects(checkpoint.TriggerBounds))
                {
                    continue;
                }

                PlayerManager.ActivateCheckpoint(checkpoint);
                return;
            }
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

        foreach (CheckpointFlag checkpoint in Level.CheckpointFlags)
        {
            snapshot.CheckpointFlags.Add(new CheckpointFlagSnapshot(
                checkpoint.Id,
                checkpoint.Position.X,
                checkpoint.Position.Y,
                checkpoint.IsActive));
        }

        foreach (LaunchPad launchPad in Level.LaunchPads)
        {
            snapshot.LaunchPads.Add(new LaunchPadSnapshot(
                launchPad.Bounds.X,
                launchPad.Bounds.Y,
                launchPad.Bounds.Width,
                launchPad.Bounds.Height,
                LaunchPad.NormalizeRotation(launchPad.RotationDegrees)));
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
