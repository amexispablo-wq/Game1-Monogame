#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ColorBlocks.Replay;
using ColorBlocks.Developer.GameplayBenchmark;

namespace ColorBlocks;

public class ColorBlocksGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly SteamManager _steam = new();
    private readonly SteamCallbackManager _steamCallbacks = new();
    private readonly SteamInputManager _steamInput;
    private readonly SteamLobbyService _steamLobby;
    private readonly SteamPartyService _steamParty;
    private readonly SteamGameNetworkService _steamGameNetwork;
    private readonly SteamInviteManager _steamInvites;
    private readonly GameNetworkCoordinator _gameNetwork;
    private readonly LevelStartRouter _levelStartRouter;
    private readonly SteamReplayService _steamReplays;
    private readonly SteamLeaderboardService _steamLeaderboards;
    private readonly SteamGhostService _steamGhosts;
    private readonly SteamWorkshopService _steamWorkshop;
    private readonly PartyHudOverlay _partyHud = new();
    private readonly MusicManager _music = new();
    private readonly SfxManager _sfx = new();
    private readonly AudioOutputHotSwap _audioOutputHotSwap = new();
    private readonly PresentationManager _presentation = new();
    private readonly ReplayBackgroundRenderer _replayBackground = new();
    private readonly BenchmarkOverlay _benchmarkOverlay = new();
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private InputManager _input = null!;
    private IScene _currentScene = null!;

    public ColorBlocksGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "Color Blocks";

        UserDataPaths.Initialize();
        DiagnosticsLog.Initialize();
        foreach (string line in BuildInfo.Current.DescribeLines())
        {
            DiagnosticsLog.Info("BuildInfo", line);
        }

        SessionDiagnostics.LogSteamInputFileHashes();
        UserDataMigration.RunIfNeeded();
        SettingsManager.Initialize();
        SkinLibraryStorage.Initialize();
        var settings = SettingsManager.CurrentSettings;
        _graphics.PreferredBackBufferWidth = settings.ResolutionWidth;
        _graphics.PreferredBackBufferHeight = settings.ResolutionHeight;
        ApplyFrameSettings(settings.FpsLimit, applyChanges: false);
        ApplyGraphicsSettings(settings.ResolutionWidth, settings.ResolutionHeight, settings.DisplayMode, applyChanges: false);
        _steamLobby = new SteamLobbyService(_steam, _steamCallbacks);
        _steamInput = new SteamInputManager(_steam);
        _steamParty = new SteamPartyService(_steamLobby);
        _steamGameNetwork = new SteamGameNetworkService(_steam, _steamCallbacks);
        _steamInvites = new SteamInviteManager(_steam, _steamCallbacks, _steamLobby);
        _gameNetwork = new GameNetworkCoordinator(_steamGameNetwork, _steamLobby);
        _levelStartRouter = new LevelStartRouter(this);
        _steamReplays = new SteamReplayService(_steam);
        _steamLeaderboards = new SteamLeaderboardService(_steam);
        _steamGhosts = new SteamGhostService(_steamLeaderboards, _steamReplays);
        _steamWorkshop = new SteamWorkshopService(_steam);
    }

    public InputManager Input => _input;
    public DeveloperTuningPanel? ActiveTuningPanel { get; set; }
    public PartyManager Party { get; } = new();
    public Texture2D Pixel => _pixel;
    public SteamManager Steam => _steam;
    public SteamLobbyService SteamLobby => _steamLobby;
    public SteamInputManager SteamInput => _steamInput;
    public SteamPartyService SteamParty => _steamParty;
    public SteamInviteManager SteamInvites => _steamInvites;
    public GameNetworkCoordinator GameNetwork => _gameNetwork;
    public LevelStartRouter LevelStartRouter => _levelStartRouter;
    public SteamReplayService SteamReplays => _steamReplays;
    public SteamLeaderboardService SteamLeaderboards => _steamLeaderboards;
    public SteamGhostService SteamGhosts => _steamGhosts;
    public SteamWorkshopService SteamWorkshop => _steamWorkshop;
    public MusicManager Music => _music;
    public SfxManager Sfx => _sfx;
    public Viewport Viewport => _presentation.LogicalViewport;
    public PresentationManager Presentation => _presentation;
    public IScene CurrentScene => _currentScene;

    public void ApplyGraphicsSettings(int width, int height, string? displayMode = null, bool applyChanges = true)
    {
        string mode = displayMode?.ToLowerInvariant() ?? SettingsManager.CurrentSettings.DisplayMode.ToLowerInvariant();
        bool borderless = mode is "borderless" or "borderlesswindowed";
        bool fullscreen = mode == "fullscreen";
        bool letterbox = fullscreen || borderless;

        int backBufferWidth = width;
        int backBufferHeight = height;
        if (letterbox)
        {
            Microsoft.Xna.Framework.Graphics.DisplayMode monitorMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            backBufferWidth = monitorMode.Width;
            backBufferHeight = monitorMode.Height;
        }

        _graphics.HardwareModeSwitch = false;
        _graphics.PreferredBackBufferWidth = backBufferWidth;
        _graphics.PreferredBackBufferHeight = backBufferHeight;

        switch (mode)
        {
            case "fullscreen":
                _graphics.IsFullScreen = true;
                Window.IsBorderless = false;
                break;
            case "windowed":
                _graphics.IsFullScreen = false;
                Window.IsBorderless = false;
                break;
            case "borderless":
            case "borderlesswindowed":
                _graphics.IsFullScreen = false;
                Window.IsBorderless = true;
                break;
            default:
                _graphics.IsFullScreen = false;
                Window.IsBorderless = false;
                break;
        }

        if (applyChanges && GraphicsDevice is not null)
        {
            _graphics.ApplyChanges();

            if (borderless)
            {
                Window.Position = Point.Zero;
                _graphics.ApplyChanges();
            }

            _presentation.Configure(GraphicsDevice, width, height, letterbox);
        }
        else if (GraphicsDevice is not null)
        {
            _presentation.Configure(GraphicsDevice, width, height, letterbox);
        }
    }

    // fpsLimit: -1 = VSync, 0 = Unlimited, >0 = hard cap.
    public void ApplyFrameSettings(int fpsLimit, bool applyChanges = true)
    {
        bool useVsync = fpsLimit < 0;

        _graphics.SynchronizeWithVerticalRetrace = useVsync;

        if (fpsLimit < 0)
        {
            IsFixedTimeStep = false;
        }
        else if (fpsLimit == 0)
        {
            IsFixedTimeStep = false;
        }
        else
        {
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / fpsLimit);
        }

        if (applyChanges)
        {
            _graphics.ApplyChanges();
        }
    }

    public void ChangeScene(IScene scene)
    {
        _currentScene?.OnExit();
        _currentScene = scene;
        NavigationDebug.CurrentScene = scene.GetType().Name;
        ApplySceneMusic(scene);

        // Menu scenes: eat leftover A/Enter so Party Play (etc.) cannot fire from same press.
        if (scene is not GameScene)
        {
            _input?.SuppressMenuConfirmUntilRelease();
        }

        // Guest may have received START while on a scene that did not listen yet.
        _levelStartRouter.TryApplyPending();
    }

    /// <summary>
    /// Friends-panel invite / Join Game land the invitee in Party UI.
    /// Host CreateLobby is owner → no navigate. Already on Party/Game → leave alone.
    /// </summary>
    private void OnSteamLobbyReadyForExternalJoin()
    {
        if (!_steamLobby.IsInLobby || _steamLobby.IsLobbyOwner())
        {
            return;
        }

        if (_currentScene is PartyScene or GameScene)
        {
            return;
        }

        MultiplayerDebug.LogLobby(
            $"ExternalJoin → PartyScene lobby={_steamLobby.CurrentLobbyId} from={_currentScene?.GetType().Name ?? "null"}");
        ChangeScene(new PartyScene(this));
    }

    /// <summary>
    /// Single place that decides whether Steam Move drives gameplay or UI.
    /// Uses prior-frame GameplayInputBlocked so pause/menus take stick without per-scene logic.
    /// </summary>
    private AnalogInputContext ResolveAnalogInputContext()
    {
        if (_currentScene is GameScene && !_input.GameplayInputBlocked)
        {
            return AnalogInputContext.Gameplay;
        }

        return AnalogInputContext.Menu;
    }

    private void ApplySceneMusic(IScene scene)
    {
        switch (scene)
        {
            case GameScene gameScene:
                _music.PlayLevelMusic(gameScene.LevelMusicId);
                break;
            case EditorScene:
                _music.PlayEditorMusic();
                break;
            case RopeSandboxScene:
            case ReplayViewerScene:
                _music.Stop();
                break;
            default:
                _music.PlayMenuMusic();
                break;
        }
    }

    public void ExitGame()
    {
        _currentScene?.OnExit();
        Party.LeaveParty();
        _steamInvites.ClearPresence();
        Exit();
    }

    protected override void Initialize()
    {
        _steam.Initialize();
        DiagnosticsLog.Info("Steam", $"SteamAPI.Init ok={_steam.IsInitialized} status='{_steam.Status}'");
        if (_steam.IsInitialized)
        {
            _steamCallbacks.Register();
            _steamInput.Initialize();
            DiagnosticsLog.Info(
                "Steam",
                $"SteamInput.Init ok={_steamInput.IsInitialized} status='{_steamInput.InitializationStatus}'");
            Party.BindSteamServices(_steamLobby, _steamParty);
            _levelStartRouter.Bind();
            _steamLobby.LobbyReady += OnSteamLobbyReadyForExternalJoin;
            _steamWorkshop.Initialize();
            _steamWorkshop.SyncSubscribedItems();
            _steamInvites.TryConsumeLaunchJoin(Environment.GetCommandLineArgs());
        }

        _input = new InputManager();
        _input.BindSteamInput(_steamInput);
        Party.LocalSteamUsername = _steam.Username;
        LevelAuthorProvider.ResolveLocalAuthor = () =>
            _steam.IsInitialized && !string.IsNullOrWhiteSpace(_steam.Username) && _steam.Username != "Unavailable"
                ? _steam.Username
                : Environment.UserName;
        LevelLibrary.Initialize();
        Party.EnsureDefaultParty();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        ContentResolver.Bind(this);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _steamInput.Glyphs.BindGraphicsDevice(GraphicsDevice);

        var settings = SettingsManager.CurrentSettings;
        ApplyGraphicsSettings(settings.ResolutionWidth, settings.ResolutionHeight, settings.DisplayMode);

        _sfx.Load();
        GameAudio.Sfx = _sfx;
        _music.ApplyVolume(SettingsManager.GetMusicVolume());
        ApplyFrameSettings(SettingsManager.CurrentSettings.FpsLimit);
        ChangeScene(new MenuScene(this));
    }

    protected override void Update(GameTime gameTime)
    {
        _steam.RunCallbacks();
        _steamInput.RunFrame();
        _input.ConfigurePointerTransform(Window.ClientBounds, GraphicsDevice.Viewport, _presentation);
        _input.AnalogContext = ResolveAnalogInputContext();
        _input.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        GameAudio.Update(dt);
        _audioOutputHotSwap.Update(dt);

        if (_input.SteamInputOriginDumpPressed && _steamInput.IsInitialized)
        {
            _steamInput.DumpActionOriginsToConsole();
        }

        if (_input.ReplayBackgroundTogglePressed)
        {
            ReplayManager.MenuBackgroundEnabled = !ReplayManager.MenuBackgroundEnabled;
        }

        if (_input.DebugTogglePressed)
        {
            if (DeveloperSettings.DeveloperMode)
            {
                UserDataDebugOverlay.Visible = !UserDataDebugOverlay.Visible;
            }

            if (ReplayManager.HasReplay() || ReplayDiagnostics.ActiveRecorder is not null)
            {
                ReplayDiagnostics.DebugOverlayVisible = !ReplayDiagnostics.DebugOverlayVisible;
            }
        }

        _replayBackground.Update(this, gameTime);
        BenchmarkManager.Update(gameTime, _input);
        if (DeveloperSettings.DeveloperMode)
        {
            _benchmarkOverlay.HandleInput(_input, BenchmarkManager.Runner);
        }

        _currentScene.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        System.Diagnostics.Stopwatch renderWatch = System.Diagnostics.Stopwatch.StartNew();
        _presentation.BeginDraw(GraphicsDevice);
        GraphicsDevice.Clear(new Color(23, 27, 34));
        _replayBackground.Draw(this, gameTime, _spriteBatch);
        _currentScene.Draw(gameTime, _spriteBatch);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        if (_currentScene is not GameScene gameScene || !gameScene.IsPhotoModeActive)
        {
            _partyHud.Draw(_spriteBatch, _pixel, Viewport, Party);
        }

        ActiveTuningPanel?.Draw(_spriteBatch, _pixel, Viewport, Party);
        if (DeveloperSettings.DeveloperMode)
        {
            _benchmarkOverlay.Draw(_spriteBatch, _pixel, Viewport, BenchmarkManager.Runner);
            BenchmarkDebugOverlay.Draw(_spriteBatch, _pixel, Viewport, BenchmarkManager.Runner);
            UserDataDebugOverlay.Draw(_spriteBatch, _pixel, Viewport, _steamInput, _input);
        }

        ReplayDebugOverlay.Draw(_spriteBatch, _pixel, Viewport);
        _spriteBatch.End();

        renderWatch.Stop();
        BenchmarkManager.LastRenderMs = renderWatch.Elapsed.TotalMilliseconds;

        _presentation.EndDraw(GraphicsDevice, _spriteBatch, _pixel, new Color(23, 27, 34));

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _pixel.Dispose();
        base.UnloadContent();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _levelStartRouter.Unbind();
            _presentation.Dispose();
            _steamInvites.ClearPresence();
            _steamInput.Shutdown();
            _steamWorkshop.Dispose();
            _steamCallbacks.Dispose();
            _steam.Shutdown();
        }

        base.Dispose(disposing);
    }
}
