#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ColorBlocks.Replay;

namespace ColorBlocks;

public enum LevelSelectMode
{
    PlayMode,
    EditMode
}

public sealed class LevelSelectScene : IScene
{
    private enum LevelSelectPopupKind
    {
        Delete,
        CreateNew,
        CreateOfficial,
        OfficialReadOnly,
        Info
    }

    private static RopeGameplayMode s_selectedRopeMode = RopeGameplayMode.ColoredPhysics;
    private static bool s_lavaRiseEnabled;
    private static bool s_playerCollisionEnabled;
    private static bool s_ghostBestRunEnabled;

    private readonly ColorBlocksGame _game;
    private readonly LevelSelectMode _mode;
    private IReadOnlyList<LevelMetadata> _levels = new List<LevelMetadata>();
    private GridLayout _gridLayout = null!;
    private int? _selectedIndex;
    private Popup? _popup;
    private AlertPopup? _alertPopup;
    private LevelSelectPopupKind _popupKind;
    private LevelSource _activeTab;
    private bool _importOfficialPickerOpen;
    private double _importPickerOpenedAt = -1;
    private string? _pendingOfficialLevelId;
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
    private FocusableButton? _quinaryFocus;
    private FocusableCycleSelector<RopeGameplayMode>? _ropeModeFocus;
    private FocusableCheckbox? _lavaRiseFocus;
    private FocusableCheckbox? _playerCollisionFocus;
    private FocusableCheckbox? _ghostBestRunFocus;
    private Button? _primaryButton;
    private Button? _secondaryButton;
    private Button? _tertiaryButton;
    private Button? _quaternaryButton;
    private Button? _quinaryButton;
    private Button? _importOfficialButton;
    private Button? _createOfficialButton;
    private Button? _convertToOfficialButton;
    private FocusableButton? _importOfficialFocus;
    private FocusableButton? _createOfficialFocus;
    private FocusableButton? _convertToOfficialFocus;
    private readonly List<Rectangle> _tabBounds = new();
    private Rectangle _leftShoulderHintBounds;
    private Rectangle _rightShoulderHintBounds;
    private CycleSelector<RopeGameplayMode>? _ropeModeSelector;
    private Checkbox? _lavaRiseCheckbox;
    private Checkbox? _playerCollisionCheckbox;
    private Checkbox? _ghostBestRunCheckbox;
    private Button? _watchReplayButton;
    private FocusableButton? _watchReplayFocus;
    private Rectangle _ropeModePanelBounds;
    private Rectangle _ropeModeLabelBounds;
    private Rectangle _ropeModeDescriptionBounds;
    private Rectangle _detailsPanelBounds;
    private Level? _selectedLevel;
    private Texture2D? _selectedLevelPreview;
    private string? _selectedLevelId;
    private string _detailsLevelName = "";
    private string _detailsAuthorText = "";
    private string _detailsPlayersText = "";
    private string _detailsRopeText = "";
    private string _detailsFeaturesText = "";
    private string _detailsBestTimeText = "--";
    private string _detailsUnofficialBestText = "";
    private bool _detailsHasUnofficialBest;
    private bool _detailsHasBestReplay;

    // Constants
    private const int CellWidth = 200;
    private const int CellHeight = 140;
    private const int HorizontalGap = 20;
    private const int VerticalGap = 20;

    public LevelSelectScene(ColorBlocksGame game, LevelSelectMode mode)
    {
        _game = game;
        _mode = mode;
        DeveloperSettings.Reload();
        _activeTab = mode == LevelSelectMode.PlayMode ? LevelSource.Official : LevelSource.Local;
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

    private void RefreshLevelList(bool preserveSelection = false)
    {
        int? previousIndex = preserveSelection ? _selectedIndex : null;
        string? previousLevelId = preserveSelection ? _selectedLevelId : null;

        _levels = GetLevelsForActiveTab();
        if (_mode == LevelSelectMode.EditMode && _activeTab == LevelSource.Local && _levels.Count == 0)
        {
            LevelLibrary.CreateNewLevel("Level 1");
            _levels = LevelLibrary.GetLocalLevels();
        }

        if (previousLevelId is not null)
        {
            int restored = -1;
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Id == previousLevelId)
                {
                    restored = i;
                    break;
                }
            }

            _selectedIndex = restored >= 0 ? restored : null;
        }
        else
        {
            _selectedIndex = previousIndex is int index && index >= 0 && index < _levels.Count ? index : null;
        }

        if (_selectedIndex is null)
        {
            _selectedLevel = null;
            _selectedLevelPreview = null;
            _selectedLevelId = null;
        }

        _gridLayout = GridLayout.Create(_levels.Count, _game.Viewport.Width, GetGridLayoutHeight(), CellWidth, CellHeight, HorizontalGap, VerticalGap, minStartY: 118);
    }

    private IReadOnlyList<LevelMetadata> GetLevelsForActiveTab()
    {
        if (_importOfficialPickerOpen)
        {
            return LevelLibrary.GetOfficialLevels();
        }

        if (_mode == LevelSelectMode.PlayMode)
        {
            return _activeTab switch
            {
                LevelSource.Official => LevelLibrary.GetOfficialLevels(),
                LevelSource.Workshop => LevelLibrary.GetWorkshopLevels(),
                _ => LevelLibrary.GetLocalLevels()
            };
        }

        // Editor: Local (+ Official in developer mode). No Workshop — edit via Create Copy → Local.
        if (DeveloperSettings.DeveloperMode && _activeTab == LevelSource.Official)
        {
            return LevelLibrary.GetOfficialLevels();
        }

        return LevelLibrary.GetLocalLevels();
    }

    private IReadOnlyList<LevelSource> GetVisibleTabs()
    {
        if (_mode == LevelSelectMode.PlayMode)
        {
            return new[] { LevelSource.Official, LevelSource.Local, LevelSource.Workshop };
        }

        // Editor has no Workshop tab (browse/play only; edits go through Local copies).
        if (DeveloperSettings.DeveloperMode)
        {
            return new[] { LevelSource.Local, LevelSource.Official };
        }

        return new[] { LevelSource.Local };
    }

    private void SwitchTab(LevelSource tab)
    {
        if (_activeTab == tab)
        {
            return;
        }

        _activeTab = tab;
        _importOfficialPickerOpen = false;
        RefreshLevelList();
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
            LevelSelectMode.EditMode => new Button("Level Info"),
            _ => null
        };

        _tertiaryButton = _mode == LevelSelectMode.EditMode ? new Button("Delete") : null;
        _quaternaryButton = _mode == LevelSelectMode.EditMode ? new Button("Create Copy") : null;
        _quinaryButton = _mode == LevelSelectMode.EditMode ? new Button("Create New") : null;
        _importOfficialButton = _mode == LevelSelectMode.EditMode ? new Button("Import Official") : null;
        _createOfficialButton = _mode == LevelSelectMode.EditMode ? new Button("Create Official") : null;
        _convertToOfficialButton = _mode == LevelSelectMode.EditMode ? new Button("Convert To Official") : null;

        _primaryFocus = _primaryButton is not null ? new FocusableButton(_primaryButton) : null;
        _secondaryFocus = _secondaryButton is not null ? new FocusableButton(_secondaryButton) : null;
        _tertiaryFocus = _tertiaryButton is not null ? new FocusableButton(_tertiaryButton) : null;
        _quaternaryFocus = _quaternaryButton is not null ? new FocusableButton(_quaternaryButton) : null;
        _quinaryFocus = _quinaryButton is not null ? new FocusableButton(_quinaryButton) : null;
        _importOfficialFocus = _importOfficialButton is not null ? new FocusableButton(_importOfficialButton) : null;
        _createOfficialFocus = _createOfficialButton is not null ? new FocusableButton(_createOfficialButton) : null;
        _convertToOfficialFocus = _convertToOfficialButton is not null ? new FocusableButton(_convertToOfficialButton) : null;

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

            _playerCollisionCheckbox = new Checkbox
            {
                Label = "Player Collision",
                IsChecked = s_playerCollisionEnabled
            };

            _ghostBestRunCheckbox = new Checkbox
            {
                Label = "Ghost Best Run",
                IsChecked = s_ghostBestRunEnabled
            };

            _watchReplayButton = new Button("Watch Replay");
            _watchReplayFocus = new FocusableButton(_watchReplayButton);

            _ropeModeFocus = new FocusableCycleSelector<RopeGameplayMode>(_ropeModeSelector);
            _lavaRiseFocus = new FocusableCheckbox(_lavaRiseCheckbox);
            _playerCollisionFocus = new FocusableCheckbox(_playerCollisionCheckbox);
            _ghostBestRunFocus = new FocusableCheckbox(_ghostBestRunCheckbox);
        }
    }

    public void Update(GameTime gameTime)
    {
        LayoutButtons();
        RefreshGridLayout();

        if (_alertPopup != null)
        {
            _alertPopup.Update(gameTime, _game.Input, _game.Viewport.Width, _game.Viewport.Height);
            if (_alertPopup.IsDismissed)
            {
                _alertPopup = null;
            }

            return;
        }

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
                _pendingOfficialLevelId = null;
            }

            return;
        }

        if (_importOfficialPickerOpen)
        {
            HandleImportOfficialPickerInput(gameTime);
            return;
        }

        HandleTabInput();

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

        if (_playerCollisionCheckbox != null)
        {
            s_playerCollisionEnabled = _playerCollisionCheckbox.IsChecked;
        }

        if (_ghostBestRunCheckbox != null)
        {
            s_ghostBestRunEnabled = _detailsHasBestReplay && _ghostBestRunCheckbox.IsChecked;
        }

        if (_watchReplayFocus?.WasActivated == true && _selectedLevelId is not null
            && _detailsHasBestReplay)
        {
            _game.ChangeScene(new ReplayViewerScene(_game, _selectedLevelId));
            return;
        }

        if (_primaryFocus?.WasActivated == true && _selectedIndex.HasValue && CanActivatePrimary())
        {
            HandlePrimaryAction();
            return;
        }

        if (_secondaryFocus?.WasActivated == true && _selectedIndex.HasValue && CanOpenLevelInfo())
        {
            HandleSecondaryAction();
            return;
        }

        if (_tertiaryFocus?.WasActivated == true && _selectedIndex.HasValue && CanDeleteSelected())
        {
            HandleDeleteLevel();
            return;
        }

        if (_quaternaryFocus?.WasActivated == true && _selectedIndex.HasValue)
        {
            HandleCreateCopy();
            return;
        }

        if (_quinaryFocus?.WasActivated == true)
        {
            HandleCreateNew();
            return;
        }

        if (_importOfficialFocus?.WasActivated == true)
        {
            OpenImportOfficialPicker(gameTime);
            return;
        }

        if (_createOfficialFocus?.WasActivated == true && DeveloperSettings.DeveloperMode)
        {
            HandleCreateOfficialLevel();
            return;
        }

        if (_convertToOfficialFocus?.WasActivated == true && _selectedIndex.HasValue && DeveloperSettings.DeveloperMode)
        {
            HandleConvertToOfficial();
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
        if (_ropeModeFocus != null)
        {
            ropeIndex = _focus.Add(_ropeModeFocus, "RopeMode");
        }

        int? watchReplayIndex = null;
        if (_watchReplayFocus != null && _selectedLevelId is not null)
        {
            _watchReplayFocus.IsEnabled = true;
            watchReplayIndex = _focus.Add(_watchReplayFocus, "WatchReplay");
        }

        int? lavaIndex = null;
        if (_lavaRiseFocus != null)
        {
            lavaIndex = _focus.Add(_lavaRiseFocus, "LavaRise");
        }

        int? playerCollisionIndex = null;
        if (_playerCollisionFocus != null)
        {
            playerCollisionIndex = _focus.Add(_playerCollisionFocus, "PlayerCollision");
        }

        int? ghostIndex = null;
        if (_ghostBestRunFocus != null)
        {
            ghostIndex = _focus.Add(_ghostBestRunFocus, "GhostBestRun");
        }

        int backIndex = _focus.Add(_backFocus, "Back");
        int? primaryIndex = _primaryFocus is not null ? _focus.Add(_primaryFocus, _mode == LevelSelectMode.PlayMode ? "Play" : "EditLevel") : null;
        int? secondaryIndex = _secondaryFocus is not null ? _focus.Add(_secondaryFocus, "LevelInfo") : null;
        int? tertiaryIndex = _tertiaryFocus is not null ? _focus.Add(_tertiaryFocus, "DeleteLevel") : null;
        int? quaternaryIndex = _quaternaryFocus is not null ? _focus.Add(_quaternaryFocus, "CreateCopy") : null;
        int? quinaryIndex = _quinaryFocus is not null ? _focus.Add(_quinaryFocus, "CreateNew") : null;
        int? importIndex = _importOfficialFocus is not null ? _focus.Add(_importOfficialFocus, "ImportOfficial") : null;
        int? createOfficialIndex = _createOfficialFocus is not null && DeveloperSettings.DeveloperMode
            ? _focus.Add(_createOfficialFocus, "CreateOfficial")
            : null;
        int? convertOfficialIndex = _convertToOfficialFocus is not null && DeveloperSettings.DeveloperMode
            ? _focus.Add(_convertToOfficialFocus, "ConvertToOfficial")
            : null;

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
                    nav.Link(secondary, NavigationDirection.Up, gridStart);
                }

                if (tertiaryIndex is int tertiary && secondaryIndex is int sec)
                {
                    nav.LinkHorizontal(sec, tertiary);
                    nav.Link(tertiary, NavigationDirection.Up, gridStart);
                }

                if (quaternaryIndex is int quaternary && tertiaryIndex is int ter)
                {
                    nav.LinkHorizontal(ter, quaternary);
                    nav.Link(quaternary, NavigationDirection.Up, gridStart);
                }

                if (quinaryIndex is int quinary && quaternaryIndex is int quar)
                {
                    nav.LinkHorizontal(quar, quinary);
                    nav.Link(quinary, NavigationDirection.Up, gridStart);
                }

                if (importIndex is int import && quinaryIndex is int quin)
                {
                    nav.Link(quin, NavigationDirection.Down, import);
                    nav.Link(import, NavigationDirection.Up, quin);
                }

                if (createOfficialIndex is int createOfficial && importIndex is int importFromCreate)
                {
                    nav.LinkHorizontal(importFromCreate, createOfficial);
                }

                if (convertOfficialIndex is int convertOfficial && createOfficialIndex is int createFromConvert)
                {
                    nav.LinkHorizontal(createFromConvert, convertOfficial);
                }
                else if (convertOfficialIndex is int convertOnly && importIndex is int importFromConvert)
                {
                    nav.LinkHorizontal(importFromConvert, convertOnly);
                }
            }
            else if (_mode == LevelSelectMode.PlayMode)
            {
                int downTarget = ropeIndex ?? primaryIndex ?? backIndex;
                NavigationGraphBuilder.LinkGridBottomRowTo(nav, gridStart, _gridFocusables.Count, _gridLayout.Columns, downTarget);

                if (ropeIndex is int rope && primaryIndex is int playFromRope)
                {
                    nav.LinkVertical(rope, playFromRope);
                }

                // Details panel cascade: Watch Replay → Lava → Collision → Ghost → Play
                int? detailsCursor = watchReplayIndex;
                if (detailsCursor is int watch && lavaIndex is int lava)
                {
                    nav.LinkVertical(watch, lava);
                    detailsCursor = lava;
                }

                if (detailsCursor is int fromLava && playerCollisionIndex is int playerCollision)
                {
                    nav.LinkVertical(fromLava, playerCollision);
                    detailsCursor = playerCollision;
                }

                if (detailsCursor is int fromCollision && ghostIndex is int ghost)
                {
                    nav.LinkVertical(fromCollision, ghost);
                    detailsCursor = ghost;
                }

                if (detailsCursor is int detailsBottom && primaryIndex is int playFromDetails)
                {
                    nav.Link(detailsBottom, NavigationDirection.Down, playFromDetails);
                }

                if (watchReplayIndex is int watchReplay && _gridFocusables.Count > 0)
                {
                    int topRight = gridStart + Math.Min(_gridLayout.Columns - 1, _gridFocusables.Count - 1);
                    nav.Link(topRight, NavigationDirection.Right, watchReplay);
                    nav.Link(watchReplay, NavigationDirection.Left, topRight);
                }

                if (primaryIndex is int playBtn)
                {
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

        if (ReplayMenuBackground.IsActive(_game))
        {
            ReplayMenuBackground.DrawDimmingOverlay(spriteBatch, pixel, viewport);
        }
        else
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(29, 34, 45));
        }

        // Draw title
        string title = _mode == LevelSelectMode.PlayMode ? "SELECT A LEVEL TO PLAY" : "LEVEL EDITOR";
        Rectangle titleBounds = new(20, 20, viewport.Width - 40, 50);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, title, titleBounds, 3, Color.White);

        DrawSourceTabs(spriteBatch, pixel, viewport, gameTime);

        // Draw level grid
        DrawLevelGrid(spriteBatch, pixel);
        DrawRopeModeSelector(spriteBatch, pixel, viewport);

        if (_selectedLevel != null)
        {
            DrawLevelDetailsPanel(spriteBatch, pixel, viewport, showPlayStats: _mode == LevelSelectMode.PlayMode);
        }

        if (_importOfficialPickerOpen)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 150));
            Rectangle hintBounds = new(20, 110, viewport.Width - 40, 32);
            SimpleTextRenderer.DrawCentered(
                spriteBatch,
                pixel,
                "SELECT OFFICIAL LEVEL TO IMPORT  (Esc / B to cancel)",
                hintBounds,
                2,
                new Color(230, 236, 245));
        }

        // Draw buttons
        int bottomBarHeight = GetBottomBarHeight();
        spriteBatch.Draw(
            pixel,
            new Rectangle(0, viewport.Height - bottomBarHeight, viewport.Width, bottomBarHeight),
            ReplayMenuBackground.IsActive(_game) ? new Color(22, 26, 34, 180) : new Color(22, 26, 34));
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
        _quinaryButton?.Draw(spriteBatch, pixel);
        _importOfficialButton?.Draw(spriteBatch, pixel);
        if (DeveloperSettings.DeveloperMode)
        {
            _createOfficialButton?.Draw(spriteBatch, pixel);
            _convertToOfficialButton?.Draw(spriteBatch, pixel);
        }
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);

        spriteBatch.End();

        // Draw popup on top (needs separate begin/end for proper layering)
        if (_popup != null)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _popup.Draw(gameTime, spriteBatch, pixel);
            spriteBatch.End();
        }

        if (_alertPopup != null)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            _alertPopup.Draw(spriteBatch, pixel, viewport.Width, viewport.Height, gameTime, _game.Input);
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

            // Draw source badge
            Rectangle badgeBounds = new(cellBounds.X + 8, cellBounds.Y + 8, 22, 22);
            Color badgeColor = LevelSourceVisuals.GetBadgeColor(level.Source);
            spriteBatch.Draw(pixel, badgeBounds, badgeColor);
            DrawHelper.DrawBorder(spriteBatch, pixel, badgeBounds, Color.White, 1);
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, level.SourceIcon, badgeBounds, 1, Color.White);

            // Draw level name
            Rectangle nameRect = new(cellBounds.X + 10, cellBounds.Y + 34, cellBounds.Width - 20, 40);
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
            return $"Best: {BestTimeStorage.FormatTime(bestTime)}";
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
            var layout = ButtonRowLayout.Create(
                new[] { "Play" },
                viewport.Width, viewport.Height,
                buttonHeight, horizontalPadding, 12, buttonGap, bottomMargin);

            if (layout.ButtonBounds.Length >= 1)
            {
                _primaryButton!.Bounds = layout.ButtonBounds[0];
            }

            bool leaderCanPlay = CanStartPlay();
            _primaryButton!.FillColor = leaderCanPlay ? new Color(52, 61, 80) : new Color(40, 46, 58);
            _primaryButton.TextColor = leaderCanPlay ? Color.White : new Color(167, 178, 198);
        }
        else
        {
            const int secondRowBottomMargin = 22;
            const int rowGap = 14;
            int mainRowBottomMargin = secondRowBottomMargin + buttonHeight + rowGap;

            var mainRow = ButtonRowLayout.Create(
                new[] { "Edit Level", "Level Info", "Delete", "Create Copy", "Create New" },
                viewport.Width, viewport.Height,
                buttonHeight, horizontalPadding, 12, buttonGap, mainRowBottomMargin);

            if (mainRow.ButtonBounds.Length >= 5)
            {
                _primaryButton!.Bounds = mainRow.ButtonBounds[0];
                _secondaryButton!.Bounds = mainRow.ButtonBounds[1];
                _tertiaryButton!.Bounds = mainRow.ButtonBounds[2];
                _quaternaryButton!.Bounds = mainRow.ButtonBounds[3];
                _quinaryButton!.Bounds = mainRow.ButtonBounds[4];
            }

            var secondRowLabels = new List<string> { "Import Official" };
            if (DeveloperSettings.DeveloperMode)
            {
                secondRowLabels.Add("Create Official");
                secondRowLabels.Add("Convert To Official");
            }

            var secondRow = ButtonRowLayout.Create(
                secondRowLabels.ToArray(),
                viewport.Width,
                viewport.Height,
                buttonHeight,
                horizontalPadding,
                12,
                buttonGap,
                secondRowBottomMargin);

            if (secondRow.ButtonBounds.Length >= 1 && _importOfficialButton is not null)
            {
                _importOfficialButton.Bounds = secondRow.ButtonBounds[0];
            }

            if (DeveloperSettings.DeveloperMode)
            {
                if (secondRow.ButtonBounds.Length >= 2 && _createOfficialButton is not null)
                {
                    _createOfficialButton.Bounds = secondRow.ButtonBounds[1];
                }

                if (secondRow.ButtonBounds.Length >= 3 && _convertToOfficialButton is not null)
                {
                    _convertToOfficialButton.Bounds = secondRow.ButtonBounds[2];
                }
            }

            bool canEdit = CanEditSelected();
            _primaryButton!.FillColor = canEdit ? new Color(52, 61, 80) : new Color(40, 46, 58);
            _primaryButton.TextColor = canEdit ? Color.White : new Color(167, 178, 198);

            bool canDelete = CanDeleteSelected();
            if (_tertiaryButton is not null)
            {
                _tertiaryButton.FillColor = canDelete ? new Color(52, 61, 80) : new Color(40, 46, 58);
                _tertiaryButton.TextColor = canDelete ? Color.White : new Color(167, 178, 198);
            }
        }
    }

    private void LayoutSourceTabs(Viewport viewport)
    {
        _tabBounds.Clear();
        IReadOnlyList<LevelSource> tabs = GetVisibleTabs();
        if (tabs.Count == 0)
        {
            return;
        }

        const int tabHeight = 34;
        const int tabGap = 10;
        int tabY = 72;
        int totalWidth = 0;
        int[] tabWidths = new int[tabs.Count];

        for (int i = 0; i < tabs.Count; i++)
        {
            string label = LevelSourceVisuals.GetTabLabel(tabs[i]);
            Point measured = SimpleTextRenderer.MeasureString(label, 2);
            tabWidths[i] = Math.Max(120, measured.X + 28);
            totalWidth += tabWidths[i];
        }

        totalWidth += tabGap * (tabs.Count - 1);
        int startX = Math.Max(20, (viewport.Width - totalWidth) / 2);
        int currentX = startX;

        for (int i = 0; i < tabs.Count; i++)
        {
            _tabBounds.Add(new Rectangle(currentX, tabY, tabWidths[i], tabHeight));
            currentX += tabWidths[i] + tabGap;
        }

        if (_tabBounds.Count > 0)
        {
            const int hintWidth = 50;
            const int hintGap = 8;
            Rectangle firstTab = _tabBounds[0];
            Rectangle lastTab = _tabBounds[^1];
            _leftShoulderHintBounds = new Rectangle(firstTab.X - hintWidth - hintGap, tabY, hintWidth, tabHeight);
            _rightShoulderHintBounds = new Rectangle(lastTab.Right + hintGap, tabY, hintWidth, tabHeight);
        }
    }

    private int GetBottomBarHeight()
    {
        if (_mode != LevelSelectMode.EditMode)
        {
            return 100;
        }

        const int buttonHeight = 50;
        const int secondRowBottomMargin = 22;
        const int rowGap = 14;
        return buttonHeight * 2 + rowGap + secondRowBottomMargin + 16;
    }

    private void DrawSourceTabs(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, GameTime gameTime)
    {
        LayoutSourceTabs(viewport);
        IReadOnlyList<LevelSource> tabs = GetVisibleTabs();
        bool allowHover = _game.Input.Navigation.AllowPointerHoverVisual;
        Point pointer = _game.Input.UiPointerPosition;

        for (int i = 0; i < tabs.Count && i < _tabBounds.Count; i++)
        {
            Rectangle bounds = _tabBounds[i];
            bool isActive = !_importOfficialPickerOpen && tabs[i] == _activeTab;
            bool isHovered = allowHover && !isActive && bounds.Contains(pointer);

            float scale = isHovered ? 1.03f : 1.0f;
            int scaledW = (int)(bounds.Width * scale);
            int scaledH = (int)(bounds.Height * scale);
            int offsetX = (scaledW - bounds.Width) / 2;
            int offsetY = (scaledH - bounds.Height) / 2;
            Rectangle drawRect = new(bounds.X - offsetX, bounds.Y - offsetY, scaledW, scaledH);

            Color fill = isActive
                ? new Color(74, 120, 180)
                : isHovered ? new Color(74, 86, 110) : new Color(45, 52, 68);
            Color border = isActive
                ? new Color(120, 180, 255)
                : isHovered ? new Color(240, 242, 246) : new Color(80, 90, 110);

            spriteBatch.Draw(pixel, new Rectangle(drawRect.X + 4, drawRect.Y + 5, drawRect.Width, drawRect.Height), new Color(5, 7, 12, 95));
            spriteBatch.Draw(pixel, drawRect, fill);
            DrawHelper.DrawBorder(spriteBatch, pixel, drawRect, border, isHovered || isActive ? 3 : 2);
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, LevelSourceVisuals.GetTabLabel(tabs[i]), drawRect, 2, Color.White);
        }

        DrawGamepadTabHints(spriteBatch, pixel, gameTime);
    }

    private void DrawGamepadTabHints(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime)
    {
        if (!_game.Input.Navigation.IsGamepadActive || _tabBounds.Count <= 1)
        {
            return;
        }

        double time = gameTime.TotalGameTime.TotalSeconds;
        DrawShoulderHint(spriteBatch, pixel, _leftShoulderHintBounds, "< LB", time);
        DrawShoulderHint(spriteBatch, pixel, _rightShoulderHintBounds, "RB >", time);
    }

    private static void DrawShoulderHint(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, string label, double totalSeconds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        float pulse = 0.5f + (MathF.Sin((float)totalSeconds * 5.2f) * 0.5f);
        int inflate = 1 + (int)MathF.Round(pulse * 2f);
        Rectangle glowBounds = new(
            bounds.X - inflate,
            bounds.Y - inflate,
            bounds.Width + inflate * 2,
            bounds.Height + inflate * 2);

        byte glowAlpha = (byte)Math.Clamp(40 + pulse * 90f, 40, 130);
        spriteBatch.Draw(pixel, glowBounds, new Color((byte)80, (byte)150, (byte)230, glowAlpha));

        var fill = new Color(
            (byte)Math.Clamp(38 + pulse * 40f, 38, 78),
            (byte)Math.Clamp(56 + pulse * 50f, 56, 106),
            (byte)Math.Clamp(88 + pulse * 60f, 88, 148),
            (byte)255);
        spriteBatch.Draw(pixel, bounds, fill);

        var border = new Color(
            (byte)Math.Clamp(120 + pulse * 100f, 120, 220),
            (byte)Math.Clamp(170 + pulse * 70f, 170, 240),
            (byte)255,
            (byte)Math.Clamp(170 + pulse * 85f, 170, 255));
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, border, 2);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, label, bounds, 1, Color.White);
    }

    private void HandleTabInput()
    {
        if (_importOfficialPickerOpen)
        {
            return;
        }

        LayoutSourceTabs(_game.Viewport);
        IReadOnlyList<LevelSource> tabs = GetVisibleTabs();

        if (_game.Input.GamepadMenuTabLeftPressed)
        {
            SwitchTabOffset(-1);
            return;
        }

        if (_game.Input.GamepadMenuTabRightPressed)
        {
            SwitchTabOffset(1);
            return;
        }

        if (_game.Input.MenuTabBackwardPressed)
        {
            SwitchTabOffset(-1);
            return;
        }

        if (_game.Input.MenuTabPressed)
        {
            SwitchTabOffset(1);
            return;
        }

        if (_game.Input.UiPointerPressed)
        {
            for (int i = 0; i < tabs.Count && i < _tabBounds.Count; i++)
            {
                if (_tabBounds[i].Contains(_game.Input.UiPointerPosition))
                {
                    SwitchTab(tabs[i]);
                    return;
                }
            }
        }
    }

    private void SwitchTabOffset(int delta)
    {
        IReadOnlyList<LevelSource> tabs = GetVisibleTabs();
        if (tabs.Count == 0)
        {
            return;
        }

        int currentIndex = 0;
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] == _activeTab)
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + delta + tabs.Count) % tabs.Count;
        SwitchTab(tabs[nextIndex]);
    }

    private void RefreshGridLayout()
    {
        int availableWidth = _game.Viewport.Width;
        if (_selectedLevel != null)
        {
            int panelWidth = Math.Min(420, Math.Max(320, _game.Viewport.Width / 4));
            availableWidth = Math.Max(1, _game.Viewport.Width - panelWidth - 40);
        }

        _gridLayout = GridLayout.Create(_levels.Count, availableWidth, GetGridLayoutHeight(), CellWidth, CellHeight, HorizontalGap, VerticalGap, minStartY: 118);
    }

    private bool IsMouseOverButtons()
    {
        if (_backButton.Bounds.Contains(_game.Input.UiPointerPosition))
            return true;
        if (_primaryButton?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_secondaryButton?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_tertiaryButton?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_quaternaryButton?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_importOfficialButton?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_createOfficialButton?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_convertToOfficialButton?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        foreach (Rectangle tabBounds in _tabBounds)
        {
            if (tabBounds.Contains(_game.Input.UiPointerPosition))
            {
                return true;
            }
        }
        if (_ropeModePanelBounds.Contains(_game.Input.UiPointerPosition))
            return true;
        if (_ropeModeSelector?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_lavaRiseCheckbox?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_playerCollisionCheckbox?.Bounds.Contains(_game.Input.UiPointerPosition) ?? false)
            return true;
        if (_detailsPanelBounds.Contains(_game.Input.UiPointerPosition))
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
            s_playerCollisionEnabled = _playerCollisionCheckbox?.IsChecked ?? s_playerCollisionEnabled;
            s_ghostBestRunEnabled = _ghostBestRunCheckbox?.IsChecked ?? s_ghostBestRunEnabled;

            MultiplayerDebug.LogSim($"PlayPressed level={levelId} mode={_mode}");
            if (_game.SteamLobby.IsInLobby)
            {
                if (!_game.Party.IsLeader)
                {
                    return;
                }

                MultiplayerDebug.LogSim(
                    $"HOST PRESSED PLAY level={levelId} partyMembers={_game.Party.Members.Count} " +
                    $"lobbyMembers={_game.SteamLobby.GetLobbyMemberCount()} " +
                    "(no session handshake exists — each peer spawns from its OWN party roster)");
                foreach (PartyMember member in _game.Party.Members)
                {
                    MultiplayerDebug.LogSim(
                        $"  start-roster '{member.DisplayName}' {(member.IsLocallyOwned ? "LOCAL" : "REMOTE")} " +
                        $"type={member.MemberType} steam={member.OwningSteamId}");
                }

                if (!_game.SteamLobby.BroadcastLevelStart(levelId, s_selectedRopeMode, s_lavaRiseEnabled))
                {
                    _alertPopup = new AlertPopup(
                        "VERSION MISMATCH",
                        $"Host: {SessionDiagnostics.HostBuildLabel} Client: {SessionDiagnostics.ClientBuildLabel}");
                    return;
                }
            }

            _game.ChangeScene(new GameScene(
                _game,
                levelId,
                s_selectedRopeMode,
                s_lavaRiseEnabled,
                s_ghostBestRunEnabled,
                s_playerCollisionEnabled));
        }
        else
        {
            LevelMetadata metadata = _levels[_selectedIndex.Value];
            if (!CanEditSelected())
            {
                ShowOfficialReadOnlyPopup(metadata);
                return;
            }

            _game.ChangeScene(new EditorScene(_game, metadata.Id));
        }
    }

    private bool CanActivatePrimary()
    {
        if (_mode == LevelSelectMode.PlayMode)
        {
            return CanStartPlay();
        }

        return _selectedIndex.HasValue;
    }

    private bool CanOpenLevelInfo()
    {
        if (_mode != LevelSelectMode.EditMode || !_selectedIndex.HasValue)
        {
            return false;
        }

        LevelMetadata metadata = _levels[_selectedIndex.Value];
        return metadata.Source != LevelSource.Workshop || DeveloperSettings.DeveloperMode;
    }

    private bool CanEditSelected()
    {
        if (_mode != LevelSelectMode.EditMode || !_selectedIndex.HasValue)
        {
            return false;
        }

        LevelMetadata metadata = _levels[_selectedIndex.Value];
        if (metadata.Source == LevelSource.Workshop && !DeveloperSettings.DeveloperMode)
        {
            return false;
        }

        if (metadata.Source == LevelSource.Official && !DeveloperSettings.DeveloperMode)
        {
            return false;
        }

        return true;
    }

    private bool CanDeleteSelected()
    {
        if (_mode != LevelSelectMode.EditMode || !_selectedIndex.HasValue)
        {
            return false;
        }

        LevelMetadata metadata = _levels[_selectedIndex.Value];
        if (metadata.Source == LevelSource.Official && !DeveloperSettings.DeveloperMode)
        {
            return false;
        }

        if (metadata.Source == LevelSource.Workshop && !DeveloperSettings.DeveloperMode)
        {
            return false;
        }

        return true;
    }

    private bool CanStartPlay()
    {
        if (_mode != LevelSelectMode.PlayMode)
        {
            return true;
        }

        if (_game.SteamLobby.IsInLobby && !_game.Party.IsLeader)
        {
            return false;
        }

        if (_selectedLevel is null)
        {
            return false;
        }

        int partyCount = _game.Party.Members.Count;
        return LevelRules.SupportsPlayerCount(_selectedLevel, partyCount);
    }

    private void OnLevelStartReceived(PartyStartMessage message)
    {
        if (_mode != LevelSelectMode.PlayMode)
        {
            return;
        }

        MultiplayerDebug.LogSim(
            $"CLIENT START RECEIVED level={message.LevelId} partyMembers={_game.Party.Members.Count} " +
            $"lobbyMembers={_game.SteamLobby.GetLobbyMemberCount()}");
        foreach (PartyMember member in _game.Party.Members)
        {
            MultiplayerDebug.LogSim(
                $"  start-roster '{member.DisplayName}' {(member.IsLocallyOwned ? "LOCAL" : "REMOTE")} " +
                $"type={member.MemberType} steam={member.OwningSteamId}");
        }

        if (!MultiplayerStartGate.ValidateClientStart(_game.SteamLobby, message, out string title, out string error))
        {
            _alertPopup = new AlertPopup(title, error);
            return;
        }

        _game.ChangeScene(new GameScene(_game, message.LevelId, message.RopeMode, message.LavaRiseEnabled, s_ghostBestRunEnabled));
    }

    private void HandleSecondaryAction()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
            return;

        LevelMetadata level = _levels[_selectedIndex.Value];
        _game.ChangeScene(new LevelInfoScene(_game, level.Id));
    }

    private void HandleDeleteLevel()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
            return;

        LevelMetadata level = _levels[_selectedIndex.Value];
        _popupKind = LevelSelectPopupKind.Delete;
        _popup = new Popup("Delete Level", $"Delete '{level.Name}' permanently?");
    }

    private void HandleCreateNew()
    {
        _popupKind = LevelSelectPopupKind.CreateNew;
        int nextNumber = LevelLibrary.GetLocalLevels().Count + 1;
        _popup = new Popup("Create New Level", "Enter level name:", $"LEVEL {nextNumber}");
    }

    private void HandleCreateCopy()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
            return;

        LevelMetadata source = _levels[_selectedIndex.Value];
        string newLevelId = source.Source == LevelSource.Official
            ? LevelLibrary.ImportOfficialLevel(source.Id)
            : LevelLibrary.DuplicateLevel(source.Id);
        _activeTab = LevelSource.Local;
        SelectLevelById(newLevelId);
    }

    private void ShowOfficialReadOnlyPopup(LevelMetadata metadata)
    {
        _pendingOfficialLevelId = metadata.Id;
        _popupKind = LevelSelectPopupKind.OfficialReadOnly;
        _popup = new Popup(
            "Official Level",
            "Official levels cannot be edited.\nCreate a copy to edit this level.",
            "Create Copy",
            "Cancel");
    }

    private void OpenImportOfficialPicker(GameTime gameTime)
    {
        IReadOnlyList<LevelMetadata> officials = LevelLibrary.GetOfficialLevels();
        if (officials.Count == 0)
        {
            _popupKind = LevelSelectPopupKind.Info;
            _popup = new Popup(
                "Import Official",
                "No official levels found.\nRebuild game or check Content/OfficialLevels.",
                "OK",
                "Close");
            return;
        }

        _importOfficialPickerOpen = true;
        _importPickerOpenedAt = gameTime.TotalGameTime.TotalSeconds;
        _selectedIndex = null;
        _selectedLevel = null;
        _selectedLevelId = null;
        RefreshLevelList();
    }

    private void HandleImportOfficialPickerInput(GameTime gameTime)
    {
        LayoutButtons();
        RefreshGridLayout();
        UpdateFocusForImportPicker(gameTime);

        bool inGracePeriod = gameTime.TotalGameTime.TotalSeconds - _importPickerOpenedAt < 0.25;
        if (!inGracePeriod
            && (_game.Input.ExitPressed || _game.Input.MenuCancelPressed || _backFocus.WasActivated))
        {
            _importOfficialPickerOpen = false;
            _activeTab = LevelSource.Local;
            RefreshLevelList();
            return;
        }

        if (_game.Input.UiPointerPressed)
        {
            for (int i = 0; i < _levels.Count && i < _gridLayout.CellBounds.Length; i++)
            {
                if (_gridLayout.CellBounds[i].Contains(_game.Input.UiPointerPosition))
                {
                    string newLevelId = LevelLibrary.ImportOfficialLevel(_levels[i].Id);
                    _importOfficialPickerOpen = false;
                    _activeTab = LevelSource.Local;
                    SelectLevelById(newLevelId);
                    return;
                }
            }
        }

        for (int i = 0; i < _gridFocusables.Count; i++)
        {
            if (_gridFocusables[i].WasActivated && i < _levels.Count)
            {
                string newLevelId = LevelLibrary.ImportOfficialLevel(_levels[i].Id);
                _importOfficialPickerOpen = false;
                _activeTab = LevelSource.Local;
                SelectLevelById(newLevelId);
                return;
            }
        }
    }

    private void UpdateFocusForImportPicker(GameTime gameTime)
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

        int backIndex = _focus.Add(_backFocus, "Back");
        if (gridStart >= 0)
        {
            _focus.Navigation.WireGrid(gridStart, _gridFocusables.Count, _gridLayout.Columns);
            _focus.Navigation.Link(backIndex, NavigationDirection.Up, gridStart);
        }

        _focus.FinalizeFocus(gridStart >= 0 ? "Level0" : "Back");
        _focus.Update(gameTime, _game.Input);
    }

    private void HandleCreateOfficialLevel()
    {
        _popupKind = LevelSelectPopupKind.CreateOfficial;
        int nextNumber = LevelLibrary.GetOfficialLevels().Count + 1;
        _popup = new Popup("Create Official Level", "Enter level name:", $"LEVEL {nextNumber}");
    }

    private void HandleConvertToOfficial()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
        {
            return;
        }

        LevelMetadata metadata = _levels[_selectedIndex.Value];
        if (metadata.Source != LevelSource.Local)
        {
            return;
        }

        if (LevelLibrary.ConvertLocalToOfficial(metadata.Id))
        {
            _activeTab = LevelSource.Official;
            RefreshLevelList();
        }
    }

    private void SelectLevelById(string levelId)
    {
        _activeTab = LevelSource.Local;
        RefreshLevelList(preserveSelection: true);

        int newIndex = -1;
        for (int i = 0; i < _levels.Count; i++)
        {
            if (_levels[i].Id == levelId)
            {
                newIndex = i;
                break;
            }
        }

        if (newIndex < 0)
        {
            _levels = LevelLibrary.GetLocalLevels();
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Id == levelId)
                {
                    newIndex = i;
                    break;
                }
            }
        }

        if (newIndex >= 0)
        {
            _selectedIndex = newIndex;
            UpdateSelectedLevelDetails();
        }
    }

    private void UpdateSelectedLevelDetails()
    {
        if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
        {
            _selectedLevel = null;
            _selectedLevelPreview = null;
            _selectedLevelId = null;
            RefreshDetailsPanelCache();
            return;
        }

        LevelMetadata metadata = _levels[_selectedIndex.Value];
        _selectedLevelId = metadata.Id;
        _selectedLevel = LevelLibrary.LoadLevel(metadata.Id);
        _selectedLevelPreview = LevelPreviewManager.GetPreview(_game.GraphicsDevice, _game.Pixel, _selectedLevel, metadata.Id);
        RefreshDetailsPanelCache();
        ApplyLevelPlayDefaults(_selectedLevel);
    }

    private void RefreshDetailsPanelCache()
    {
        if (_selectedLevel is null || string.IsNullOrEmpty(_selectedLevelId))
        {
            _detailsLevelName = "";
            _detailsAuthorText = "";
            _detailsPlayersText = "";
            _detailsRopeText = "";
            _detailsFeaturesText = "";
            _detailsBestTimeText = "--";
            _detailsUnofficialBestText = "";
            _detailsHasUnofficialBest = false;
            _detailsHasBestReplay = false;
            return;
        }

        _detailsLevelName = _selectedLevel.Name;
        LevelMetadata? metadata = LevelLibrary.GetLevel(_selectedLevelId);
        _detailsAuthorText = metadata?.Author ?? string.Empty;
        _detailsPlayersText = GetPlayerCompatibilityText(_selectedLevel);
        string ropeText = GetRopeText(_selectedLevel);
        _detailsRopeText = string.IsNullOrEmpty(ropeText) ? "None" : ropeText;
        string featureText = GetFeatureText(_selectedLevel);
        _detailsFeaturesText = string.IsNullOrEmpty(featureText) ? "None" : featureText;
        _detailsBestTimeText = BestTimeStorage.TryGetBestTime(_selectedLevelId, out float bestTime)
            ? BestTimeStorage.FormatTime(bestTime)
            : "--";
        _detailsHasUnofficialBest = BestTimeStorage.TryGetUnofficialBestTime(_selectedLevelId, out float unofficialBest);
        _detailsUnofficialBestText = _detailsHasUnofficialBest
            ? BestTimeStorage.FormatTime(unofficialBest)
            : "";
        _detailsHasBestReplay = ReplayStorage.HasValidBestReplay(_selectedLevelId);
    }

    private void ApplyLevelPlayDefaults(Level level)
    {
        if (_mode != LevelSelectMode.PlayMode)
        {
            return;
        }

        if (_lavaRiseCheckbox != null)
        {
            _lavaRiseCheckbox.IsEnabled = true;
            _lavaRiseCheckbox.IsChecked = level.LavaRise;
            s_lavaRiseEnabled = level.LavaRise;
        }

        if (_playerCollisionCheckbox != null)
        {
            _playerCollisionCheckbox.IsEnabled = true;
            _playerCollisionCheckbox.IsChecked = level.PlayerCollision;
            s_playerCollisionEnabled = level.PlayerCollision;
        }

        if (_ropeModeSelector != null)
        {
            // Always allow picking either rope mode; mismatch vs level rules → unofficial run.
            _ropeModeSelector.Options.Clear();
            _ropeModeSelector.Options.Add(RopeGameplayMode.ColoredPhysics);
            _ropeModeSelector.Options.Add(RopeGameplayMode.Neutral);

            s_selectedRopeMode = LevelRules.ClampRopeMode(level, s_selectedRopeMode);
            _ropeModeSelector.CurrentOption = s_selectedRopeMode;
        }

        if (_ghostBestRunCheckbox != null)
        {
            _ghostBestRunCheckbox.IsEnabled = true;
            if (!_detailsHasBestReplay)
            {
                _ghostBestRunCheckbox.IsChecked = false;
                s_ghostBestRunEnabled = false;
            }
        }
    }

    private void HandlePopupConfirmed()
    {
        if (_popup == null)
        {
            return;
        }

        switch (_popupKind)
        {
            case LevelSelectPopupKind.OfficialReadOnly:
                if (_pendingOfficialLevelId is not null)
                {
                    string newLevelId = LevelLibrary.ImportOfficialLevel(_pendingOfficialLevelId);
                    SelectLevelById(newLevelId);
                }

                _pendingOfficialLevelId = null;
                break;

            case LevelSelectPopupKind.CreateNew:
                if (_popup.TextInput is not null)
                {
                    string levelName = _popup.InputValue.Trim();
                    if (!string.IsNullOrEmpty(levelName))
                    {
                        string newLevelId = LevelLibrary.CreateNewLevel(levelName);
                        SelectLevelById(newLevelId);
                    }
                }

                break;

            case LevelSelectPopupKind.CreateOfficial:
                if (_popup.TextInput is not null && DeveloperSettings.DeveloperMode)
                {
                    string levelName = _popup.InputValue.Trim();
                    if (!string.IsNullOrEmpty(levelName))
                    {
                        string newLevelId = LevelLibrary.CreateOfficialLevel(levelName);
                        _activeTab = LevelSource.Official;
                        RefreshLevelList();
                        for (int i = 0; i < _levels.Count; i++)
                        {
                            if (_levels[i].Id == newLevelId)
                            {
                                _selectedIndex = i;
                                UpdateSelectedLevelDetails();
                                break;
                            }
                        }
                    }
                }

                break;

            case LevelSelectPopupKind.Delete:
                if (_selectedIndex.HasValue && _selectedIndex.Value < _levels.Count)
                {
                    LevelMetadata level = _levels[_selectedIndex.Value];
                    LevelLibrary.DeleteLevel(level.Id);
                    RefreshLevelList();
                    _selectedIndex = null;
                }

                break;

            case LevelSelectPopupKind.Info:
                break;
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
        int topOffset = _importOfficialPickerOpen ? 130 : 118;
        int bottomReserved = _mode == LevelSelectMode.EditMode ? GetBottomBarHeight() + 20 : 160;
        return Math.Max(220, _game.Viewport.Height - topOffset - bottomReserved);
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
    }

    private void DrawLevelDetailsPanel(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, bool showPlayStats)
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
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, _detailsLevelName, nameBounds, 2, new Color(210, 220, 235));

        if (!string.IsNullOrWhiteSpace(_detailsAuthorText))
        {
            var authorBounds = new Rectangle(panelX + 16, panelY + 82, panelWidth - 32, 20);
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, $"by {_detailsAuthorText}", authorBounds, 1, new Color(170, 180, 200));
        }

        var previewBounds = new Rectangle(panelX + 15, panelY + 108, panelWidth - 30, Math.Min(180, panelHeight / 4));
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
        SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsPlayersText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
        y += rowHeight + 4;

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "Rope:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
        y += rowHeight;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsRopeText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
        y += rowHeight + 4;

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "Features:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
        y += rowHeight;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsFeaturesText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);

        if (!showPlayStats)
        {
            return;
        }

        y += rowHeight + 4;

        SimpleTextRenderer.DrawString(spriteBatch, pixel, "Best Time:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
        y += rowHeight;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsBestTimeText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
        y += rowHeight + 4;

        if (_detailsHasUnofficialBest)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, "Unofficial Best:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
            y += rowHeight;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsUnofficialBestText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(200, 180, 140));
            y += rowHeight + 8;
        }

        if (_watchReplayButton != null)
        {
            _watchReplayButton.Bounds = new Rectangle(textX, y, textWidth, 44);
            _watchReplayButton.FillColor = _detailsHasBestReplay ? new Color(52, 61, 80) : new Color(40, 46, 58);
            _watchReplayButton.TextColor = _detailsHasBestReplay ? Color.White : new Color(140, 150, 165);
            _watchReplayButton.Draw(spriteBatch, pixel);
            y = _watchReplayButton.Bounds.Bottom + 12;
        }

        // Play options cascade under Watch Replay (flex column inside details panel).
        const int optionHeight = 26;
        const int optionGap = 8;
        if (_lavaRiseCheckbox != null)
        {
            _lavaRiseCheckbox.Bounds = new Rectangle(textX, y, textWidth, optionHeight);
            _lavaRiseCheckbox.Draw(spriteBatch, pixel);
            y += optionHeight + optionGap;
        }

        if (_playerCollisionCheckbox != null)
        {
            _playerCollisionCheckbox.Bounds = new Rectangle(textX, y, textWidth, optionHeight);
            _playerCollisionCheckbox.Draw(spriteBatch, pixel);
            y += optionHeight + optionGap;
        }

        if (_ghostBestRunCheckbox != null)
        {
            _ghostBestRunCheckbox.Bounds = new Rectangle(textX, y, textWidth, optionHeight);
            _ghostBestRunCheckbox.Draw(spriteBatch, pixel);
            y = _ghostBestRunCheckbox.Bounds.Bottom + optionGap;
        }

        if (!AreCurrentPlaySettingsOfficial())
        {
            var warnBounds = new Rectangle(textX, y, textWidth, 40);
            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                "UNOFFICIAL RUN",
                new Microsoft.Xna.Framework.Vector2(textX, y),
                2,
                new Color(230, 70, 70));
            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                "Options won't count for best run",
                new Microsoft.Xna.Framework.Vector2(textX, y + 20),
                1,
                new Color(230, 90, 90));
        }
    }

    private bool AreCurrentPlaySettingsOfficial()
    {
        if (_mode != LevelSelectMode.PlayMode || _selectedLevel is null)
        {
            return true;
        }

        RopeGameplayMode ropeMode = _ropeModeSelector?.CurrentOption ?? s_selectedRopeMode;
        bool lavaRise = _lavaRiseCheckbox?.IsChecked ?? s_lavaRiseEnabled;
        bool playerCollision = _playerCollisionCheckbox?.IsChecked ?? s_playerCollisionEnabled;
        return LevelRules.IsOfficialPlaySettings(
            _selectedLevel,
            ropeMode,
            lavaRise,
            playerCollision,
            _game.Party.Members.Count);
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
        if (level.AnyRope)
        {
            ropeTags.Add("Any");
        }
        else
        {
            if (level.ColoredRope) ropeTags.Add("Colored Rope");
            if (level.RegularRope) ropeTags.Add("Regular Rope");
        }
        return string.Join(", ", ropeTags);
    }

    private string GetFeatureText(Level level)
    {
        var features = new System.Collections.Generic.List<string>();
        if (level.LavaRise) features.Add("Lava Rise");
        if (level.PlayerCollision) features.Add("Player Collision");
        return string.Join(", ", features);
    }
}
