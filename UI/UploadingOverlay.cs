#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>Modal "Uploading…" panel with Cancel. Blocks menu interaction while shown.</summary>
public sealed class UploadingOverlay
{
    private readonly Button _cancelButton = new("Cancel");
    private readonly UIFocusManager _focus = new();
    private readonly FocusableButton _cancelFocus;
    private bool _cancelRequested;

    public UploadingOverlay()
    {
        _cancelFocus = new FocusableButton(_cancelButton);
    }

    public bool CancelRequested => _cancelRequested;

    public void Reset()
    {
        _cancelRequested = false;
    }

    public void Update(GameTime gameTime, InputManager input, int viewportWidth, int viewportHeight)
    {
        Layout(viewportWidth, viewportHeight);
        _focus.Clear();
        _focus.Add(_cancelFocus, "Cancel");
        _focus.FinalizeFocus("Cancel");
        _focus.Update(gameTime, input);

        if (_cancelFocus.WasActivated || input.ExitPressed)
        {
            _cancelRequested = true;
        }
    }

    public void Draw(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        int viewportWidth,
        int viewportHeight,
        GameTime gameTime,
        InputManager input)
    {
        Layout(viewportWidth, viewportHeight);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), new Color(0, 0, 0, 170));

        int panelWidth = Math.Clamp(viewportWidth / 2, 360, 560);
        int panelHeight = 180;
        Rectangle panel = new((viewportWidth - panelWidth) / 2, (viewportHeight - panelHeight) / 2, panelWidth, panelHeight);
        spriteBatch.Draw(pixel, panel, new Color(38, 46, 62));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(255, 220, 80), 2);

        Rectangle titleBounds = new(panel.X + 16, panel.Y + 20, panel.Width - 32, 36);
        Rectangle messageBounds = new(panel.X + 16, panel.Y + 64, panel.Width - 32, 60);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "WORKSHOP", titleBounds, 3, Color.White);
        SimpleTextRenderer.DrawCentered(
            spriteBatch,
            pixel,
            _cancelRequested ? "Cancelling…" : "Uploading…",
            messageBounds,
            2,
            new Color(210, 218, 230));

        if (!_cancelRequested)
        {
            _cancelButton.Draw(spriteBatch, pixel);
            _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, input);
        }
    }

    private void Layout(int viewportWidth, int viewportHeight)
    {
        int panelWidth = Math.Clamp(viewportWidth / 2, 360, 560);
        int panelHeight = 180;
        int panelX = (viewportWidth - panelWidth) / 2;
        int panelY = (viewportHeight - panelHeight) / 2;
        const int buttonWidth = 140;
        const int buttonHeight = 42;
        _cancelButton.Bounds = new Rectangle(
            panelX + (panelWidth - buttonWidth) / 2,
            panelY + panelHeight - 58,
            buttonWidth,
            buttonHeight);
    }
}
