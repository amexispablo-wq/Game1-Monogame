using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

public sealed class TextInputComponent
{
    private string _text = string.Empty;
    private double _caretBlinkTime;
    private const double CaretBlinkInterval = 0.5;
    private bool _isFocused;

    // Keyboard repeat tracking
    private KeyboardState _previousKeyboardState;
    private readonly Dictionary<Keys, double> _keyHoldTimers = new();
    private const double KeyInitialDelayMs = 400;  // Initial delay before repeat
    private const double KeyRepeatIntervalMs = 50;  // Interval between repeats

    public string Text
    {
        get => _text;
        set => _text = value ?? string.Empty;
    }

    public bool IsFocused
    {
        get => _isFocused;
        set => _isFocused = value;
    }

    public int MaxLength { get; set; } = 50;
    public Rectangle Bounds { get; set; }

    public TextInputComponent()
    {
        _text = string.Empty;
        _previousKeyboardState = Keyboard.GetState();
    }

    public TextInputComponent(string initialText)
    {
        _text = initialText ?? string.Empty;
        _previousKeyboardState = Keyboard.GetState();
    }

    public void Update(GameTime gameTime, InputManager input)
    {
        if (!_isFocused)
        {
            _previousKeyboardState = Keyboard.GetState();
            _keyHoldTimers.Clear();
            return;
        }

        _caretBlinkTime += gameTime.ElapsedGameTime.TotalSeconds;
        if (_caretBlinkTime > CaretBlinkInterval * 2)
        {
            _caretBlinkTime = 0;
        }

        HandleTextInputWithRepeat(gameTime);
        _previousKeyboardState = Keyboard.GetState();
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        // Draw background
        Color bgColor = _isFocused ? new Color(62, 71, 90) : new Color(52, 61, 80);
        spriteBatch.Draw(pixel, Bounds, bgColor);

        // Draw border
        Color borderColor = _isFocused ? new Color(100, 200, 255) : new Color(80, 90, 110);
        DrawHelper.DrawBorder(spriteBatch, pixel, Bounds, borderColor, 2);

        // Draw text
        Vector2 textPos = new(Bounds.X + 8, Bounds.Y + 6);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, _text, textPos, 2, Color.White);

        // Draw caret if focused and blinking
        if (_isFocused && _caretBlinkTime < CaretBlinkInterval)
        {
            int caretX = (int)textPos.X + (_text.Length * 12);
            spriteBatch.Draw(pixel, new Rectangle(caretX, (int)textPos.Y, 2, 14), Color.White);
        }
    }

    private void HandleTextInputWithRepeat(GameTime gameTime)
    {
        var currentKeyboard = Keyboard.GetState();
        double deltaMs = gameTime.ElapsedGameTime.TotalMilliseconds;

        foreach (Keys key in currentKeyboard.GetPressedKeys())
        {
            bool isNewPress = currentKeyboard.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
            bool isHeld = currentKeyboard.IsKeyDown(key) && _previousKeyboardState.IsKeyDown(key);

            if (isNewPress)
            {
                AddCharacterForKey(key, currentKeyboard);
                _keyHoldTimers[key] = 0;
            }
            else if (isHeld)
            {
                if (!_keyHoldTimers.ContainsKey(key))
                {
                    _keyHoldTimers[key] = 0;
                }

                _keyHoldTimers[key] += deltaMs;

                if (_keyHoldTimers[key] >= KeyInitialDelayMs)
                {
                    double excessTime = _keyHoldTimers[key] - KeyInitialDelayMs;
                    int repeatCount = (int)(excessTime / KeyRepeatIntervalMs);

                    if (repeatCount > 0)
                    {
                        AddCharacterForKey(key, currentKeyboard);
                        _keyHoldTimers[key] = KeyInitialDelayMs + (excessTime % KeyRepeatIntervalMs);
                    }
                }
            }
        }

        // Clean up released keys from timer dictionary
        var keysToRemove = new List<Keys>();
        foreach (var kvp in _keyHoldTimers)
        {
            if (currentKeyboard.IsKeyUp(kvp.Key))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _keyHoldTimers.Remove(key);
        }
    }

    private void AddCharacterForKey(Keys key, KeyboardState keyboardState)
    {
        if (key >= Keys.A && key <= Keys.Z)
        {
            if (_text.Length < MaxLength)
            {
                bool shift = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);
                char c = shift ? char.ToUpper((char)('a' + (key - Keys.A))) : char.ToLower((char)('a' + (key - Keys.A)));
                _text += c;
            }
        }
        else if (key >= Keys.D0 && key <= Keys.D9)
        {
            if (_text.Length < MaxLength)
            {
                _text += (char)('0' + (key - Keys.D0));
            }
        }
        else if (key == Keys.Space)
        {
            if (_text.Length < MaxLength)
            {
                _text += ' ';
            }
        }
        else if (key == Keys.Back && _text.Length > 0)
        {
            _text = _text.Substring(0, _text.Length - 1);
        }
    }

    public void SetFocus(bool focused)
    {
        _isFocused = focused;
        if (focused)
        {
            _caretBlinkTime = 0;
            _keyHoldTimers.Clear();
        }
    }
}

