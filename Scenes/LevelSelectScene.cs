#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public enum LevelSelectMode
{
    PlayMode,
    EditMode
}

public sealed class LevelSelectScene : IScene
{
    private static RopeGameplayMode s_selectedRopeMode = RopeGameplayMode.ColoredPhysics;

    private readonly ColorBlocksGame _game;
    private readonly LevelSelectMode _mode;
    private IReadOnlyList<LevelMetadata> _levels = new List<LevelMetadata>();
    private GridLayout _gridLayout = null!;
    private int? _selectedIndex;
    private Popup? _popup;

    // UI
    private readonly Button _backButton = new("Back") { TextScale = 2 };
    private Button? _primaryButton;
    private Button? _secondaryButton;
    private Button? _tertiaryButton;
    private CycleSelector<RopeGameplayMode>? _ropeModeSelector;
    private Rectangle _ropeModePanelBounds;
    private Rectangle _ropeModeLabelBounds;
    private Rectangle _ropeModeDescriptionBounds;

    // Constants
    private const int CellWidth = 200;
    private const int CellHeight = 140;
    private const int HorizontalGap = 20;
    private const int VerticalGap = 20;

    public LevelSelectScene(ColorBlocksGame game, LevelSelectMode mode)
    {
        _game = game;
        _mode = mode;
        RefreshLevelList();
        InitializeButtons();
    }

    private void RefreshLevelList()
    {
        _levels = LevelManager.GetAllLevels();
        if (_levels.Count == 0)
        {
            // Create first level if none exist
            LevelManager.CreateNewLevel("Level 1");
            _levels = LevelManager.GetAllLevels();
        }

        _gridLayout = GridLayout.Create(_levels.Count, _game.Viewport.Width, GetGridLayoutHeight(), CellWidth, CellHeight, HorizontalGap, VerticalGap);
    }

    private void InitializeButtons()
    {
        _primaryButton = _mode switch
        {
            LevelSelectMode.PlayMode => new Button("Play"),
            LevelSelectMode.EditMode => new Button("Edit"),
            _ => new Button("OK")
        };

        _secondaryButton = _mode switch
        {
            LevelSelectMode.PlayMode => new Button("Delete Highscore"),
            LevelSelectMode.EditMode => new Button("Delete"),
            _ => new Button("Cancel")
        };

        _tertiaryButton = _mode == LevelSelectMode.EditMode ? new Button("Create New") : null;

        if (_mode == LevelSelectMode.PlayMode)
        {
            _ropeModeSelector = new CycleSelector<RopeGameplayMode>(
                new List<RopeGameplayMode>
                {
                    RopeGameplayMode.ColoredPhysics,
                    RopeGameplayMode.Neutral
                },
                mode => mode.ToDisplayName())
            {
                CurrentOption = s_selectedRopeMode
            };
        }
    }

    public void Update(GameTime gameTime)
    {
        LayoutButtons();

        // Update popup if active
        if (_popup != null)
        {
            _popup.Update(gameTime, _game.Input, _game.Viewport.Width, _game.Viewport.Height);

            if (_popup.Result == PopupResult.Confirmed)
            {
                HandlePopupConfirmed();
                _popup = null;
            }
            else if (_popup.Result == PopupResult.Cancelled)
            {
                _popup = null;
            }

            return;
        }

        // Back button
        if (_backButton.Update(_game.Input) || _game.Input.ExitPressed)
        {
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        if (_ropeModeSelector != null)
        {
            _ropeModeSelector.Update(_game.Input);
            s_selectedRopeMode = _ropeModeSelector.CurrentOption;
        }

        // Handle level grid clicks
        if (_game.Input.LeftMousePressed && !IsMouseOverButtons())
        {
            int? cellIndex = _gridLayout.GetCellAtPoint(_game.Input.MousePosition);
            if (cellIndex.HasValue && cellIndex.Value < _levels.Count)
            {
                _selectedIndex = cellIndex.Value;
            }
        }

        // Handle primary button (Play / Edit)
        if (_primaryButton != null && _primaryButton.Update(_game.Input))
        {
            if (_selectedIndex.HasValue)
            {
                HandlePrimaryAction();
            }
        }

        // Handle secondary button (Delete Highscore / Delete)
        if (_secondaryButton != null && _secondaryButton.Update(_game.Input))
        {
            if (_selectedIndex.HasValue)
            {
                HandleSecondaryAction();
            }
        }

        // Handle tertiary button (Create New - Edit Mode only)
        if (_tertiaryButton != null && _tertiaryButton.Update(_game.Input))
        {
            HandleCreateNew();
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutButtons();
        RefreshGridLayout();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        Viewport viewport = _game.Viewport;
        Texture2D pixel = _game.Pixel;

        // Draw background
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(29, 34, 45));

        // Draw title
        string title = _mode == LevelSelectMode.PlayMode ? "SELECT A LEVEL TO PLAY" : "LEVEL EDITOR";
        Rectangle titleBounds = new(20, 20, viewport.Width - 40, 50);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, title, titleBounds, 3, Color.White);

        // Draw level grid
        DrawLevelGrid(spriteBatch, pixel);
        DrawRopeModeSelector(spriteBatch, pixel, viewport);

        // Draw buttons
        spriteBatch.Draw(pixel, new Rectangle(0, viewport.Height - 100, viewport.Width, 100), new Color(22, 26, 34));
        _backButton.Draw(spriteBatch, pixel);
        _primaryButton?.Draw(spriteBatch, pixel);
        _secondaryButton?.Draw(spriteBatch, pixel);
        _tertiaryButton?.Draw(spriteBatch, pixel);

        spriteBatch.End();

        // Draw popup on top (needs separate begin/end for proper layering)
        if (_popup != null)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _popup.Draw(gameTime, spriteBatch, pixel);
            spriteBatch.End();
        }
    }

    private void DrawLevelGrid(SpriteBatch spriteBatch, Texture2D pixel)
    {
        for (int i = 0; i < _levels.Count && i < _gridLayout.CellBounds.Length; i++)
        {
            Rectangle cellBounds = _gridLayout.CellBounds[i];
            LevelMetadata level = _levels[i];

            // Draw cell background
            bool isSelected = _selectedIndex == i;
            bool isHovered = cellBounds.Contains(_game.Input.MousePosition);
            Color cellColor = isSelected ? new Color(74, 120, 180) : (isHovered ? new Color(62, 71, 90) : new Color(52, 61, 80));
            spriteBatch.Draw(pixel, cellBounds, cellColor);

            // Draw border
            Color borderColor = isSelected ? new Color(100, 180, 255) : new Color(80, 90, 110);
            int borderThickness = isSelected ? 4 : 2;
            DrawHelper.DrawBorder(spriteBatch, pixel, cellBounds, borderColor, borderThickness);

            // Draw level name
            Rectangle nameRect = new(cellBounds.X + 10, cellBounds.Y + 15, cellBounds.Width - 20, 40);
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, level.Name, nameRect, 2, Color.White);

            // Draw best time (PlayMode only)
            if (_mode == LevelSelectMode.PlayMode)
            {
                string timeText = GetBestTimeText(level.Id);
                Rectangle timeRect = new(cellBounds.X + 10, cellBounds.Y + 65, cellBounds.Width - 20, 30);
                SimpleTextRenderer.DrawCentered(spriteBatch, pixel, timeText, timeRect, 1, new Color(180, 200, 220));
            }
        }
    }

    private string GetBestTimeText(string levelId)
    {
        if (BestTimeStorage.TryGetBestTime(levelId, out float bestTime))
        {
            TimeSpan ts = TimeSpan.FromSeconds(bestTime);
            return $"Best: {ts.Minutes:00}:{ts.Seconds:00}:{(int)(ts.Milliseconds / 10):00}";
        }

        return "Best: --";
    }

    private void LayoutButtons()
    {
        Viewport viewport = _game.Viewport;
        const int buttonHeight = 50;
        const int horizontalPadding = 16;
        const int buttonGap = 15;
        const int bottomMargin = 25;

        // Layout back button separately (left side)
        _backButton.Bounds = new Rectangle(25, viewport.Height - buttonHeight - bottomMargin, 120, buttonHeight);

        if (_mode == LevelSelectMode.PlayMode && _ropeModeSelector != null)
        {
            int stripY = Math.Max(78, viewport.Height - 190);
            int stripBottom = viewport.Height - 100;
            int selectorWidth = Math.Min(380, Math.Max(1, viewport.Width - 40));
            int selectorHeight = 46;
            int selectorY = Math.Max(86, viewport.Height - 172);
            _ropeModePanelBounds = new Rectangle(0, stripY, viewport.Width, Math.Max(0, stripBottom - stripY));
            _ropeModeLabelBounds = new Rectangle(20, selectorY - 22, Math.Max(1, viewport.Width - 40), 18);
            _ropeModeSelector.Bounds = new Rectangle((viewport.Width - selectorWidth) / 2, selectorY, selectorWidth, selectorHeight);
            _ropeModeDescriptionBounds = new Rectangle(20, selectorY + selectorHeight + 8, Math.Max(1, viewport.Width - 40), 18);
        }

        if (_mode == LevelSelectMode.PlayMode)
        {
            // Play | Delete Highscore (centered)
            var layout = ButtonRowLayout.Create(
                new[] { "Play", "Delete Highscore" },
                viewport.Width, viewport.Height,
                buttonHeight, horizontalPadding, 12, buttonGap, bottomMargin);

            if (layout.ButtonBounds.Length >= 2)
            {
                _primaryButton!.Bounds = layout.ButtonBounds[0];
                _secondaryButton!.Bounds = layout.ButtonBounds[1];
            }
        }
        else
        {
            // Edit | Delete | Create New (centered)
            var layout = ButtonRowLayout.Create(
                new[] { "Edit", "Delete", "Create New" },
                viewport.Width, viewport.Height,
                buttonHeight, horizontalPadding, 12, buttonGap, bottomMargin);

            if (layout.ButtonBounds.Length >= 3)
            {
                _primaryButton!.Bounds = layout.ButtonBounds[0];
                _secondaryButton!.Bounds = layout.ButtonBounds[1];
                _tertiaryButton!.Bounds = layout.ButtonBounds[2];
            }
        }
    }

    private void RefreshGridLayout()
    {
        _gridLayout = GridLayout.Create(_levels.Count, _game.Viewport.Width, GetGridLayoutHeight(), CellWidth, CellHeight, HorizontalGap, VerticalGap);
    }

    private bool IsMouseOverButtons()
    {
        if (_backButton.Bounds.Contains(_game.Input.MousePosition))
            return true;
        if (_primaryButton?.Bounds.Contains(_game.Input.MousePosition) ?? false)
            return true;
        if (_secondaryButton?.Bounds.Contains(_game.Input.MousePosition) ?? false)
            return true;
        if (_tertiaryButton?.Bounds.Contains(_game.Input.MousePosition) ?? false)
            return true;
        if (_ropeModePanelBounds.Contains(_game.Input.MousePosition))
            return true;
        if (_ropeModeSelector?.Bounds.Contains(_game.Input.MousePosition) ?? false)
            return true;

        return false;
    }

    private void HandlePrimaryAction()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
            return;

        string levelId = _levels[_selectedIndex.Value].Id;

        if (_mode == LevelSelectMode.PlayMode)
        {
            s_selectedRopeMode = _ropeModeSelector?.CurrentOption ?? s_selectedRopeMode;
            _game.ChangeScene(new GameScene(_game, levelId, s_selectedRopeMode));
        }
        else
        {
            _game.ChangeScene(new EditorScene(_game, levelId));
        }
    }

    private void HandleSecondaryAction()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
            return;

        LevelMetadata level = _levels[_selectedIndex.Value];

        if (_mode == LevelSelectMode.PlayMode)
        {
            // Show delete highscore confirmation
            _popup = new Popup("Delete Highscore", $"Delete best time for '{level.Name}'?");
        }
        else
        {
            // Show delete level confirmation
            _popup = new Popup("Delete Level", $"Delete '{level.Name}' permanently?");
        }
    }

    private void HandleCreateNew()
    {
        // Show create new level dialog with text input
        _popup = new Popup("Create New Level", "Enter level name:", "New Level");
    }

    private void HandlePopupConfirmed()
    {
        if (_popup == null)
            return;

        if (_mode == LevelSelectMode.PlayMode && _selectedIndex.HasValue)
        {
            // Delete highscore
            LevelMetadata level = _levels[_selectedIndex.Value];
            BestTimeStorage.ResetLevelRecord(level.Id);
            _selectedIndex = null;
        }
        else if (_mode == LevelSelectMode.EditMode)
        {
            if (_popup.TextInput != null)
            {
                // Create new level
                string levelName = _popup.InputValue.Trim();
                if (!string.IsNullOrEmpty(levelName))
                {
                    string newLevelId = LevelManager.CreateNewLevel(levelName);
                    RefreshLevelList();

                    // Auto-select the newly created level
                    int newIndex = -1;
                    for (int i = 0; i < _levels.Count; i++)
                    {
                        if (_levels[i].Id == newLevelId)
                        {
                            newIndex = i;
                            break;
                        }
                    }

                    if (newIndex >= 0)
                    {
                        _selectedIndex = newIndex;
                    }
                }
            }
            else if (_selectedIndex.HasValue)
            {
                // Delete level
                LevelMetadata level = _levels[_selectedIndex.Value];
                LevelManager.DeleteLevel(level.Id);
                RefreshLevelList();
                _selectedIndex = null;
            }
        }
    }

    public void OnExit()
    {
    }

    private int GetGridLayoutHeight()
    {
        return _mode == LevelSelectMode.PlayMode
            ? Math.Max(240, _game.Viewport.Height - 160)
            : _game.Viewport.Height;
    }

    private void DrawRopeModeSelector(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        if (_mode != LevelSelectMode.PlayMode || _ropeModeSelector == null)
        {
            return;
        }

        if (_ropeModePanelBounds.Height > 0)
        {
            spriteBatch.Draw(pixel, _ropeModePanelBounds, new Color(27, 32, 43));
        }

        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "ROPE MODE", _ropeModeLabelBounds, 1, new Color(184, 196, 216));
        _ropeModeSelector.Draw(spriteBatch, pixel);

        string description = _ropeModeSelector.CurrentOption.ToDescription();
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, description, _ropeModeDescriptionBounds, 1, new Color(180, 200, 220));
    }
}
