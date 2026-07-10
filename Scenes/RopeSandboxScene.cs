using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ColorBlocks.Replay;

namespace ColorBlocks;

public sealed class RopeSandboxScene : IScene
{
    private const float MaxSceneFrameTime = 0.25f;

    private readonly ColorBlocksGame _game;
    private readonly GameSession _session;
    private readonly Level _level;
    private readonly PlayerManager _playerManager;
    private readonly GameSimulation _simulation;
    private readonly Camera _camera;
    private readonly DeveloperTuningPanel _tuningPanel = new();
    private bool _debugDraw = true;

    public RopeSandboxScene(ColorBlocksGame game)
    {
        _game = game;
        EnsureSandboxParty();
        _session = GameSession.CreateLocalTest("rope_sandbox", RopeGameplayMode.Neutral);
        _level = Level.CreateRopeSandbox();
        _playerManager = new PlayerManager(_session, _level);
        _game.ActiveTuningPanel = _tuningPanel;
        _game.Input.GameplayInputBlocked = false;
        _game.Party.ApplyPreferredInputForPrimaryLocalMember(_game.Input);
        _game.Party.LockAssignments();
        _playerManager.SpawnFromParty(_game.Party.Members, _game.Input);
        _simulation = new GameSimulation(_session, _level, _playerManager, lavaRiseEnabled: false);
        _camera = new Camera(GetPlayersCenter());
    }

    public void OnExit()
    {
        _game.ActiveTuningPanel = null;
        _game.Input.ClearGameplayBindings();
        _game.Party.UnlockAssignments();
        _game.GameNetwork.Reset();
        _game.Music.Stop();
    }

    public void Update(GameTime gameTime)
    {
        float dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxSceneFrameTime);
        Viewport viewport = _game.Viewport;

        if (_game.Input.ExitPressed || _game.Input.MenuCancelPressed)
        {
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        if (_game.Input.DebugTogglePressed)
        {
            _debugDraw = !_debugDraw;
        }

        if (_game.Input.TuningPanelTogglePressed)
        {
            _tuningPanel.Toggle();
        }

        _tuningPanel.Update(gameTime, _game.Input, viewport, _game.Party);
        _simulation.Advance(dt, _game.Input);
        UpdateCamera(gameTime);
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        Viewport viewport = _game.Viewport;

        GameplayWorldRenderer.Draw(
            spriteBatch,
            _game.Pixel,
            viewport,
            _camera,
            _level,
            _simulation.Ropes,
            _simulation.Players,
            lavaActive: false,
            lavaSurfaceY: 0f,
            elapsedTime: _simulation.ElapsedTime,
            gameTime,
            _debugDraw);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawSandboxHud(spriteBatch, _game.Pixel, viewport);
        if (_debugDraw)
        {
            DrawRopeDebug(spriteBatch, _game.Pixel, viewport);
        }

        spriteBatch.End();
    }

    private void EnsureSandboxParty()
    {
        _game.Party.EnsureDevSandboxMembers();
    }

    private void UpdateCamera(GameTime gameTime)
    {
        GameplayCameraHelper.UpdateSmoothFollow(
            _camera,
            gameTime,
            GetPlayersCenter(),
            1f);
    }

    private Vector2 GetPlayersCenter()
    {
        if (_simulation.Players.Count == 0)
        {
            return _level.PlayerStart;
        }

        Vector2 sum = Vector2.Zero;
        foreach (Player player in _simulation.Players)
        {
            sum += player.Position + (player.Size * 0.5f);
        }

        return sum / _simulation.Players.Count;
    }

    private void DrawSandboxHud(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "ROPE SANDBOX", new Vector2(16, 12), 2, Color.White);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "ESC: MENU  F3: DEBUG  F6: TUNING", new Vector2(16, 36), 1, Color.Cyan);
    }

    private void DrawRopeDebug(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int margin = 12;
        int y = 64;
        int lineHeight = 12;
        foreach (Player player in _simulation.Players)
        {
            string line = $"P{player.PlayerIndex + 1} SPD {player.Velocity.Length():0} FRC {player.GroundFriction:0.#}";
            SimpleTextRenderer.DrawString(spriteBatch, pixel, line, new Vector2(margin, y), 1, Color.White);
            y += lineHeight;
        }

        foreach (Rope rope in _simulation.Ropes)
        {
            string line =
                $"ROPE LEN {rope.CurrentPathLength:0} TARGET {rope.TargetRestLength:0} SLACK {rope.SlackAmount:0} TENSION {rope.LastTension:0.00} {rope.TensionPhase}";
            SimpleTextRenderer.DrawString(spriteBatch, pixel, line, new Vector2(margin, y), 1, Color.Yellow);
            y += lineHeight;
            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                $"PULL {rope.LastPullIntensity:0.00} FORCE {rope.LastEndpointForce:0} ITERS {rope.SolverIterations} NODES {rope.Nodes.Count}",
                new Vector2(margin, y),
                1,
                Color.LimeGreen);
            y += lineHeight;
        }

        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            $"SIM STEP {_simulation.PhysicsWorld.LastSimulationStepSeconds * 1000f:0.0}ms",
            new Vector2(margin, y),
            1,
            Color.White);
    }
}
