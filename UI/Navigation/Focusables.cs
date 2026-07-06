using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class FocusableButton : IFocusable
{
    private readonly Button _button;

    public FocusableButton(Button button)
    {
        _button = button;
    }

    public Rectangle Bounds => _button.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CapturesNavigation => false;
    public bool IsEditing => false;
    public bool WasActivated { get; private set; }

    public bool HandleDirection(NavigationDirection direction) => false;

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        WasActivated = true;
        return true;
    }

    public bool OnCancel() => false;

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        WasActivated = false;
        if (Bounds.Contains(input.UiPointerPosition) && input.UiPointerPressed && IsEnabled)
        {
            WasActivated = true;
        }

        _button.Update(input, navigation);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableCycleSelector<T> : IFocusable where T : notnull
{
    private readonly CycleSelector<T> _selector;
    private readonly EditModeController<T> _edit = new();

    public FocusableCycleSelector(CycleSelector<T> selector)
    {
        _selector = selector;
    }

    public Rectangle Bounds => _selector.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CapturesNavigation => _edit.IsEditing;
    public bool IsEditing => _edit.IsEditing;

    public bool HandleDirection(NavigationDirection direction)
    {
        if (!_edit.IsEditing || _selector.Options.Count == 0)
        {
            return false;
        }

        if (direction == NavigationDirection.Left)
        {
            Cycle(-1);
            return true;
        }

        if (direction == NavigationDirection.Right)
        {
            Cycle(1);
            return true;
        }

        return false;
    }

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (_edit.IsEditing)
        {
            _edit.Confirm();
            return true;
        }

        _edit.BeginEdit(_selector.CurrentOption);
        return true;
    }

    public bool OnCancel()
    {
        if (!_edit.IsEditing)
        {
            return false;
        }

        _edit.Cancel(value => _selector.CurrentOption = value);
        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _selector.Update(input, navigation);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
        if (_edit.IsEditing)
        {
            Rectangle editBadge = new(Bounds.Right - 72, Bounds.Y - 14, 68, 16);
            spriteBatch.Draw(pixel, editBadge, new Color(72, 120, 200, 200));
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "EDIT", editBadge, 1, Color.White);
        }
    }

    private void Cycle(int direction)
    {
        int current = _selector.Options.IndexOf(_selector.CurrentOption);
        int next = (current + direction + _selector.Options.Count) % _selector.Options.Count;
        _selector.CurrentOption = _selector.Options[next];
    }
}

public sealed class FocusableCheckbox : IFocusable
{
    private readonly Checkbox _checkbox;

    public FocusableCheckbox(Checkbox checkbox)
    {
        _checkbox = checkbox;
    }

    public Rectangle Bounds => _checkbox.Bounds;
    public bool IsEnabled => _checkbox.IsEnabled;
    public bool CapturesNavigation => false;
    public bool IsEditing => false;

    public bool HandleDirection(NavigationDirection direction) => false;

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        _checkbox.IsChecked = !_checkbox.IsChecked;
        return true;
    }

    public bool OnCancel() => false;

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _checkbox.Update(input, navigation);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableSlider : IFocusable
{
    private readonly Slider _slider;
    private readonly EditModeController<float> _edit = new();

    public FocusableSlider(Slider slider)
    {
        _slider = slider;
    }

    public Rectangle Bounds => _slider.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CapturesNavigation => _edit.IsEditing;
    public bool IsEditing => _edit.IsEditing;

    public bool HandleDirection(NavigationDirection direction)
    {
        if (!_edit.IsEditing)
        {
            return false;
        }

        if (direction is NavigationDirection.Left or NavigationDirection.Right)
        {
            int stepDirection = direction == NavigationDirection.Left ? -1 : 1;
            float range = _slider.MaxValue - _slider.MinValue;
            float step = range * 0.05f;
            _slider.Value = MathHelper.Clamp(_slider.Value + (stepDirection * step), _slider.MinValue, _slider.MaxValue);
            return true;
        }

        return false;
    }

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (_edit.IsEditing)
        {
            _edit.Confirm();
            return true;
        }

        _edit.BeginEdit(_slider.Value);
        return true;
    }

    public bool OnCancel()
    {
        if (!_edit.IsEditing)
        {
            return false;
        }

        _edit.Cancel(value => _slider.Value = value);
        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _slider.Update(input, navigation);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
        if (_edit.IsEditing)
        {
            Rectangle editBadge = new(Bounds.Right - 72, Bounds.Y - 14, 68, 16);
            spriteBatch.Draw(pixel, editBadge, new Color(72, 120, 200, 200));
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "EDIT", editBadge, 1, Color.White);
        }
    }
}

public sealed class FocusableAction : IFocusable
{
    private readonly Func<Rectangle> _boundsProvider;
    private readonly Func<bool> _onConfirm;

    public FocusableAction(Rectangle bounds, Func<bool> onConfirm)
        : this(() => bounds, onConfirm)
    {
    }

    public FocusableAction(Func<Rectangle> boundsProvider, Func<bool> onConfirm)
    {
        _boundsProvider = boundsProvider;
        _onConfirm = onConfirm;
    }

    public Rectangle Bounds => _boundsProvider();
    public bool IsEnabled { get; set; } = true;
    public bool CapturesNavigation => false;
    public bool IsEditing => false;
    public bool WasActivated { get; private set; }

    public bool HandleDirection(NavigationDirection direction) => false;

    public bool OnConfirm()
    {
        WasActivated = _onConfirm();
        return WasActivated;
    }

    public bool OnCancel() => false;

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        WasActivated = false;
        if (Bounds.Contains(input.UiPointerPosition) && input.UiPointerPressed)
        {
            OnConfirm();
        }
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableCycleMemberInput : IFocusable
{
    private readonly PartyMember _member;
    private readonly Action<PartyMember, int> _cycle;

    public FocusableCycleMemberInput(Rectangle bounds, PartyMember member, Action<PartyMember, int> cycle)
    {
        Bounds = bounds;
        _member = member;
        _cycle = cycle;
    }

    public Rectangle Bounds { get; }
    public bool IsEnabled => _member.IsLocallyOwned;
    public bool CapturesNavigation => false;
    public bool IsEditing => false;

    public bool HandleDirection(NavigationDirection direction)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (direction == NavigationDirection.Left)
        {
            _cycle(_member, -1);
            return true;
        }

        if (direction == NavigationDirection.Right)
        {
            _cycle(_member, 1);
            return true;
        }

        return false;
    }

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        _cycle(_member, 1);
        return true;
    }

    public bool OnCancel() => false;

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        if (Bounds.Contains(input.UiPointerPosition) && input.UiPointerPressed && IsEnabled)
        {
            _cycle(_member, 1);
        }
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableResolutionDropdown : IFocusable
{
    private readonly ResolutionDropdown _dropdown;
    private Resolution? _cancelSelection;

    public FocusableResolutionDropdown(ResolutionDropdown dropdown)
    {
        _dropdown = dropdown;
    }

    public Rectangle Bounds => _dropdown.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CapturesNavigation => _dropdown.IsExpanded;
    public bool IsEditing => _dropdown.IsExpanded;

    public bool HandleDirection(NavigationDirection direction)
    {
        if (!_dropdown.IsExpanded || _dropdown.Resolutions.Count == 0)
        {
            return false;
        }

        if (direction is NavigationDirection.Up or NavigationDirection.Down)
        {
            int step = direction == NavigationDirection.Up ? -1 : 1;
            int current = _dropdown.HighlightedIndex ?? GetSelectedIndex();
            int next = (current + step + _dropdown.Resolutions.Count) % _dropdown.Resolutions.Count;
            _dropdown.HighlightedIndex = next;
            return true;
        }

        return false;
    }

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (!_dropdown.IsExpanded)
        {
            _cancelSelection = _dropdown.SelectedResolution;
            _dropdown.IsExpanded = true;
            _dropdown.HighlightedIndex = GetSelectedIndex();
            return true;
        }

        if (_dropdown.HighlightedIndex is int index && index >= 0 && index < _dropdown.Resolutions.Count)
        {
            _dropdown.SelectedResolution = _dropdown.Resolutions[index];
        }

        _dropdown.IsExpanded = false;
        _dropdown.HighlightedIndex = null;
        _cancelSelection = null;
        return true;
    }

    public bool OnCancel()
    {
        if (!_dropdown.IsExpanded)
        {
            return false;
        }

        _dropdown.SelectedResolution = _cancelSelection;
        _dropdown.IsExpanded = false;
        _dropdown.HighlightedIndex = null;
        _cancelSelection = null;
        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _dropdown.Update(input, isFocused);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }

    private int GetSelectedIndex()
    {
        if (_dropdown.SelectedResolution is null)
        {
            return 0;
        }

        int index = _dropdown.Resolutions.FindIndex(r => r.Equals(_dropdown.SelectedResolution));
        return index < 0 ? 0 : index;
    }
}

public sealed class FocusableDropdown<T> : IFocusable where T : notnull
{
    private readonly Dropdown<T> _dropdown;
    private T? _cancelSelection;

    public FocusableDropdown(Dropdown<T> dropdown)
    {
        _dropdown = dropdown;
    }

    public Rectangle Bounds => _dropdown.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CapturesNavigation => _dropdown.IsExpanded;
    public bool IsEditing => _dropdown.IsExpanded;

    public bool HandleDirection(NavigationDirection direction)
    {
        if (!_dropdown.IsExpanded || _dropdown.Options.Count == 0)
        {
            return false;
        }

        if (direction is NavigationDirection.Up or NavigationDirection.Down)
        {
            int step = direction == NavigationDirection.Up ? -1 : 1;
            int current = _dropdown.HighlightedIndex ?? Math.Max(0, _dropdown.Options.IndexOf(_dropdown.SelectedOption!));
            int next = (current + step + _dropdown.Options.Count) % _dropdown.Options.Count;
            _dropdown.HighlightedIndex = next;
            return true;
        }

        return false;
    }

    public bool OnConfirm()
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (!_dropdown.IsExpanded)
        {
            _cancelSelection = _dropdown.SelectedOption;
            _dropdown.IsExpanded = true;
            return true;
        }

        if (_dropdown.HighlightedIndex is int index && index >= 0 && index < _dropdown.Options.Count)
        {
            _dropdown.SelectedOption = _dropdown.Options[index];
        }

        _dropdown.IsExpanded = false;
        _dropdown.HighlightedIndex = null;
        _cancelSelection = default;
        return true;
    }

    public bool OnCancel()
    {
        if (!_dropdown.IsExpanded)
        {
            return false;
        }

        _dropdown.SelectedOption = _cancelSelection;
        _dropdown.IsExpanded = false;
        _dropdown.HighlightedIndex = null;
        _cancelSelection = default;
        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _dropdown.Update(input, isFocused);
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}

public sealed class FocusableTextInput : IFocusable
{
    private readonly TextInputComponent _input;
    private bool _hasNavigationFocus;

    public FocusableTextInput(TextInputComponent input)
    {
        _input = input;
    }

    public Rectangle Bounds => _input.Bounds;
    public bool IsEnabled { get; set; } = true;
    public bool CapturesNavigation => _input.IsFocused;
    public bool IsEditing => _input.IsFocused;

    public bool HandleDirection(NavigationDirection direction) => false;

    public bool OnConfirm()
    {
        _input.IsFocused = true;
        return true;
    }

    public bool OnCancel()
    {
        if (!_input.IsFocused)
        {
            return false;
        }

        _input.IsFocused = false;
        return true;
    }

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        _hasNavigationFocus = isFocused;
        if (!isFocused)
        {
            _input.IsFocused = false;
        }

        if (input.UiPointerPressed && Bounds.Contains(input.UiPointerPosition))
        {
            _input.IsFocused = true;
        }
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        if (_hasNavigationFocus || _input.IsFocused)
        {
            FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
        }
    }
}

public sealed class FocusableGridCell : IFocusable
{
    private readonly Func<bool> _onConfirm;

    public FocusableGridCell(Rectangle bounds, Func<bool> onConfirm)
    {
        Bounds = bounds;
        _onConfirm = onConfirm;
    }

    public Rectangle Bounds { get; }
    public bool IsEnabled { get; set; } = true;
    public bool CapturesNavigation => false;
    public bool IsEditing => false;
    public bool WasActivated { get; private set; }

    public bool HandleDirection(NavigationDirection direction) => false;

    public bool OnConfirm()
    {
        WasActivated = _onConfirm();
        return WasActivated;
    }

    public bool OnCancel() => false;

    public void Update(InputManager input, InputNavigationService navigation, bool isFocused)
    {
        WasActivated = false;
        if (Bounds.Contains(input.UiPointerPosition) && input.UiPointerPressed)
        {
            OnConfirm();
        }
    }

    public void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        FocusHighlight.Draw(spriteBatch, pixel, Bounds, gameTime.TotalGameTime.TotalSeconds);
    }
}
