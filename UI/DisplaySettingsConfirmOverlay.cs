#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class DisplaySettingsConfirmOverlay
{
    public const float TimeoutSeconds = 15f;

    private readonly Button _confirmButton = new("Keep Changes");
    private readonly Button _cancelButton = new("Revert");
    private readonly UIFocusManager _focus = new();
    private readonly FocusableButton _confirmFocus;
    private readonly FocusableButton _cancelFocus;
    private float _secondsRemaining = TimeoutSeconds;

    public DisplaySettingsConfirmOverlay()
    {
        _confirmFocus = new FocusableButton(_confirmButton);
        _cancelFocus = new FocusableButton(_cancelButton);
        _confirmButton.TextScale = 3;
        _cancelButton.TextScale = 3;
        _confirmButton.FillColor = new Color(74, 111, 93);
        _confirmButton.HoverFillColor = new Color(94, 140, 116);
    }

    public float SecondsRemaining => _secondsRemaining;
    public bool IsExpired => _secondsRemaining <= 0f;
    public bool WasConfirmed { get; private set; }
    public bool WasCancelled { get; private set; }

    public void Update(GameTime gameTime, InputManager input, Viewport viewport)
    {
        _secondsRemaining = MathF.Max(0f, _secondsRemaining - (float)gameTime.ElapsedGameTime.TotalSeconds);

        Layout(viewport);
        _focus.Clear();
        int confirmIndex = _focus.Add(_confirmFocus, "KeepChanges");
        int cancelIndex = _focus.Add(_cancelFocus, "RevertChanges");
        _focus.Navigation.LinkHorizontal(confirmIndex, cancelIndex);
        _focus.FinalizeFocus("KeepChanges");
        _focus.Update(gameTime, input);

        if (_confirmFocus.WasActivated || input.MenuConfirmPressed)
        {
            WasConfirmed = true;
        }
        else if (_cancelFocus.WasActivated || input.MenuCancelPressed || IsExpired)
        {
            WasCancelled = true;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, GameTime gameTime, InputManager input)
    {
        Layout(viewport);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 170));

        int panelWidth = Math.Clamp(viewport.Width - 120, 420, 640);
        int panelHeight = 220;
        var panelBounds = new Rectangle(
            (viewport.Width - panelWidth) / 2,
            (viewport.Height - panelHeight) / 2,
            panelWidth,
            panelHeight);

        spriteBatch.Draw(pixel, panelBounds, new Color(34, 42, 58));
        DrawHelper.DrawBorder(spriteBatch, pixel, panelBounds, new Color(255, 226, 122), 3);

        var titleBounds = new Rectangle(panelBounds.X + 20, panelBounds.Y + 18, panelBounds.Width - 40, 32);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "KEEP DISPLAY SETTINGS?", titleBounds, 3, Color.White);

        int seconds = Math.Max(0, (int)MathF.Ceiling(_secondsRemaining));
        var messageBounds = new Rectangle(panelBounds.X + 20, panelBounds.Y + 58, panelBounds.Width - 40, 48);
        SimpleTextRenderer.DrawCentered(
            spriteBatch,
            pixel,
            $"Reverting in {seconds}s if not confirmed.",
            messageBounds,
            2,
            new Color(200, 210, 225));

        _confirmButton.Draw(spriteBatch, pixel);
        _cancelButton.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, input);
    }

    private void Layout(Viewport viewport)
    {
        int buttonWidth = 180;
        int buttonHeight = 46;
        int gap = 20;
        int totalWidth = (buttonWidth * 2) + gap;
        int startX = (viewport.Width - totalWidth) / 2;
        int buttonY = (viewport.Height / 2) + 36;

        _confirmButton.Bounds = new Rectangle(startX, buttonY, buttonWidth, buttonHeight);
        _cancelButton.Bounds = new Rectangle(startX + buttonWidth + gap, buttonY, buttonWidth, buttonHeight);
    }
}
