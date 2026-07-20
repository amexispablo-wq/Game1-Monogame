using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public enum PauseMenuChoice
{
    Resume,
    Respawn,
    RestartLevel,
    BackToMenu
}

public sealed class PauseMenuOverlay
{
    private static readonly string[] OptionLabels =
    {
        "RESUME",
        "RESPAWN",
        "RESTART LEVEL",
        "BACK TO MENU"
    };

    private readonly UIFocusManager _focus = new() { Name = "Pause Menu" };
    private readonly List<FocusableGridCell> _optionFocusables = new();
    private float _openAnim;
    private Rectangle[] _optionBounds = Array.Empty<Rectangle>();
    private Rectangle _panelBounds;

    public bool IsOpen { get; private set; }

    private static readonly string[] OptionIds =
    {
        "Resume", "Respawn", "RestartLevel", "BackToMenu"
    };

    private static string OptionId(int index) =>
        index >= 0 && index < OptionIds.Length ? OptionIds[index] : $"PauseOption{index}";

    public void Open()
    {
        IsOpen = true;
        _openAnim = 0f;
        _focus.ResetFocus();
    }

    public void Close()
    {
        IsOpen = false;
    }

    public PauseMenuChoice? Update(GameTime gameTime, InputManager input, Viewport viewport)
    {
        if (!IsOpen)
        {
            return null;
        }

        _openAnim = MathHelper.Clamp(_openAnim + (float)gameTime.ElapsedGameTime.TotalSeconds * 5f, 0f, 1f);
        Layout(viewport);

        if (input.GameplayPausePressed || input.MenuCancelPressed)
        {
            Close();
            return PauseMenuChoice.Resume;
        }

        _optionFocusables.Clear();
        _focus.Clear();
        var optionIndices = new List<int>();
        for (int i = 0; i < _optionBounds.Length; i++)
        {
            var option = new FocusableGridCell(_optionBounds[i], () => true);
            _optionFocusables.Add(option);
            optionIndices.Add(_focus.Add(option, OptionId(i)));
        }

        _focus.Navigation.WireVerticalChain(optionIndices);
        _focus.FinalizeFocus(OptionId(1));

        _focus.Update(gameTime, input);

        for (int i = 0; i < _optionFocusables.Count; i++)
        {
            bool activated = _optionFocusables[i].WasActivated;
            bool confirmed = _focus.Focused == _optionFocusables[i]
                && ((input.Navigation.IsKeyboardActive && input.KeyboardMenuConfirmPressed)
                    || (input.Navigation.IsGamepadActive && input.GamepadMenuConfirmPressed));

            if (activated || confirmed)
            {
                return ResolveChoice(i);
            }
        }

        return null;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime, Viewport viewport, InputManager input)
    {
        if (!IsOpen)
        {
            return;
        }

        Layout(viewport);

        float fade = EaseOut(_openAnim);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * (0.55f * fade));

        Color panelFill = new Color(22, 26, 34) * (0.94f * fade);
        spriteBatch.Draw(pixel, _panelBounds, panelFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, _panelBounds, new Color(120, 140, 170) * fade, Math.Max(2, _panelBounds.Height / 80));

        Rectangle titleBounds = new(_panelBounds.X + 20, _panelBounds.Y + 18, _panelBounds.Width - 40, 40);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "PAUSED", titleBounds, GetTitleScale(viewport), Color.White * fade);

        for (int i = 0; i < OptionLabels.Length && i < _optionBounds.Length; i++)
        {
            bool selected = i < _optionFocusables.Count && _focus.Focused == _optionFocusables[i];
            bool mouseHover = input.Navigation.AllowPointerHoverVisual
                && _optionBounds[i].Contains(input.UiPointerPosition);
            DrawOption(spriteBatch, pixel, _optionBounds[i], OptionLabels[i], selected || mouseHover, fade);
        }

        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, input);
    }

    private void Layout(Viewport viewport)
    {
        int margin = Math.Max(16, viewport.Width / 24);
        int panelWidth = Math.Min(520, Math.Max(280, viewport.Width - (margin * 2)));
        int buttonHeight = Math.Clamp(viewport.Height / 14, 44, 58);
        int gap = Math.Max(10, buttonHeight / 5);
        int titleArea = Math.Max(56, buttonHeight + 10);
        int panelHeight = titleArea + (OptionLabels.Length * buttonHeight) + ((OptionLabels.Length - 1) * gap) + 28;

        int panelX = (viewport.Width - panelWidth) / 2;
        int panelY = (viewport.Height - panelHeight) / 2;
        _panelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);

        _optionBounds = new Rectangle[OptionLabels.Length];
        int y = panelY + titleArea;
        int buttonX = panelX + 24;
        int buttonWidth = panelWidth - 48;

        for (int i = 0; i < OptionLabels.Length; i++)
        {
            _optionBounds[i] = new Rectangle(buttonX, y, buttonWidth, buttonHeight);
            y += buttonHeight + gap;
        }
    }

    private static void DrawOption(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, string label, bool highlighted, float fade)
    {
        Color fill = highlighted ? new Color(58, 68, 88) : new Color(46, 56, 76);
        Color border = highlighted ? new Color(140, 160, 190) : new Color(105, 121, 150);
        Color text = highlighted ? Color.White : new Color(210, 220, 235);

        spriteBatch.Draw(pixel, bounds, fill * fade);
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, border * fade, highlighted ? 2 : 2);

        int scale = Math.Clamp(bounds.Height / 22, 2, 3);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, label, bounds, scale, text * fade);
    }

    private static PauseMenuChoice ResolveChoice(int index)
    {
        return index switch
        {
            0 => PauseMenuChoice.Resume,
            1 => PauseMenuChoice.Respawn,
            2 => PauseMenuChoice.RestartLevel,
            3 => PauseMenuChoice.BackToMenu,
            _ => PauseMenuChoice.Resume
        };
    }

    private static int GetTitleScale(Viewport viewport) => Math.Clamp(viewport.Height / 120, 3, 6);

    private static float EaseOut(float t) => 1f - MathF.Pow(1f - t, 3f);
}
