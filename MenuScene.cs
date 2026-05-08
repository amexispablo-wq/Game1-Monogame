using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class MenuScene : IScene
{
    private readonly Game1 _game;
    private readonly Button _playButton = new("Play");
    private readonly Button _editorButton = new("Level Editor");

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
            _game.ChangeScene(new GameScene(_game));
        }
        else if (_editorButton.Update(_game.Input))
        {
            _game.ChangeScene(new EditorScene(_game));
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

        spriteBatch.End();
    }

    private void LayoutButtons()
    {
        Viewport viewport = _game.Viewport;
        const int buttonWidth = 380;
        const int buttonHeight = 82;
        const int gap = 24;

        int x = (viewport.Width - buttonWidth) / 2;
        int y = (viewport.Height - ((buttonHeight * 2) + gap)) / 2;

        _playButton.Bounds = new Rectangle(x, y, buttonWidth, buttonHeight);
        _editorButton.Bounds = new Rectangle(x, y + buttonHeight + gap, buttonWidth, buttonHeight);
    }
}
