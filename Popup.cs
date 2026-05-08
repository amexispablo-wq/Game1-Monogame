#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public enum PopupType
{
    Confirmation,
    TextInput
}

public enum PopupResult
{
    Pending,
    Confirmed,
    Cancelled
}

public sealed class Popup
{
    private readonly PopupType _type;
    private readonly string _title;
    private readonly string _message;
    private readonly TextInputComponent? _textInput;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;
    private PopupResult _result;
    private double _fadeInTime;
    private const double FadeInDuration = 0.15;

    public string Title => _title;
    public string Message => _message;
    public TextInputComponent? TextInput => _textInput;
    public PopupResult Result => _result;
    public string InputValue => _textInput?.Text ?? string.Empty;
    public double FadeAlpha => Math.Min(_fadeInTime / FadeInDuration, 1.0);

    public Popup(string title, string message)
    {
        _type = PopupType.Confirmation;
        _title = title;
        _message = message;
        _textInput = null;
        _confirmButton = new Button("Confirm");
        _cancelButton = new Button("Cancel");
        _result = PopupResult.Pending;
        _fadeInTime = 0;
    }

    public Popup(string title, string message, string initialText)
    {
        _type = PopupType.TextInput;
        _title = title;
        _message = message;
        _textInput = new TextInputComponent(initialText);
        _confirmButton = new Button("Create");
        _cancelButton = new Button("Cancel");
        _result = PopupResult.Pending;
        _fadeInTime = 0;
    }

    public void Update(GameTime gameTime, InputManager input, int viewportWidth, int viewportHeight)
    {
        _fadeInTime += gameTime.ElapsedGameTime.TotalSeconds;

        LayoutPopup(viewportWidth, viewportHeight);

        if (_textInput != null)
        {
            _textInput.SetFocus(true);
            _textInput.Update(gameTime, input);
        }

        if (_confirmButton.Update(input) || input.EnterPressed)
        {
            _result = PopupResult.Confirmed;
        }
        else if (_cancelButton.Update(input))
        {
            _result = PopupResult.Cancelled;
        }

        // ESC to cancel
        if (input.ExitPressed)
        {
            _result = PopupResult.Cancelled;
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch, Texture2D pixel)
    {
        float alpha = (float)FadeAlpha;

        // Draw darkened overlay
        Color overlayColor = new Color(0, 0, 0, (int)(alpha * 200));
        spriteBatch.Draw(pixel, new Rectangle(0, 0, 99999, 99999), overlayColor);

        // Get layout info
        int popupWidth = 400;
        int popupHeight = _type == PopupType.TextInput ? 280 : 220;
        int popupX = (spriteBatch.GraphicsDevice.Viewport.Width - popupWidth) / 2;
        int popupY = (spriteBatch.GraphicsDevice.Viewport.Height - popupHeight) / 2;

        // Draw popup background
        Rectangle popupBounds = new(popupX, popupY, popupWidth, popupHeight);
        spriteBatch.Draw(pixel, popupBounds, new Color(35, 41, 55, (int)(alpha * 255)));
        DrawHelper.DrawBorder(spriteBatch, pixel, popupBounds, new Color(100, 110, 130, (int)(alpha * 255)), 3);

        // Draw title
        var titleBounds = new Rectangle(popupX + 20, popupY + 20, popupWidth - 40, 40);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, _title, titleBounds, 3, new Color(255, 255, 255, (int)(alpha * 255)));

        // Draw message
        var messageBounds = new Rectangle(popupX + 20, popupY + 65, popupWidth - 40, 60);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, _message, messageBounds, 2, new Color(200, 200, 200, (int)(alpha * 255)));

        // Draw text input if needed
        if (_textInput != null)
        {
            _textInput.IsFocused = true;
            _textInput.Bounds = new Rectangle(popupX + 20, popupY + 130, popupWidth - 40, 40);
            _textInput.Draw(spriteBatch, pixel);
        }

        // Draw buttons
        LayoutButtons(popupX, popupY, popupWidth, popupHeight, _type);
        _confirmButton.Draw(spriteBatch, pixel);
        _cancelButton.Draw(spriteBatch, pixel);
    }

    private void LayoutPopup(int viewportWidth, int viewportHeight)
    {
        // Already handled in Draw method
    }

    private void LayoutButtons(int popupX, int popupY, int popupWidth, int popupHeight, PopupType type)
    {
        const int buttonWidth = 160;
        const int buttonHeight = 40;
        const int gap = 20;
        int totalWidth = (buttonWidth * 2) + gap;
        int buttonsX = popupX + (popupWidth - totalWidth) / 2;
        int buttonsY = popupY + popupHeight - 60;

        _confirmButton.Bounds = new Rectangle(buttonsX, buttonsY, buttonWidth, buttonHeight);
        _cancelButton.Bounds = new Rectangle(buttonsX + buttonWidth + gap, buttonsY, buttonWidth, buttonHeight);
    }
}
