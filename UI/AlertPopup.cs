#nullable enable

using System;

using Microsoft.Xna.Framework;

using Microsoft.Xna.Framework.Graphics;



namespace ColorBlocks;



public sealed class AlertPopup

{

    private readonly string _title;

    private readonly string _message;

    private readonly Button _okButton = new("OK");

    private readonly UIFocusManager _focus = new();

    private readonly FocusableButton _okFocus;

    private bool _isDismissed;



    public AlertPopup(string title, string message)

    {

        _title = title;

        _message = message;

        _okFocus = new FocusableButton(_okButton);

    }



    public bool IsDismissed => _isDismissed;



    public void Update(GameTime gameTime, InputManager input, int viewportWidth, int viewportHeight)

    {

        Layout(viewportWidth, viewportHeight);

        _focus.Clear();

        _focus.Add(_okFocus);

        _focus.Update(gameTime, input);



        if (_okFocus.WasActivated || input.MenuConfirmPressed || input.ExitPressed)

        {

            _isDismissed = true;

        }

    }



    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, int viewportWidth, int viewportHeight, GameTime gameTime, InputManager input)

    {

        Layout(viewportWidth, viewportHeight);

        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(0, 0, 0, 170));



        int panelWidth = Math.Clamp(viewportWidth / 2, 360, 560);

        int panelHeight = 180;

        Rectangle panel = new((viewportWidth - panelWidth) / 2, (viewportHeight - panelHeight) / 2, panelWidth, panelHeight);

        spriteBatch.Draw(pixel, panel, new Color(38, 46, 62));

        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(255, 220, 80), 2);



        int titleScale = 3;

        int messageScale = 2;

        Rectangle titleBounds = new(panel.X + 16, panel.Y + 20, panel.Width - 32, 36);

        Rectangle messageBounds = new(panel.X + 16, panel.Y + 64, panel.Width - 32, 60);

        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, _title, titleBounds, titleScale, Color.White);

        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, _message, messageBounds, messageScale, new Color(210, 218, 230));

        _okButton.Draw(spriteBatch, pixel);

        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, input);

    }



    private void Layout(int viewportWidth, int viewportHeight)

    {

        int panelWidth = Math.Clamp(viewportWidth / 2, 360, 560);

        int panelHeight = 180;

        int panelX = (viewportWidth - panelWidth) / 2;

        int panelY = (viewportHeight - panelHeight) / 2;

        int buttonWidth = 140;

        int buttonHeight = 42;

        _okButton.Bounds = new Rectangle(

            panelX + (panelWidth - buttonWidth) / 2,

            panelY + panelHeight - 58,

            buttonWidth,

            buttonHeight);

    }

}

