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
    private static GhostMode s_ghostMode = GhostMode.None;

    public static void SyncPlaySettings(
        RopeGameplayMode ropeMode,
        bool lavaRiseEnabled,
        bool playerCollisionEnabled)
    {
        s_selectedRopeMode = ropeMode;
        s_lavaRiseEnabled = lavaRiseEnabled;
        s_playerCollisionEnabled = playerCollisionEnabled;
    }

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
    private int _workshopChangeStamp = -1;
    private string? _detailsWorldRecordText;
    private string? _detailsWorkshopVotesText;
    private string? _detailsWorkshopSubsText;
    private string? _detailsWorkshopPublishedText;
    private string? _detailsWorkshopUpdatedText;
    private string? _detailsWorkshopVisibilityText;
    private bool _detailsHasWorldRecordReplay;
    private bool _detailsSupportsLeaderboards;
    private bool _wrPeekRequested;

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
    private FocusableCycleSelector<GhostMode>? _ghostModeFocus;
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
    private CycleSelector<GhostMode>? _ghostModeSelector;
    private Button? _watchReplayButton;
    private FocusableButton? _watchReplayFocus;
    private Button? _watchWorldRecordButton;
    private FocusableButton? _watchWorldRecordFocus;
    private Button? _leaderboardButton;
    private FocusableButton? _leaderboardFocus;
    private Button? _workshopPrimaryButton;
    private FocusableButton? _workshopPrimaryFocus;
    private Button? _workshopSecondaryButton;
    private FocusableButton? _workshopSecondaryFocus;
    private Button? _workshopTertiaryButton;
    private FocusableButton? _workshopTertiaryFocus;
    private Button? _workshopQuaternaryButton;
    private FocusableButton? _workshopQuaternaryFocus;
    private Button? _workshopQuinaryButton;
    private FocusableButton? _workshopQuinaryFocus;
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
        if (_mode == LevelSelectMode.PlayMode
            && _game.LevelStartRouter.TryConsumePendingStartAlert(out string alertTitle, out string alertMessage))
        {
            _alertPopup = new AlertPopup(alertTitle, alertMessage);
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
        if (_mode == LevelSelectMode.PlayMode && tab == LevelSource.Workshop)
        {
            _game.SteamWorkshop.SyncSubscribedItems();
            _workshopChangeStamp = _game.SteamWorkshop.ChangeStamp;
        }

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

        // Workshop publish/subscribe actions: Play Mode (Local + Workshop tabs) and Edit Mode (Local upload).
        _workshopPrimaryButton = new Button("Upload Workshop");
        _workshopPrimaryFocus = new FocusableButton(_workshopPrimaryButton);
        _workshopSecondaryButton = new Button("Open Workshop");
        _workshopSecondaryFocus = new FocusableButton(_workshopSecondaryButton);
        _workshopTertiaryButton = new Button("Delete Local");
        _workshopTertiaryFocus = new FocusableButton(_workshopTertiaryButton);
        _workshopQuaternaryButton = new Button("Unsubscribe");
        _workshopQuaternaryFocus = new FocusableButton(_workshopQuaternaryButton);
        _workshopQuinaryButton = new Button("Create Copy");
        _workshopQuinaryFocus = new FocusableButton(_workshopQuinaryButton);
        _workshopChangeStamp = _game.SteamWorkshop.ChangeStamp;

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

            _ghostModeSelector = new CycleSelector<GhostMode>(
                new List<GhostMode>
                {
                    GhostMode.None,
                    GhostMode.PersonalBest,
                    GhostMode.WorldRecord,
                    GhostMode.Both
                },
                mode => mode.ToDisplayName())
            {
                CurrentOption = s_ghostMode
            };

            _watchReplayButton = new Button("Watch Replay");
            _watchReplayFocus = new FocusableButton(_watchReplayButton);
            _watchWorldRecordButton = new Button("Watch WR Replay");
            _watchWorldRecordFocus = new FocusableButton(_watchWorldRecordButton);
            _leaderboardButton = new Button("Leaderboard");
            _leaderboardFocus = new FocusableButton(_leaderboardButton);

            _ropeModeFocus = new FocusableCycleSelector<RopeGameplayMode>(_ropeModeSelector);
            _lavaRiseFocus = new FocusableCheckbox(_lavaRiseCheckbox);
            _playerCollisionFocus = new FocusableCheckbox(_playerCollisionCheckbox);
            _ghostModeFocus = new FocusableCycleSelector<GhostMode>(_ghostModeSelector);
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

        if (_mode == LevelSelectMode.PlayMode
            && _game.LevelStartRouter.TryConsumePendingStartAlert(out string alertTitle, out string alertMessage))
        {
            _alertPopup = new AlertPopup(alertTitle, alertMessage);
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

        if (_mode == LevelSelectMode.PlayMode
            && _game.SteamWorkshop.ChangeStamp != _workshopChangeStamp)
        {
            _workshopChangeStamp = _game.SteamWorkshop.ChangeStamp;
            RefreshLevelList(preserveSelection: true);
            if (_selectedIndex.HasValue)
            {
                UpdateSelectedLevelDetails();
            }
        }

        if (_mode == LevelSelectMode.PlayMode
            && _detailsWorkshopVotesText == "…"
            && TryGetSelectedWorkshopId(out ulong pendingWorkshopId))
        {
            WorkshopItemDetails? details = _game.SteamWorkshop.GetDetails(pendingWorkshopId);
            if (details is not null)
            {
                _detailsWorkshopVotesText = $"+{details.VotesUp} / -{details.VotesDown}";
                _detailsWorkshopSubsText = details.Subscribers.ToString();
            }
        }

        UpdateFocus(gameTime);

        if (_backFocus.WasActivated || _game.Input.ExitPressed || (_game.Input.MenuCancelPressed && !_focus.IsCapturingNavigation))
        {
            if (_mode == LevelSelectMode.PlayMode && _game.SteamLobby.IsInLobby)
            {
                _game.ChangeScene(new PartyScene(_game));
            }
            else
            {
                _game.ChangeScene(new MenuScene(_game));
            }

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

        if (_ghostModeSelector != null)
        {
            s_ghostMode = ClampGhostMode(_ghostModeSelector.CurrentOption, _selectedLevelId);
            if (_ghostModeSelector.CurrentOption != s_ghostMode)
            {
                _ghostModeSelector.CurrentOption = s_ghostMode;
            }
        }

        if (_watchReplayFocus?.WasActivated == true && _selectedLevelId is not null
            && _detailsHasBestReplay)
        {
            _game.ChangeScene(new ReplayViewerScene(_game, _selectedLevelId));
            return;
        }

        if (_watchWorldRecordFocus?.WasActivated == true
            && _selectedLevelId is not null
            && _detailsSupportsLeaderboards)
        {
            HandleWatchWorldRecord();
            return;
        }

        if (_leaderboardFocus?.WasActivated == true
            && _selectedLevelId is not null
            && _detailsSupportsLeaderboards)
        {
            _game.ChangeScene(new LeaderboardScene(_game, _selectedLevelId, _mode));
            return;
        }

        if (HandleWorkshopActionButtons())
        {
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
            _watchReplayFocus.IsEnabled = _detailsHasBestReplay;
            watchReplayIndex = _focus.Add(_watchReplayFocus, "WatchReplay");
        }

        int? watchWrIndex = null;
        if (_watchWorldRecordFocus != null && _detailsSupportsLeaderboards)
        {
            _watchWorldRecordFocus.IsEnabled = true;
            watchWrIndex = _focus.Add(_watchWorldRecordFocus, "WatchWorldRecord");
        }

        int? leaderboardIndex = null;
        if (_leaderboardFocus != null && _detailsSupportsLeaderboards)
        {
            _leaderboardFocus.IsEnabled = true;
            leaderboardIndex = _focus.Add(_leaderboardFocus, "Leaderboard");
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
        if (_ghostModeFocus != null)
        {
            ghostIndex = _focus.Add(_ghostModeFocus, "GhostMode");
        }

        int? workshopPrimaryIndex = null;
        int? workshopSecondaryIndex = null;
        int? workshopTertiaryIndex = null;
        int? workshopQuaternaryIndex = null;
        int? workshopQuinaryIndex = null;
        if (ShouldShowWorkshopPrimary() && _workshopPrimaryFocus != null)
        {
            workshopPrimaryIndex = _focus.Add(_workshopPrimaryFocus, "WorkshopPrimary");
        }

        if (ShouldShowWorkshopSecondary() && _workshopSecondaryFocus != null)
        {
            workshopSecondaryIndex = _focus.Add(_workshopSecondaryFocus, "WorkshopSecondary");
        }

        if (ShouldShowWorkshopTertiary() && _workshopTertiaryFocus != null)
        {
            workshopTertiaryIndex = _focus.Add(_workshopTertiaryFocus, "WorkshopTertiary");
        }

        if (ShouldShowWorkshopQuaternary() && _workshopQuaternaryFocus != null)
        {
            workshopQuaternaryIndex = _focus.Add(_workshopQuaternaryFocus, "WorkshopQuaternary");
        }

        if (ShouldShowWorkshopQuinary() && _workshopQuinaryFocus != null)
        {
            workshopQuinaryIndex = _focus.Add(_workshopQuinaryFocus, "WorkshopQuinary");
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

                int? editActionCursor = importIndex;
                if (workshopPrimaryIndex is int editUpload && editActionCursor is int fromImport)
                {
                    nav.LinkHorizontal(fromImport, editUpload);
                    editActionCursor = editUpload;
                }
                else if (workshopPrimaryIndex is int editUploadOnly)
                {
                    editActionCursor = editUploadOnly;
                }

                if (workshopSecondaryIndex is int editOpen && editActionCursor is int fromUpload)
                {
                    nav.LinkHorizontal(fromUpload, editOpen);
                    editActionCursor = editOpen;
                }

                if (createOfficialIndex is int createOfficial && editActionCursor is int fromEditAction)
                {
                    nav.LinkHorizontal(fromEditAction, createOfficial);
                }
                else if (createOfficialIndex is int createOfficialOnly && importIndex is int importFromCreate)
                {
                    nav.LinkHorizontal(importFromCreate, createOfficialOnly);
                }

                if (convertOfficialIndex is int convertOfficial && createOfficialIndex is int createFromConvert)
                {
                    nav.LinkHorizontal(createFromConvert, convertOfficial);
                }
                else if (convertOfficialIndex is int convertOnly && editActionCursor is int fromConvertBase)
                {
                    nav.LinkHorizontal(fromConvertBase, convertOnly);
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

                // Details: Watch PB → Watch WR → Leaderboard → Lava → Collision → Ghost → Play
                int? detailsCursor = watchReplayIndex;
                if (detailsCursor is int watch && watchWrIndex is int watchWr)
                {
                    nav.LinkVertical(watch, watchWr);
                    detailsCursor = watchWr;
                }
                else if (watchWrIndex is int watchWrOnly)
                {
                    detailsCursor = watchWrOnly;
                }

                if (detailsCursor is int fromWatch && leaderboardIndex is int leaderboard)
                {
                    nav.LinkVertical(fromWatch, leaderboard);
                    detailsCursor = leaderboard;
                }
                else if (leaderboardIndex is int leaderboardOnly && detailsCursor is null)
                {
                    detailsCursor = leaderboardOnly;
                }

                if (detailsCursor is int fromLb && lavaIndex is int lava)
                {
                    nav.LinkVertical(fromLb, lava);
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
                else if (watchWrIndex is int wrNav && _gridFocusables.Count > 0)
                {
                    int topRight = gridStart + Math.Min(_gridLayout.Columns - 1, _gridFocusables.Count - 1);
                    nav.Link(topRight, NavigationDirection.Right, wrNav);
                    nav.Link(wrNav, NavigationDirection.Left, topRight);
                }
                else if (leaderboardIndex is int lbNav && _gridFocusables.Count > 0)
                {
                    int topRight = gridStart + Math.Min(_gridLayout.Columns - 1, _gridFocusables.Count - 1);
                    nav.Link(topRight, NavigationDirection.Right, lbNav);
                    nav.Link(lbNav, NavigationDirection.Left, topRight);
                }

                // Workshop action strip (bottom): Back → actions → Play
                int? actionCursor = null;
                void LinkWorkshopAction(int actionIndex)
                {
                    if (actionCursor is int fromAction)
                    {
                        nav.LinkHorizontal(fromAction, actionIndex);
                    }
                    else
                    {
                        nav.LinkHorizontal(backIndex, actionIndex);
                    }

                    actionCursor = actionIndex;
                }

                if (workshopPrimaryIndex is int wp)
                {
                    LinkWorkshopAction(wp);
                }

                if (workshopSecondaryIndex is int ws)
                {
                    LinkWorkshopAction(ws);
                }

                if (workshopTertiaryIndex is int wt)
                {
                    LinkWorkshopAction(wt);
                }

                if (workshopQuaternaryIndex is int wq)
                {
                    LinkWorkshopAction(wq);
                }

                if (workshopQuinaryIndex is int w5)
                {
                    LinkWorkshopAction(w5);
                }

                if (primaryIndex is int playBtn)
                {
                    if (actionCursor is int fromAction)
                    {
                        nav.LinkHorizontal(fromAction, playBtn);
                    }
                    else
                    {
                        nav.LinkHorizontal(backIndex, playBtn);
                    }
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

        if (_mode == LevelSelectMode.PlayMode)
        {
            if (ShouldShowWorkshopPrimary())
            {
                _workshopPrimaryButton?.Draw(spriteBatch, pixel);
            }

            if (ShouldShowWorkshopSecondary())
            {
                _workshopSecondaryButton?.Draw(spriteBatch, pixel);
            }

            if (ShouldShowWorkshopTertiary())
            {
                _workshopTertiaryButton?.Draw(spriteBatch, pixel);
            }

            if (ShouldShowWorkshopQuaternary())
            {
                _workshopQuaternaryButton?.Draw(spriteBatch, pixel);
            }

            if (ShouldShowWorkshopQuinary())
            {
                _workshopQuinaryButton?.Draw(spriteBatch, pixel);
            }
        }
        else if (_mode == LevelSelectMode.EditMode)
        {
            if (ShouldShowWorkshopPrimary())
            {
                _workshopPrimaryButton?.Draw(spriteBatch, pixel);
            }

            if (ShouldShowWorkshopSecondary())
            {
                _workshopSecondaryButton?.Draw(spriteBatch, pixel);
            }
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
            RefreshWorkshopActionLabels();

            var labels = new List<string> { "Play" };
            if (ShouldShowWorkshopPrimary())
            {
                labels.Insert(0, _workshopPrimaryButton!.Text);
            }

            if (ShouldShowWorkshopSecondary())
            {
                labels.Insert(ShouldShowWorkshopPrimary() ? 1 : 0, _workshopSecondaryButton!.Text);
            }

            if (ShouldShowWorkshopTertiary())
            {
                int insertAt = labels.Count - 1;
                labels.Insert(insertAt, _workshopTertiaryButton!.Text);
            }

            if (ShouldShowWorkshopQuaternary())
            {
                int insertAt = labels.Count - 1;
                labels.Insert(insertAt, _workshopQuaternaryButton!.Text);
            }

            if (ShouldShowWorkshopQuinary())
            {
                int insertAt = labels.Count - 1;
                labels.Insert(insertAt, _workshopQuinaryButton!.Text);
            }

            var layout = ButtonRowLayout.Create(
                labels.ToArray(),
                viewport.Width, viewport.Height,
                buttonHeight, horizontalPadding, 12, buttonGap, bottomMargin);

            int boundIndex = 0;
            if (ShouldShowWorkshopPrimary() && boundIndex < layout.ButtonBounds.Length)
            {
                _workshopPrimaryButton!.Bounds = layout.ButtonBounds[boundIndex++];
            }

            if (ShouldShowWorkshopSecondary() && boundIndex < layout.ButtonBounds.Length)
            {
                _workshopSecondaryButton!.Bounds = layout.ButtonBounds[boundIndex++];
            }

            if (ShouldShowWorkshopTertiary() && boundIndex < layout.ButtonBounds.Length)
            {
                _workshopTertiaryButton!.Bounds = layout.ButtonBounds[boundIndex++];
            }

            if (ShouldShowWorkshopQuaternary() && boundIndex < layout.ButtonBounds.Length)
            {
                _workshopQuaternaryButton!.Bounds = layout.ButtonBounds[boundIndex++];
            }

            if (ShouldShowWorkshopQuinary() && boundIndex < layout.ButtonBounds.Length)
            {
                _workshopQuinaryButton!.Bounds = layout.ButtonBounds[boundIndex++];
            }

            if (boundIndex < layout.ButtonBounds.Length)
            {
                _primaryButton!.Bounds = layout.ButtonBounds[boundIndex];
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

            RefreshWorkshopActionLabels();
            var secondRowLabels = new List<string> { "Import Official" };
            if (ShouldShowWorkshopPrimary())
            {
                secondRowLabels.Add(_workshopPrimaryButton!.Text);
            }

            if (ShouldShowWorkshopSecondary())
            {
                secondRowLabels.Add(_workshopSecondaryButton!.Text);
            }

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

            int secondIndex = 0;
            if (secondRow.ButtonBounds.Length > secondIndex && _importOfficialButton is not null)
            {
                _importOfficialButton.Bounds = secondRow.ButtonBounds[secondIndex++];
            }

            if (ShouldShowWorkshopPrimary() && secondIndex < secondRow.ButtonBounds.Length)
            {
                _workshopPrimaryButton!.Bounds = secondRow.ButtonBounds[secondIndex++];
            }

            if (ShouldShowWorkshopSecondary() && secondIndex < secondRow.ButtonBounds.Length)
            {
                _workshopSecondaryButton!.Bounds = secondRow.ButtonBounds[secondIndex++];
            }

            if (DeveloperSettings.DeveloperMode)
            {
                if (secondIndex < secondRow.ButtonBounds.Length && _createOfficialButton is not null)
                {
                    _createOfficialButton.Bounds = secondRow.ButtonBounds[secondIndex++];
                }

                if (secondIndex < secondRow.ButtonBounds.Length && _convertToOfficialButton is not null)
                {
                    _convertToOfficialButton.Bounds = secondRow.ButtonBounds[secondIndex];
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
            s_ghostMode = ClampGhostMode(_ghostModeSelector?.CurrentOption ?? s_ghostMode, levelId);

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
                s_ghostMode,
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
            _detailsSupportsLeaderboards = false;
            _detailsHasWorldRecordReplay = false;
            _detailsWorldRecordText = null;
            _detailsWorkshopVotesText = null;
            _detailsWorkshopSubsText = null;
            _detailsWorkshopPublishedText = null;
            _detailsWorkshopUpdatedText = null;
            _detailsWorkshopVisibilityText = null;
            _wrPeekRequested = false;
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
        _detailsSupportsLeaderboards = SteamLeaderboardService.SupportsLeaderboards(_selectedLevelId);
        int wrPlayers = GetLeaderboardPlayerCount();
        _detailsHasWorldRecordReplay = SteamGhostService.SupportsWorldRecordGhost(_selectedLevelId)
            && _game.SteamGhosts.HasCachedWorldRecordGhost(_selectedLevelId, wrPlayers);
        _detailsWorldRecordText = null;
        _detailsWorkshopVotesText = null;
        _detailsWorkshopSubsText = null;
        _detailsWorkshopPublishedText = null;
        _detailsWorkshopUpdatedText = null;
        _detailsWorkshopVisibilityText = null;
        _wrPeekRequested = false;

        LevelSource source = LevelIdentity.GetSource(_selectedLevelId);
        if (source == LevelSource.Workshop
            && metadata is not null
            && ulong.TryParse(metadata.WorkshopId, out ulong workshopId)
            && workshopId != 0)
        {
            WorkshopItemDetails? details = _game.SteamWorkshop.GetDetails(workshopId);
            if (details is not null)
            {
                ApplyWorkshopDetails(details);
            }
            else
            {
                _detailsWorkshopVotesText = "…";
                _detailsWorkshopSubsText = "…";
                _detailsWorkshopPublishedText = "…";
                _detailsWorkshopUpdatedText = "…";
                _detailsWorkshopVisibilityText = "…";
            }
        }

        if (_detailsSupportsLeaderboards)
        {
            RequestWorldRecordPeek(_selectedLevelId, Math.Max(1, metadata?.Version ?? 1), wrPlayers);
            if (SteamGhostService.SupportsWorldRecordGhost(_selectedLevelId))
            {
                string levelId = _selectedLevelId;
                int ensurePlayers = wrPlayers;
                _game.SteamGhosts.EnsureWorldRecordGhost(levelId, ensurePlayers, ready =>
                {
                    if (_selectedLevelId == levelId)
                    {
                        _detailsHasWorldRecordReplay = ready;
                    }
                });
            }
        }
    }

    private void HandleWatchWorldRecord()
    {
        if (_selectedLevelId is null || !_detailsSupportsLeaderboards)
        {
            return;
        }

        string levelId = _selectedLevelId;
        int wrPlayers = GetLeaderboardPlayerCount();
        string path = SteamGhostService.GetWorldRecordGhostPath(levelId, wrPlayers);

        if (_detailsHasWorldRecordReplay
            && _game.SteamGhosts.HasCachedWorldRecordGhost(levelId, wrPlayers))
        {
            _game.ChangeScene(new ReplayViewerScene(_game, levelId, path));
            return;
        }

        _alertPopup = new AlertPopup("WORLD RECORD", "Downloading world record replay…");
        _game.SteamGhosts.EnsureWorldRecordGhost(levelId, wrPlayers, ready =>
        {
            if (_selectedLevelId != levelId)
            {
                return;
            }

            _detailsHasWorldRecordReplay = ready;
            if (!ready)
            {
                _alertPopup = new AlertPopup("WORLD RECORD", "World record replay is not available yet.");
                return;
            }

            _alertPopup = null;
            _game.ChangeScene(new ReplayViewerScene(_game, levelId, path));
        });
    }

    private int GetLeaderboardPlayerCount() =>
        SteamLeaderboardService.ClampPlayerCount(Math.Max(1, _game.Party.Members.Count));

    private void RequestWorldRecordPeek(string levelId, int levelVersion, int playerCount)
    {
        if (_wrPeekRequested || !_game.SteamLeaderboards.IsAvailable)
        {
            return;
        }

        _wrPeekRequested = true;
        _detailsWorldRecordText = "…";
        int clamped = SteamLeaderboardService.ClampPlayerCount(playerCount);
        _game.SteamLeaderboards.DownloadEntries(
            levelId,
            levelVersion,
            clamped,
            LeaderboardScope.GlobalTop,
            1,
            entries =>
            {
                if (_selectedLevelId != levelId)
                {
                    return;
                }

                if (entries is null || entries.Count == 0)
                {
                    _detailsWorldRecordText = "--";
                    return;
                }

                _detailsWorldRecordText = BestTimeStorage.FormatTime(entries[0].TimeSeconds);
            });
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

        if (_ghostModeSelector != null && _selectedLevelId is not null)
        {
            RefreshGhostModeOptions(_selectedLevelId);
            s_ghostMode = ClampGhostMode(s_ghostMode, _selectedLevelId);
            _ghostModeSelector.CurrentOption = s_ghostMode;
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

        if (_detailsSupportsLeaderboards)
        {
            int wrPlayers = GetLeaderboardPlayerCount();
            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                wrPlayers == 1 ? "World Record:" : $"World Record ({wrPlayers}P):",
                new Microsoft.Xna.Framework.Vector2(textX, y),
                2,
                new Color(180, 190, 210));
            y += rowHeight;
            SimpleTextRenderer.DrawString(
                spriteBatch,
                pixel,
                _detailsWorldRecordText ?? "--",
                new Microsoft.Xna.Framework.Vector2(textX, y),
                2,
                new Color(255, 210, 90));
            y += rowHeight + 4;
        }

        if (_detailsWorkshopVotesText is not null)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, "Workshop Rating:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
            y += rowHeight;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsWorkshopVotesText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
            y += rowHeight + 4;
        }

        if (_detailsWorkshopSubsText is not null)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, "Subscribers:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
            y += rowHeight;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsWorkshopSubsText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
            y += rowHeight + 4;
        }

        if (_detailsWorkshopVisibilityText is not null)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, "Visibility:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
            y += rowHeight;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsWorkshopVisibilityText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
            y += rowHeight + 4;
        }

        if (_detailsWorkshopPublishedText is not null)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, "Published:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
            y += rowHeight;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsWorkshopPublishedText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
            y += rowHeight + 4;
        }

        if (_detailsWorkshopUpdatedText is not null)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, "Updated:", new Microsoft.Xna.Framework.Vector2(textX, y), 2, new Color(180, 190, 210));
            y += rowHeight;
            SimpleTextRenderer.DrawString(spriteBatch, pixel, _detailsWorkshopUpdatedText, new Microsoft.Xna.Framework.Vector2(textX, y), 2, Color.White);
            y += rowHeight + 4;
        }

        if (_watchReplayButton != null)
        {
            _watchReplayButton.Bounds = new Rectangle(textX, y, textWidth, 40);
            _watchReplayButton.FillColor = _detailsHasBestReplay ? new Color(52, 61, 80) : new Color(40, 46, 58);
            _watchReplayButton.TextColor = _detailsHasBestReplay ? Color.White : new Color(140, 150, 165);
            _watchReplayButton.Draw(spriteBatch, pixel);
            y = _watchReplayButton.Bounds.Bottom + 8;
        }

        if (_watchWorldRecordButton != null && _detailsSupportsLeaderboards)
        {
            bool wrReady = _detailsHasWorldRecordReplay || _detailsWorldRecordText is not null && _detailsWorldRecordText != "--";
            _watchWorldRecordButton.Bounds = new Rectangle(textX, y, textWidth, 40);
            _watchWorldRecordButton.FillColor = wrReady ? new Color(52, 61, 80) : new Color(40, 46, 58);
            _watchWorldRecordButton.TextColor = wrReady ? Color.White : new Color(140, 150, 165);
            _watchWorldRecordButton.Draw(spriteBatch, pixel);
            y = _watchWorldRecordButton.Bounds.Bottom + 8;
        }

        if (_leaderboardButton != null && _detailsSupportsLeaderboards)
        {
            _leaderboardButton.Bounds = new Rectangle(textX, y, textWidth, 40);
            _leaderboardButton.FillColor = new Color(52, 61, 80);
            _leaderboardButton.TextColor = Color.White;
            _leaderboardButton.Draw(spriteBatch, pixel);
            y = _leaderboardButton.Bounds.Bottom + 10;
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

        if (_ghostModeSelector != null)
        {
            SimpleTextRenderer.DrawString(spriteBatch, pixel, "Ghost:", new Microsoft.Xna.Framework.Vector2(textX, y), 1, new Color(180, 190, 210));
            y += 18;
            _ghostModeSelector.Bounds = new Rectangle(textX, y, textWidth, 36);
            _ghostModeSelector.Draw(spriteBatch, pixel);
            y = _ghostModeSelector.Bounds.Bottom + optionGap;
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

    private bool HandleWorkshopActionButtons()
    {
        if (ShouldShowWorkshopPrimary() && _workshopPrimaryFocus?.WasActivated == true)
        {
            HandleWorkshopPrimaryAction();
            return true;
        }

        if (ShouldShowWorkshopSecondary() && _workshopSecondaryFocus?.WasActivated == true)
        {
            HandleWorkshopSecondaryAction();
            return true;
        }

        if (ShouldShowWorkshopTertiary() && _workshopTertiaryFocus?.WasActivated == true)
        {
            HandleWorkshopTertiaryAction();
            return true;
        }

        if (ShouldShowWorkshopQuaternary() && _workshopQuaternaryFocus?.WasActivated == true)
        {
            HandleWorkshopQuaternaryAction();
            return true;
        }

        if (ShouldShowWorkshopQuinary() && _workshopQuinaryFocus?.WasActivated == true)
        {
            HandleCreateCopy();
            return true;
        }

        return false;
    }

    private void RefreshWorkshopActionLabels()
    {
        if (_workshopPrimaryButton is null || _workshopSecondaryButton is null || _workshopTertiaryButton is null)
        {
            return;
        }

        if (_activeTab == LevelSource.Local)
        {
            LevelMetadata? metadata = _selectedLevelId is not null ? LevelLibrary.GetLevel(_selectedLevelId) : null;
            bool hasWorkshopId = metadata is not null
                && ulong.TryParse(metadata.WorkshopId, out ulong id)
                && id != 0;
            _workshopPrimaryButton.Text = hasWorkshopId ? "Update Workshop" : "Upload Workshop";
            _workshopSecondaryButton.Text = "Open Workshop";
            _workshopTertiaryButton.Text = "Delete Local";
        }
        else if (_activeTab == LevelSource.Workshop)
        {
            _workshopPrimaryButton.Text = "Browse Workshop";
            _workshopSecondaryButton.Text = "Subscribe";
            _workshopTertiaryButton.Text = "Open Workshop";
            if (_workshopQuaternaryButton is not null)
            {
                _workshopQuaternaryButton.Text = "Unsubscribe";
            }

            if (_workshopQuinaryButton is not null)
            {
                _workshopQuinaryButton.Text = "Create Copy";
            }
        }
    }

    private bool ShouldShowWorkshopPrimary()
    {
        if (_activeTab == LevelSource.Workshop && _mode == LevelSelectMode.PlayMode)
        {
            return true;
        }

        return _selectedLevelId is not null
            && _activeTab == LevelSource.Local
            && (_mode == LevelSelectMode.PlayMode || _mode == LevelSelectMode.EditMode);
    }

    private bool ShouldShowWorkshopSecondary()
    {
        if (_selectedLevelId is null || !TryGetSelectedWorkshopId(out _))
        {
            return false;
        }

        if (_activeTab == LevelSource.Workshop && _mode == LevelSelectMode.PlayMode)
        {
            return true;
        }

        return _activeTab == LevelSource.Local
            && (_mode == LevelSelectMode.PlayMode || _mode == LevelSelectMode.EditMode);
    }

    private bool ShouldShowWorkshopTertiary()
    {
        if (_mode != LevelSelectMode.PlayMode || _selectedLevelId is null)
        {
            return false;
        }

        if (_activeTab == LevelSource.Local)
        {
            return true;
        }

        return _activeTab == LevelSource.Workshop && TryGetSelectedWorkshopId(out _);
    }

    private bool ShouldShowWorkshopQuaternary() =>
        _mode == LevelSelectMode.PlayMode
        && _activeTab == LevelSource.Workshop
        && _selectedLevelId is not null
        && TryGetSelectedWorkshopId(out _);

    private bool ShouldShowWorkshopQuinary() =>
        _mode == LevelSelectMode.PlayMode
        && _activeTab == LevelSource.Workshop
        && _selectedLevelId is not null;

    private bool TryGetSelectedWorkshopId(out ulong workshopId)
    {
        workshopId = 0;
        if (_selectedLevelId is null)
        {
            return false;
        }

        LevelMetadata? metadata = LevelLibrary.GetLevel(_selectedLevelId);
        return metadata is not null
            && ulong.TryParse(metadata.WorkshopId, out workshopId)
            && workshopId != 0;
    }

    private void ApplyWorkshopDetails(WorkshopItemDetails details)
    {
        _detailsWorkshopVotesText = $"+{details.VotesUp} / -{details.VotesDown}";
        _detailsWorkshopSubsText = details.Subscribers.ToString();
        _detailsWorkshopVisibilityText = details.VisibilityLabel;
        _detailsWorkshopPublishedText = details.PublishedDateUtc == default
            ? "--"
            : details.PublishedDateUtc.ToLocalTime().ToString("yyyy-MM-dd");
        _detailsWorkshopUpdatedText = details.UpdatedDateUtc == default
            ? "--"
            : details.UpdatedDateUtc.ToLocalTime().ToString("yyyy-MM-dd");
    }

    private void HandleWorkshopPrimaryAction()
    {
        if (_activeTab == LevelSource.Workshop)
        {
            _game.SteamWorkshop.OpenWorkshopHub();
            return;
        }

        if (_activeTab != LevelSource.Local || _selectedLevelId is null)
        {
            return;
        }

        if (!_game.SteamWorkshop.IsAvailable)
        {
            _alertPopup = new AlertPopup("WORKSHOP", "Steam is not available.");
            return;
        }

        if (_game.SteamWorkshop.IsPublishing)
        {
            _alertPopup = new AlertPopup("WORKSHOP", "Upload already in progress.");
            return;
        }

        string levelId = _selectedLevelId;
        _game.SteamWorkshop.PublishLevel(levelId, result =>
        {
            if (_selectedLevelId != levelId)
            {
                return;
            }

            if (result.Success)
            {
                RefreshLevelList(preserveSelection: true);
                UpdateSelectedLevelDetails();
                _alertPopup = new AlertPopup(
                    "WORKSHOP",
                    result.NeedsLegalAgreement
                        ? "Published. Accept the Steam Workshop legal agreement if prompted."
                        : "Level published to Workshop.");
            }
            else
            {
                _alertPopup = new AlertPopup("WORKSHOP", result.Message);
            }
        });
    }

    private void HandleWorkshopSecondaryAction()
    {
        if (_activeTab == LevelSource.Workshop)
        {
            if (!TryGetSelectedWorkshopId(out ulong subscribeId))
            {
                return;
            }

            if (!_game.SteamWorkshop.IsAvailable)
            {
                _alertPopup = new AlertPopup("WORKSHOP", "Steam is not available.");
                return;
            }

            _game.SteamWorkshop.Subscribe(subscribeId, ok =>
            {
                if (!ok)
                {
                    _alertPopup = new AlertPopup("WORKSHOP", "Subscribe failed.");
                    return;
                }

                _game.SteamWorkshop.SyncSubscribedItems();
                _workshopChangeStamp = _game.SteamWorkshop.ChangeStamp;
                RefreshLevelList(preserveSelection: true);
                _alertPopup = new AlertPopup("WORKSHOP", "Subscribed. Downloading level…");
            });
            return;
        }

        if (!TryGetSelectedWorkshopId(out ulong workshopId))
        {
            return;
        }

        _game.SteamWorkshop.OpenWorkshopPage(workshopId);
    }

    private void HandleWorkshopTertiaryAction()
    {
        if (_selectedLevelId is null)
        {
            return;
        }

        if (_activeTab == LevelSource.Local)
        {
            if (!_selectedIndex.HasValue || _selectedIndex.Value >= _levels.Count)
            {
                return;
            }

            _popupKind = LevelSelectPopupKind.Delete;
            LevelMetadata level = _levels[_selectedIndex.Value];
            _popup = new Popup("Delete Level", $"Delete '{level.Name}' permanently?");
            return;
        }

        if (_activeTab == LevelSource.Workshop && TryGetSelectedWorkshopId(out ulong workshopId))
        {
            _game.SteamWorkshop.OpenWorkshopPage(workshopId);
        }
    }

    private void HandleWorkshopQuaternaryAction()
    {
        if (_activeTab != LevelSource.Workshop
            || _selectedLevelId is null
            || !TryGetSelectedWorkshopId(out ulong workshopId))
        {
            return;
        }

        string levelId = _selectedLevelId;
        _game.SteamWorkshop.Unsubscribe(workshopId, ok =>
        {
            if (!ok)
            {
                _alertPopup = new AlertPopup("WORKSHOP", "Unsubscribe failed.");
                return;
            }

            if (_selectedLevelId == levelId)
            {
                _selectedIndex = null;
                _selectedLevel = null;
                _selectedLevelId = null;
            }

            RefreshLevelList();
        });
    }

    private void RefreshGhostModeOptions(string levelId)
    {
        if (_ghostModeSelector is null)
        {
            return;
        }

        bool supportsWr = SteamGhostService.SupportsWorldRecordGhost(levelId);
        _ghostModeSelector.Options.Clear();
        _ghostModeSelector.Options.Add(GhostMode.None);
        _ghostModeSelector.Options.Add(GhostMode.PersonalBest);
        if (supportsWr)
        {
            _ghostModeSelector.Options.Add(GhostMode.WorldRecord);
            _ghostModeSelector.Options.Add(GhostMode.Both);
        }
    }

    private GhostMode ClampGhostMode(GhostMode mode, string? levelId)
    {
        if (levelId is null)
        {
            return GhostMode.None;
        }

        bool supportsWr = SteamGhostService.SupportsWorldRecordGhost(levelId);
        bool hasPb = ReplayStorage.HasValidBestReplay(levelId);

        GhostMode result = mode;
        if (!supportsWr && result.IncludesWorldRecord())
        {
            result = result == GhostMode.Both ? GhostMode.PersonalBest : GhostMode.None;
        }

        if (!hasPb && result.IncludesPersonalBest())
        {
            result = result == GhostMode.Both ? GhostMode.WorldRecord : GhostMode.None;
            if (!supportsWr && result == GhostMode.WorldRecord)
            {
                result = GhostMode.None;
            }
        }

        return result;
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
