using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game1_Monogame;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private InputManager _input = null!;
    private IScene _currentScene = null!;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
            SynchronizeWithVerticalRetrace = true
        };

        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Load settings before creating window
        SettingsManager.Initialize();
        var settings = SettingsManager.CurrentSettings;
        _graphics.PreferredBackBufferWidth = settings.ResolutionWidth;
        _graphics.PreferredBackBufferHeight = settings.ResolutionHeight;
    }

    public InputManager Input => _input;
    public Texture2D Pixel => _pixel;
    public Viewport Viewport => GraphicsDevice.Viewport;

    public void ApplyGraphicsSettings(int width, int height)
    {
        _graphics.PreferredBackBufferWidth = width;
        _graphics.PreferredBackBufferHeight = height;
        _graphics.ApplyChanges();
    }

    public void ChangeScene(IScene scene)
    {
        _currentScene?.OnExit();
        _currentScene = scene;
    }

    public void ExitGame()
    {
        _currentScene?.OnExit();
        Exit();
    }

    protected override void Initialize()
    {
        _input = new InputManager();
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
        _input.Update();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
        {
            ExitGame();
        }

        _currentScene.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(23, 27, 34));
        _currentScene.Draw(gameTime, _spriteBatch);

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        _pixel.Dispose();
        base.UnloadContent();
    }
}
