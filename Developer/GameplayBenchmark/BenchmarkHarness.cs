#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ColorBlocks.Developer.GameplayBenchmark;

public sealed class BenchmarkHarness : IDisposable
{
    private static int _nextMemberId = 1000;
    private bool _disposed;

    public BenchmarkHarness(GameSimulation simulation, ScriptedInputSource input, Camera camera, string levelId)
    {
        Simulation = simulation;
        Input = input;
        Camera = camera;
        LevelId = levelId;
    }

    public GameSimulation Simulation { get; }
    public ScriptedInputSource Input { get; }
    public Camera Camera { get; }
    public string LevelId { get; }

    public int RunTicks(int tickCount, Action<int>? onTick = null, int? maxSeconds = null)
    {
        if (tickCount <= 0)
        {
            return 0;
        }

        float dt = Simulation.TickRate.FixedDeltaSeconds;
        int executed = 0;
        DateTime deadline = maxSeconds.HasValue ? DateTime.UtcNow.AddSeconds(maxSeconds.Value) : DateTime.MaxValue;

        while (executed < tickCount)
        {
            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            int steps = Simulation.Advance(dt, Input);
            if (steps <= 0)
            {
                break;
            }

            for (int i = 0; i < steps; i++)
            {
                executed++;
                onTick?.Invoke(executed);
                if (executed >= tickCount)
                {
                    break;
                }
            }

            if (Simulation.IsLevelComplete || Simulation.IsPlayerDead)
            {
                break;
            }
        }

        return executed;
    }

    public void ApplyUniformInput(PlayerInputState input)
    {
        foreach (Player player in Simulation.Players)
        {
            Input.SetInput(player.NetworkId, input);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }

    public static BenchmarkHarness Create(
        Level level,
        int playerCount,
        RopeGameplayMode ropeMode,
        bool lavaRiseEnabled = false,
        string levelId = "benchmark")
    {
        int clampedPlayers = Math.Clamp(playerCount, 1, PartyManager.MaxMembers);
        GameSession session = GameSession.CreateLocalTest(levelId, ropeMode);
        session.LavaRiseEnabled = lavaRiseEnabled;
        PlayerManager playerManager = new(session, level);
        ScriptedInputSource scriptedInput = new();
        InputManager bindingInput = new();

        List<PartyMember> members = CreateBenchmarkMembers(clampedPlayers);
        playerManager.SpawnFromParty(members, bindingInput);
        scriptedInput.BindPlayers(playerManager.Players);

        GameSimulation simulation = new(session, level, playerManager, lavaRiseEnabled);
        Vector2 center = playerManager.Players.Count > 0
            ? GameplayCameraHelper.GetPlayersCenter(playerManager.Players, level.PlayerStart)
            : level.PlayerStart;
        Camera camera = new(center);
        return new BenchmarkHarness(simulation, scriptedInput, camera, levelId);
    }

    private static List<PartyMember> CreateBenchmarkMembers(int playerCount)
    {
        List<PartyMember> members = new();
        for (int i = 0; i < playerCount; i++)
        {
            PartyMember member = new(
                new PartyMemberId(_nextMemberId++),
                $"P{i + 1}",
                PartyMemberType.LocalKeyboard,
                PartyInputSource.Keyboard)
            {
                MemberIndex = i,
                NetworkPlayerId = i + 1
            };
            members.Add(member);
        }

        return members;
    }
}
