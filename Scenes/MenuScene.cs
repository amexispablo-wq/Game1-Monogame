using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class MenuScene : IScene
{
    private readonly ColorBlocksGame _game;
    private readonly Button _playButton = new("Play");
    private readonly Button _partyButton = new("Party");
    private readonly Button _editorButton = new("Level Editor");
    private readonly Button _optionsButton = new("Options");
    private readonly UIFocusManager _focus = new();
    private readonly FocusableButton _playFocus;
    private readonly FocusableButton _partyFocus;
    private readonly FocusableButton _editorFocus;
    private readonly FocusableButton _optionsFocus;

    public MenuScene(ColorBlocksGame game)
    {
        _game = game;
        _playFocus = new FocusableButton(_playButton);
        _partyFocus = new FocusableButton(_partyButton);
        _editorFocus = new FocusableButton(_editorButton);
        _optionsFocus = new FocusableButton(_optionsButton);
    }

    public void Update(GameTime gameTime)
    {
        LayoutButtons();

        if (_game.Input.ExitPressed || _game.Input.MenuCancelPressed)
        {
            _game.ExitGame();
            return;
        }

        _focus.Clear();
        _focus.Add(_playFocus);
        _focus.Add(_partyFocus);
        _focus.Add(_editorFocus);
        _focus.Add(_optionsFocus);
        _focus.Update(gameTime, _game.Input);

        if (_playFocus.WasActivated)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
        }
        else if (_partyFocus.WasActivated)
        {
            _game.ChangeScene(new PartyScene(_game));
        }
        else if (_editorFocus.WasActivated)
        {
            _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
        }
        else if (_optionsFocus.WasActivated)
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
        _partyButton.Draw(spriteBatch, pixel);
        _editorButton.Draw(spriteBatch, pixel);
        _optionsButton.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);

        spriteBatch.End();
    }

    private void LayoutButtons()
    {
        Viewport viewport = _game.Viewport;
        var layout = ButtonColumnLayout.CreateAuto(
            new[] { "Play", "Party", "Level Editor", "Options" },
            viewport.Width, viewport.Height,
            buttonHeight: 56,
            verticalGap: 20,
            topMargin: 90);

        if (layout.ButtonBounds.Length >= 4)
        {
            _playButton.Bounds = layout.ButtonBounds[0];
            _partyButton.Bounds = layout.ButtonBounds[1];
            _editorButton.Bounds = layout.ButtonBounds[2];
            _optionsButton.Bounds = layout.ButtonBounds[3];
        }
    }
}
