using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class MenuScene : IScene
{
    private readonly Game1 _game;
    private readonly Button _playButton = new("Play");
    private readonly Button _editorButton = new("Level Editor");
    private readonly Button _optionsButton = new("Options");

    public MenuScene(Game1 game)
    {
        _game = game;
    }

    public void Update(GameTime gameTime)
    {
        LayoutButtons();

        if (_game.Input.ExitPressed)
        {
            _game.ExitGame();
            return;
        }

        if (_playButton.Update(_game.Input))
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
        }
        else if (_editorButton.Update(_game.Input))
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
        }
        else if (_optionsButton.Update(_game.Input))
        {
            _game.ChangeScene(new OptionsScene(_game));
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutButtons();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        Viewport viewport = _game.Viewport;
        Texture2D pixel = _game.Pixel;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(29, 34, 45));
        spriteBatch.Draw(pixel, new Rectangle(0, viewport.Height - 160, viewport.Width, 160), new Color(22, 26, 34));

        _playButton.Draw(spriteBatch, pixel);
        _editorButton.Draw(spriteBatch, pixel);
        _optionsButton.Draw(spriteBatch, pixel);

        spriteBatch.End();
    }

    private void LayoutButtons()
    {
        Viewport viewport = _game.Viewport;
        var layout = ButtonRowLayout.Create(
            new[] { "Play", "Level Editor", "Options" },
            viewport.Width, viewport.Height,
            82, 16, 12, 24, 160);

        if (layout.ButtonBounds.Length >= 3)
        {
            _playButton.Bounds = layout.ButtonBounds[0];
            _editorButton.Bounds = layout.ButtonBounds[1];
            _optionsButton.Bounds = layout.ButtonBounds[2];
        }
    }
}
