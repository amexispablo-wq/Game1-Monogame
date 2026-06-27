#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public class ColorBlocksGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly SteamManager _steam = new();
    private readonly SteamCallbackManager _steamCallbacks = new();
    private readonly SteamLobbyService _steamLobby;
    private readonly SteamPartyService _steamParty;
    private readonly PartyHudOverlay _partyHud = new();
    private readonly MusicManager _music = new();
    private readonly PresentationManager _presentation = new();
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
        var settings = SettingsManager.CurrentSettings;
        _graphics.PreferredBackBufferWidth = settings.ResolutionWidth;
        _graphics.PreferredBackBufferHeight = settings.ResolutionHeight;
        ApplyFrameSettings(settings.FpsLimit, applyChanges: false);
        ApplyGraphicsSettings(settings.ResolutionWidth, settings.ResolutionHeight, settings.DisplayMode, applyChanges: false);
        _steamLobby = new SteamLobbyService(_steam, _steamCallbacks);
        _steamParty = new SteamPartyService(_steamLobby);
    }

    public InputManager Input => _input;
    public PartyManager Party { get; } = new();
    public Texture2D Pixel => _pixel;
    public SteamManager Steam => _steam;
    public SteamLobbyService SteamLobby => _steamLobby;
    public SteamPartyService SteamParty => _steamParty;
    public MusicManager Music => _music;
    public Viewport Viewport => _presentation.LogicalViewport;
    public PresentationManager Presentation => _presentation;

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
        if (fpsLimit < 0)
        {
            _graphics.SynchronizeWithVerticalRetrace = true;
            IsFixedTimeStep = false;
        }
        else if (fpsLimit == 0)
        {
            _graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
        }
        else
        {
            _graphics.SynchronizeWithVerticalRetrace = false;
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
            Party.BindSteamServices(_steamLobby, _steamParty);
        }

        _input = new InputManager();
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
        ChangeScene(new MenuScene(this));
    }

    protected override void Update(GameTime gameTime)
    {
        _steam.RunCallbacks();
        _input.ConfigurePointerTransform(Window.ClientBounds, GraphicsDevice.Viewport, _presentation);
        _input.Update();

        _currentScene.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _presentation.BeginDraw(GraphicsDevice);
        GraphicsDevice.Clear(new Color(23, 27, 34));
        _currentScene.Draw(gameTime, _spriteBatch);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _partyHud.Draw(_spriteBatch, _pixel, Viewport, Party);
        _spriteBatch.End();

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
            _steamCallbacks.Dispose();
            _steam.Shutdown();
        }

        base.Dispose(disposing);
    }
}
