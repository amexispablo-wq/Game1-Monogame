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
    private static bool s_lavaRiseEnabled;

    private readonly ColorBlocksGame _game;
    private readonly LevelSelectMode _mode;
    private IReadOnlyList<LevelMetadata> _levels = new List<LevelMetadata>();
    private GridLayout _gridLayout = null!;
    private int? _selectedIndex;
    private Popup? _popup;
    private bool _subscribedToLevelStart;

    // UI
    private readonly Button _backButton = new("Back") { TextScale = 2 };
    private readonly UIFocusManager _focus = new();
    private readonly FocusableButton _backFocus;
    private readonly List<FocusableGridCell> _gridFocusables = new();
    private FocusableButton? _primaryFocus;
    private FocusableButton? _secondaryFocus;
    private FocusableButton? _tertiaryFocus;
    private FocusableButton? _quaternaryFocus;
    private FocusableCycleSelector<RopeGameplayMode>? _ropeModeFocus;
    private FocusableCheckbox? _lavaRiseFocus;
    private Button? _primaryButton;
    private Button? _secondaryButton;
    private Button? _tertiaryButton;
    private Button? _quaternaryButton;
    private CycleSelector<RopeGameplayMode>? _ropeModeSelector;
    private Checkbox? _lavaRiseCheckbox;
    private Rectangle _ropeModePanelBounds;
    private Rectangle _ropeModeLabelBounds;
    private Rectangle _ropeModeDescriptionBounds;
    private Rectangle _detailsPanelBounds;
    private Level? _selectedLevel;
    private Texture2D? _selectedLevelPreview;
    private string? _selectedLevelId;

    // Constants
    private const int CellWidth = 200;
    private const int CellHeight = 140;
    private const int HorizontalGap = 20;
    private const int VerticalGap = 20;

    public LevelSelectScene(ColorBlocksGame game, LevelSelectMode mode)
    {
        _game = game;
        _mode = mode;
        _backFocus = new FocusableButton(_backButton);
        _focus.ResetFocus();
        RefreshLevelList();
        InitializeButtons();
        if (_mode == LevelSelectMode.PlayMode)
        {
            _game.SteamLobby.LevelStartReceived += OnLevelStartReceived;
            _subscribedToLevelStart = true;
        }
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

        _selectedIndex = null;
        _selectedLevel = null;
        _selectedLevelPreview = null;
        _selectedLevelId = null;

        _gridLayout = GridLayout.Create(_levels.Count, _game.Viewport.Width, GetGridLayoutHeight(), CellWidth, CellHeight, HorizontalGap, VerticalGap);
    }

    private void InitializeButtons()
    {
        _primaryButton = _mode switch
        {
            LevelSelectMode.PlayMode => new Button("Play"),
            LevelSelectMode.EditMode => new Button("Edit Level"),
            _ => new Button("OK")
        };

        _secondaryButton = _mode switch
        {
            LevelSelectMode.PlayMode => new Button("Delete Highscore"),
            LevelSelectMode.EditMode => new Button("Level Info"),
            _ => new Button("Cancel")
        };

        _tertiaryButton = _mode == LevelSelectMode.EditMode ? new Button("Delete") : null;
        _quaternaryButton = _mode == LevelSelectMode.EditMode ? new Button("Create New") : null;

        _primaryFocus = _primaryButton is not null ? new FocusableButton(_primaryButton) : null;
        _secondaryFocus = _secondaryButton is not null ? new FocusableButton(_secondaryButton) : null;
        _tertiaryFocus = _tertiaryButton is not null ? new FocusableButton(_tertiaryButton) : null;
        _quaternaryFocus = _quaternaryButton is not null ? new FocusableButton(_quaternaryButton) : null;

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

            _lavaRiseCheckbox = new Checkbox
            {
                Label = "Lava Rise",
                IsChecked = s_lavaRiseEnabled
            };

            _ropeModeFocus = new FocusableCycleSelector<RopeGameplayMode>(_ropeModeSelector);
            _lavaRiseFocus = new FocusableCheckbox(_lavaRiseCheckbox);
        }
    }

    public void Update(GameTime gameTime)
    {
        LayoutButtons();
        RefreshGridLayout();

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

        UpdateFocus(gameTime);

        if (_backFocus.WasActivated || _game.Input.ExitPressed || (_game.Input.MenuCancelPressed && !_focus.IsCapturingNavigation))
        {
            _game.ChangeScene(new MenuScene(_game));
            return;
        }

        if (_ropeModeSelector != null)
        {
            s_selectedRopeMode = _ropeModeSelector.CurrentOption;
        }

        if (_lavaRiseCheckbox != null)
        {
            s_lavaRiseEnabled = _lavaRiseCheckbox.IsChecked;
        }

        if (_primaryFocus?.WasActivated == true && _selectedIndex.HasValue && CanStartPlay())
        {
            HandlePrimaryAction();
            return;
        }

        if (_secondaryFocus?.WasActivated == true && _selectedIndex.HasValue)
        {
            HandleSecondaryAction();
            return;
        }

        if (_tertiaryFocus?.WasActivated == true && _selectedIndex.HasValue)
        {
            HandleDeleteLevel();
            return;
        }

        if (_quaternaryFocus?.WasActivated == true)
        {
            HandleCreateNew();
            return;
        }

        if (_selectedIndex.HasValue)
        {
            int selectedIndex = _selectedIndex.Value;
            if (selectedIndex >= 0 && selectedIndex < _levels.Count)
            {
                string levelId = _levels[selectedIndex].Id;
                if (_selectedLevelId != levelId)
                {
                    UpdateSelectedLevelDetails();
                }
            }
        }
    }

    private void UpdateFocus(GameTime gameTime)
    {
        _gridFocusables.Clear();
        _focus.Clear();

        int gridStart = -1;
        for (int i = 0; i < _levels.Count && i < _gridLayout.CellBounds.Length; i++)
        {
            int captured = i;
            var cell = new FocusableGridCell(_gridLayout.CellBounds[i], () =>
            {
                _selectedIndex = captured;
                return true;
            });
            _gridFocusables.Add(cell);
            if (gridStart < 0)
            {
                gridStart = _focus.Add(cell, $"Level{captured}");
            }
            else
            {
                _focus.Add(cell, $"Level{captured}");
            }
        }

        int? ropeIndex = null;
        int? lavaIndex = null;
        if (_ropeModeFocus != null)
        {
            ropeIndex = _focus.Add(_ropeModeFocus, "RopeMode");
        }

        if (_lavaRiseFocus != null)
        {
            lavaIndex = _focus.Add(_lavaRiseFocus, "LavaRise");
        }

        int backIndex = _focus.Add(_backFocus, "Back");
        int? primaryIndex = _primaryFocus is not null ? _focus.Add(_primaryFocus, _mode == LevelSelectMode.PlayMode ? "Play" : "EditLevel") : null;
        int? secondaryIndex = _secondaryFocus is not null ? _focus.Add(_secondaryFocus, _mode == LevelSelectMode.PlayMode ? "DeleteHighscore" : "LevelInfo") : null;
        int? tertiaryIndex = _tertiaryFocus is not null ? _focus.Add(_tertiaryFocus, "DeleteLevel") : null;
        int? quaternaryIndex = _quaternaryFocus is not null ? _focus.Add(_quaternaryFocus, "CreateNew") : null;

        NavigationGraph nav = _focus.Navigation;
        if (gridStart >= 0 && _gridFocusables.Count > 0)
        {
            nav.WireGrid(gridStart, _gridFocusables.Count, _gridLayout.Columns);

            if (_mode == LevelSelectMode.EditMode && primaryIndex is int primary)
            {
                NavigationGraphBuilder.LinkGridBottomRowTo(nav, gridStart, _gridFocusables.Count, _gridLayout.Columns, primary);

                nav.LinkHorizontal(backIndex, primary);

                if (secondaryIndex is int secondary)
                {
                    nav.LinkHorizontal(primary, secondary);
                }

                if (tertiaryIndex is int tertiary && secondaryIndex is int sec)
                {
                    nav.LinkHorizontal(sec, tertiary);
                }

                if (quaternaryIndex is int quaternary && tertiaryIndex is int ter)
                {
                    nav.LinkHorizontal(ter, quaternary);
                }
            }
            else if (_mode == LevelSelectMode.PlayMode)
            {
                // LEVEL GRID -> ROPE MODE -> (right) LAVA RISE, then ROPE/LAVA -> down -> PLAY
                // PLAY: left -> BACK, right -> DELETE HIGHSCORE
                int downTarget = ropeIndex ?? primaryIndex ?? backIndex;
                NavigationGraphBuilder.LinkGridBottomRowTo(nav, gridStart, _gridFocusables.Count, _gridLayout.Columns, downTarget);

                if (ropeIndex is int rope && lavaIndex is int lava)
                {
                    nav.LinkHorizontal(rope, lava);
                    if (primaryIndex is int play)
                    {
                        nav.LinkVertical(rope, play);
                        nav.Link(lava, NavigationDirection.Down, play);
                    }
                }
                else if (ropeIndex is int ropeOnly && primaryIndex is int playOnly)
                {
                    nav.LinkVertical(ropeOnly, playOnly);
                }

                if (primaryIndex is int playBtn)
                {
                    if (secondaryIndex is int secondary)
                    {
                        nav.LinkHorizontal(playBtn, secondary);
                    }

                    nav.LinkHorizontal(backIndex, playBtn);
                }
            }

            nav.Link(backIndex, NavigationDirection.Up, gridStart);
        }

        string defaultFocus = gridStart >= 0 ? "Level0" : (_mode == LevelSelectMode.PlayMode ? "Play" : "EditLevel");
        _focus.FinalizeFocus(defaultFocus);
        _focus.Update(gameTime, _game.Input);

        foreach (FocusableGridCell cell in _gridFocusables)
        {
            if (cell.WasActivated)
            {
                break;
            }
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

        if (_mode == LevelSelectMode.PlayMode && _selectedLevel != null)
        {
            DrawLevelDetailsPanel(spriteBatch, pixel, viewport);
        }

        // Draw buttons
        spriteBatch.Draw(pixel, new Rectangle(0, viewport.Height - 100, viewport.Width, 100), new Color(22, 26, 34));
        if (_mode == LevelSelectMode.PlayMode && _game.SteamLobby.IsInLobby && !_game.Party.IsLeader)
        {
            Rectangle waitBounds = new(20, viewport.Height - 132, viewport.Width - 40, 24);
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Waiting for Party Leader...", waitBounds, 2, new Color(167, 178, 198));
        }

        _backButton.Draw(spriteBatch, pixel);
        _primaryButton?.Draw(spriteBatch, pixel);
        _secondaryButton?.Draw(spriteBatch, pixel);
        _tertiaryButton?.Draw(spriteBatch, pixel);
        _quaternaryButton?.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);

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
            bool isHovered = _game.Input.Navigation.AllowPointerHoverVisual
                && cellBounds.Contains(_game.Input.UiPointerPosition);
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

            if (_lavaRiseCheckbox != null)
            {
                int checkboxX = Math.Min(viewport.Width - 200, _ropeModeSelector.Bounds.Right + 28);
                int checkboxY = _ropeModeSelector.Bounds.Y + ((selectorHeight - 24) / 2);
                _lavaRiseCheckbox.Bounds = new Rectangle(checkboxX, checkboxY, 180, 24);
            }
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

            bool leaderCanPlay = CanStartPlay();
            _primaryButton!.FillColor = leaderCanPlay ? new Color(52, 61, 80) : new Color(40, 46, 58);
            _primaryButton.TextColor = leaderCanPlay ? Color.White : new Color(167, 178, 198);
        }
        else
        {
            // Edit Level | Level Info | Delete | Create New (centered)
            var layout = ButtonRowLayout.Create(
                new[] { "Edit Level", "Level Info", "Delete", "Create New" },
                viewport.Width, viewport.Height,
                buttonHeight, horizontalPadding, 12, buttonGap, bottomMargin);

            if (layout.ButtonBounds.Length >= 4)
            {
                _primaryButton!.Bounds = layout.ButtonBounds[0];
                _secondaryButton!.Bounds = layout.ButtonBounds[1];
                _tertiaryButton!.Bounds = layout.ButtonBounds[2];
                _quaternaryButton!.Bounds = layout.ButtonBounds[3];
            }
        }
    }

    private void RefreshGridLayout()
    {
        int availableWidth = _game.Viewport.Width;
        if (_mode == LevelSelectMode.PlayMode && _selectedLevel != null)
        {
            int panelWidth = Math.Min(420, Math.Max(320, _game.Viewport.Width / 4));
            availableWidth = Math.Max(1, _game.Viewport.Width - panelWidth - 40);
        }

        _gridLayout = GridLayout.Create(_levels.Count, availableWidth, GetGridLayoutHeight(), CellWidth, CellHeight, HorizontalGap, VerticalGap);
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
        if (_quaternaryButton?.Bounds.Contains(_game.Input.MousePosition) ?? false)
            return true;
        if (_ropeModePanelBounds.Contains(_game.Input.MousePosition))
            return true;
        if (_ropeModeSelector?.Bounds.Contains(_game.Input.MousePosition) ?? false)
            return true;
        if (_lavaRiseCheckbox?.Bounds.Contains(_game.Input.MousePosition) ?? false)
            return true;
        if (_detailsPanelBounds.Contains(_game.Input.MousePosition))
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
            s_lavaRiseEnabled = _lavaRiseCheckbox?.IsChecked ?? s_lavaRiseEnabled;

            if (_game.SteamLobby.IsInLobby)
            {
                if (!_game.Party.IsLeader)
                {
                    return;
                }

                _game.SteamLobby.BroadcastLevelStart(levelId, s_selectedRopeMode, s_lavaRiseEnabled);
            }

            _game.ChangeScene(new GameScene(_game, levelId, s_selectedRopeMode, s_lavaRiseEnabled));
        }
        else
        {
            _game.ChangeScene(new EditorScene(_game, levelId));
        }
    }

    private bool CanStartPlay()
    {
        if (_mode != LevelSelectMode.PlayMode)
        {
            return true;
        }

        return !_game.SteamLobby.IsInLobby || _game.Party.IsLeader;
    }

    private void OnLevelStartReceived(PartyStartMessage message)
    {
        if (_mode != LevelSelectMode.PlayMode)
        {
            return;
        }

        _game.ChangeScene(new GameScene(_game, message.LevelId, message.RopeMode, message.LavaRiseEnabled));
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
            _game.ChangeScene(new LevelInfoScene(_game, level.Id));
        }
    }

    private void HandleDeleteLevel()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
            return;

        LevelMetadata level = _levels[_selectedIndex.Value];
        _popup = new Popup("Delete Level", $"Delete '{level.Name}' permanently?");
    }

    private void HandleCreateNew()
    {
        // Show create new level dialog with text input
        _popup = new Popup("Create New Level", "Enter level name:", "New Level");
    }

    private void UpdateSelectedLevelDetails()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
        {
            _selectedLevel = null;
            _selectedLevelPreview = null;
            _selectedLevelId = null;
            return;
        }

        LevelMetadata metadata = _levels[_selectedIndex.Value];
        _selectedLevelId = metadata.Id;
        _selectedLevel = LevelManager.LoadLevel(metadata.Id);
        _selectedLevelPreview = LevelPreviewManager.GetPreview(_game.GraphicsDevice, _game.Pixel, _selectedLevel, metadata.Id);
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
        if (_subscribedToLevelStart)
        {
            _game.SteamLobby.LevelStartReceived -= OnLevelStartReceived;
            _subscribedToLevelStart = false;
        }
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

        _lavaRiseCheckbox?.Draw(spriteBatch, pixel);
    }

    private void DrawLevelDetailsPanel(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        int panelWidth = Math.Min(420, Math.Max(320, viewport.Width / 4));
        int panelX = viewport.Width - panelWidth - 20;
        int panelY = 90;
        int panelHeight = Math.Max(320, viewport.Height - 170);
        _detailsPanelBounds = new Rectangle(panelX, panelY, panelWidth, panelHeight);

        spriteBatch.Draw(pixel, _detailsPanelBounds, new Color(25, 30, 40, 240));
        DrawHelper.DrawBorder(spriteBatch, pixel, _detailsPanelBounds, new Color(95, 110, 135), 2);

        var headerBounds = new Rectangle(panelX + 16, panelY + 16, panelWidth - 32, 36);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "LEVEL DETAILS", headerBounds, 2, Color.White);

        var nameBounds = new Rectangle(panelX + 16, panelY + 56, panelWidth - 32, 24);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, _selectedLevel.Name, nameBounds, 2, new Color(210, 220, 235));

        var previewBounds = new Rectangle(panelX + 15, panelY + 86, panelWidth - 30, Math.Min(180, panelHeight / 4));
        spriteBatch.Draw(pixel, previewBounds, new Color(20, 26, 36));
        DrawHelper.DrawBorder(spriteBatch, pixel, previewBounds, new Color(85, 100, 120), 2);

        if (_selectedLevelPreview != null)
        {
            spriteBatch.Draw(_selectedLevelPreview, previewBounds, Color.White);
        }
        else
        {
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "No Preview Available", previewBounds, 1, new Color(180, 190, 210));
        }

        int textX = panelX + 20;
        int textWidth = panelWidth - 40;
        int y = previewBounds.Bottom + 16;
        int rowHeight = 28;

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "Players:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
        y += rowHeight;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, GetPlayerCompatibilityText(_selectedLevel!), new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
        y += rowHeight + 4;

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "Rope:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
        y += rowHeight;
        string ropeText = GetRopeText(_selectedLevel!);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, string.IsNullOrEmpty(ropeText) ? "None" : ropeText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
        y += rowHeight + 4;

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "Features:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
        y += rowHeight;
        string featureText = GetFeatureText(_selectedLevel!);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, string.IsNullOrEmpty(featureText) ? "None" : featureText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
        y += rowHeight + 4;

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "Best Time:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
        y += rowHeight;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, GetBestTimeText(_selectedLevelId ?? string.Empty), new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
    }

    private string GetPlayerCompatibilityText(Level level)
    {
        if (level.AllPlayers)
        {
            return "All Players";
        }

        var supported = new System.Collections.Generic.List<string>();
        if (level.Player1) supported.Add("1P");
        if (level.Player2) supported.Add("2P");
        if (level.Player3) supported.Add("3P");
        if (level.Player4) supported.Add("4P");

        return supported.Count > 0 ? string.Join(", ", supported) : "None";
    }

    private string GetRopeText(Level level)
    {
        var ropeTags = new System.Collections.Generic.List<string>();
        if (level.ColoredRope) ropeTags.Add("Colored Rope");
        if (level.RegularRope) ropeTags.Add("Regular Rope");
        return string.Join(", ", ropeTags);
    }

    private string GetFeatureText(Level level)
    {
        var features = new System.Collections.Generic.List<string>();
        if (level.LavaRise) features.Add("Lava Rise");
        return string.Join(", ", features);
    }
}
