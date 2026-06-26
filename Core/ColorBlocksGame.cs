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
        _steamLobby = new SteamLobbyService(_steam, _steamCallbacks);
        _steamParty = new SteamPartyService(_steamLobby);
    }

    public InputManager Input => _input;
    public PartyManager Party { get; } = new();
    public Texture2D Pixel => _pixel;
    public SteamManager Steam => _steam;
    public SteamLobbyService SteamLobby => _steamLobby;
    public SteamPartyService SteamParty => _steamParty;
    public Viewport Viewport => GraphicsDevice.Viewport;

    public void ApplyGraphicsSettings(int width, int height, string? displayMode = null)
    {
        _graphics.PreferredBackBufferWidth = width;
        _graphics.PreferredBackBufferHeight = height;

        if (!string.IsNullOrEmpty(displayMode))
        {
            switch (displayMode.ToLower())
            {
                case "fullscreen":
                    _graphics.IsFullScreen = true;
                    Window.IsBorderless = false;
                    break;
                case "windowed":
                    _graphics.IsFullScreen = false;
                    Window.IsBorderless = false;
                    break;
                case "borderless" or "borderlesswindowed":
                    _graphics.IsFullScreen = false;
                    Window.IsBorderless = true;
                    break;
            }
        }

        _graphics.ApplyChanges();
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
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        ChangeScene(new MenuScene(this));
    }

    protected override void Update(GameTime gameTime)
    {
        _steam.RunCallbacks();
        _input.Update();

        _currentScene.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(23, 27, 34));
        _currentScene.Draw(gameTime, _spriteBatch);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _partyHud.Draw(_spriteBatch, _pixel, Viewport, Party);
        _spriteBatch.End();

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
            _steamCallbacks.Dispose();
            _steam.Shutdown();
        }

        base.Dispose(disposing);
    }
}
