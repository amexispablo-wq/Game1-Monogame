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
    private readonly Button _quitButton = new("Quit");
    private readonly UIFocusManager _focus = new();
    private readonly FocusableButton _playFocus;
    private readonly FocusableButton _partyFocus;
    private readonly FocusableButton _editorFocus;
    private readonly FocusableButton _optionsFocus;
    private readonly FocusableButton _quitFocus;

    public MenuScene(ColorBlocksGame game)
    {
        _game = game;
        _playFocus = new FocusableButton(_playButton);
        _partyFocus = new FocusableButton(_partyButton);
        _editorFocus = new FocusableButton(_editorButton);
        _optionsFocus = new FocusableButton(_optionsButton);
        _quitFocus = new FocusableButton(_quitButton);
        _focus.ResetFocus();
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
        int playIndex = _focus.Add(_playFocus, "Play");
        int partyIndex = _focus.Add(_partyFocus, "Party");
        int editorIndex = _focus.Add(_editorFocus, "LevelEditor");
        int optionsIndex = _focus.Add(_optionsFocus, "Options");
        int quitIndex = _focus.Add(_quitFocus, "Quit");

        NavigationGraph nav = _focus.Navigation;
        nav.LinkVertical(playIndex, partyIndex);
        nav.LinkVertical(partyIndex, editorIndex);
        nav.LinkVertical(editorIndex, optionsIndex);
        nav.LinkVertical(optionsIndex, quitIndex);

        _focus.FinalizeFocus("Play");
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
        else if (_quitFocus.WasActivated)
        {
            _game.ExitGame();
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
        _quitButton.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);

        spriteBatch.End();
    }

    private void LayoutButtons()
    {
        Viewport viewport = _game.Viewport;
        var layout = ButtonColumnLayout.CreateAuto(
            new[] { "Play", "Party", "Level Editor", "Options", "Quit" },
            viewport.Width, viewport.Height,
            buttonHeight: 56,
            verticalGap: 20,
            topMargin: 90);

        if (layout.ButtonBounds.Length >= 5)
        {
            _playButton.Bounds = layout.ButtonBounds[0];
            _partyButton.Bounds = layout.ButtonBounds[1];
            _editorButton.Bounds = layout.ButtonBounds[2];
            _optionsButton.Bounds = layout.ButtonBounds[3];
            _quitButton.Bounds = layout.ButtonBounds[4];
        }
    }
}
