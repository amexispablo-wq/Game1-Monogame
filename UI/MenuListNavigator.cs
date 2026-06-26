using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ColorBlocks;

/// <summary>
/// Keyboard + gamepad navigation for vertical menu lists. Mouse hover overrides selection.
/// </summary>
public sealed class MenuListNavigator
{
    private const float StickRepeatDelaySeconds = 0.22f;

    private int _selectedIndex;
    private float _stickRepeatTimer;
    private bool _stickHeldLastFrame;

    public int SelectedIndex => _selectedIndex;

    public void Reset(int index = 0)
    {
        _selectedIndex = Math.Max(0, index);
        _stickRepeatTimer = 0f;
        _stickHeldLastFrame = false;
    }

    public void Update(GameTime gameTime, InputManager input, int itemCount, Rectangle[] itemBounds)
    {
        if (itemCount <= 0)
        {
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        for (int i = 0; i < itemCount && i < itemBounds.Length; i++)
        {
            if (itemBounds[i].Contains(input.MousePosition))
            {
                _selectedIndex = i;
                break;
            }
        }

        if (input.MenuMoveUpPressed)
        {
            _selectedIndex = (_selectedIndex - 1 + itemCount) % itemCount;
            _stickRepeatTimer = StickRepeatDelaySeconds;
        }
        else if (input.MenuMoveDownPressed)
        {
            _selectedIndex = (_selectedIndex + 1) % itemCount;
            _stickRepeatTimer = StickRepeatDelaySeconds;
        }
        else
        {
            bool stickUp = input.MenuStickUpHeld;
            bool stickDown = input.MenuStickDownHeld;
            bool stickHeld = stickUp || stickDown;

            if (stickHeld)
            {
                _stickRepeatTimer -= dt;
                if (!_stickHeldLastFrame || _stickRepeatTimer <= 0f)
                {
                    if (stickUp)
                    {
                        _selectedIndex = (_selectedIndex - 1 + itemCount) % itemCount;
                    }
                    else if (stickDown)
                    {
                        _selectedIndex = (_selectedIndex + 1) % itemCount;
                    }

                    _stickRepeatTimer = StickRepeatDelaySeconds;
                }
            }
            else
            {
                _stickRepeatTimer = 0f;
            }

            _stickHeldLastFrame = stickHeld;
        }

        _selectedIndex = Math.Clamp(_selectedIndex, 0, itemCount - 1);
    }

    public bool WasConfirmPressed(InputManager input) => input.MenuConfirmPressed;

    public bool WasCancelPressed(InputManager input) => input.MenuCancelPressed;
}
