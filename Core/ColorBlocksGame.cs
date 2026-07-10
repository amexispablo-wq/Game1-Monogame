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
    private readonly SteamInputService _steamInput;
    private readonly SteamLobbyService _steamLobby;
    private readonly SteamPartyService _steamParty;
    private readonly SteamGameNetworkService _steamGameNetwork;
    private readonly GameNetworkCoordinator _gameNetwork;
    private readonly PartyHudOverlay _partyHud = new();
    private readonly MusicManager _music = new();
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

        SettingsManager.Initialize();
        SkinLibraryStorage.Initialize();
        var settings = SettingsManager.CurrentSettings;
        _graphics.PreferredBackBufferWidth = settings.ResolutionWidth;
        _graphics.PreferredBackBufferHeight = settings.ResolutionHeight;
        ApplyFrameSettings(settings.FpsLimit, applyChanges: false);
        ApplyGraphicsSettings(settings.ResolutionWidth, settings.ResolutionHeight, settings.DisplayMode, applyChanges: false);
        _steamLobby = new SteamLobbyService(_steam, _steamCallbacks);
        _steamInput = new SteamInputService(_steam);
        _steamParty = new SteamPartyService(_steamLobby);
        _steamGameNetwork = new SteamGameNetworkService(_steam, _steamCallbacks);
        _gameNetwork = new GameNetworkCoordinator(_steamGameNetwork, _steamLobby);
    }

    public InputManager Input => _input;
    public DeveloperTuningPanel? ActiveTuningPanel { get; set; }
    public PartyManager Party { get; } = new();
    public Texture2D Pixel => _pixel;
    public SteamManager Steam => _steam;
    public SteamLobbyService SteamLobby => _steamLobby;
    public SteamInputService SteamInput => _steamInput;
    public SteamPartyService SteamParty => _steamParty;
    public GameNetworkCoordinator GameNetwork => _gameNetwork;
    public MusicManager Music => _music;
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
    }

    public void ExitGame()
    {
        _currentScene?.OnExit();
        Party.LeaveParty();
        Exit();
    }

    protected override void Initialize()
    {
        _steam.Initialize();
        if (_steam.IsInitialized)
        {
            _steamCallbacks.Register();
            _steamInput.Initialize();
            Party.BindSteamServices(_steamLobby, _steamParty);
        }

        _input = new InputManager();
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

        var settings = SettingsManager.CurrentSettings;
        ApplyGraphicsSettings(settings.ResolutionWidth, settings.ResolutionHeight, settings.DisplayMode);

        _music.ApplyVolume(SettingsManager.GetMusicVolume());
        ApplyFrameSettings(SettingsManager.CurrentSettings.FpsLimit);
        ChangeScene(new MenuScene(this));
    }

    protected override void Update(GameTime gameTime)
    {
        _steam.RunCallbacks();
        _steamInput.RunFrame();
        _input.ConfigurePointerTransform(Window.ClientBounds, GraphicsDevice.Viewport, _presentation);
        _input.Update();

        if (_input.ReplayBackgroundTogglePressed)
        {
            ReplayManager.MenuBackgroundEnabled = !ReplayManager.MenuBackgroundEnabled;
        }

        if (_input.DebugTogglePressed
            && (ReplayManager.HasReplay() || ReplayDiagnostics.ActiveRecorder is not null))
        {
            ReplayDiagnostics.DebugOverlayVisible = !ReplayDiagnostics.DebugOverlayVisible;
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
        _partyHud.Draw(_spriteBatch, _pixel, Viewport, Party);
        ActiveTuningPanel?.Draw(_spriteBatch, _pixel, Viewport, Party);
        if (DeveloperSettings.DeveloperMode)
        {
            _benchmarkOverlay.Draw(_spriteBatch, _pixel, Viewport, BenchmarkManager.Runner);
            BenchmarkDebugOverlay.Draw(_spriteBatch, _pixel, Viewport, BenchmarkManager.Runner);
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
            _presentation.Dispose();
            _steamInput.Shutdown();
            _steamCallbacks.Dispose();
            _steam.Shutdown();
        }

        base.Dispose(disposing);
    }
}
