#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ColorBlocks.Replay;

namespace ColorBlocks;

/// <summary>
/// Dedicated Steam leaderboard browser for Official and Workshop levels.
/// One Steam board per level AND player count (stable name, no version suffix).
/// Switching 1P/2P/3P/4P loads a different board. Per-row Replay opens that entry's UGC.
/// Sticky local PB footer jumps to Around You when off-screen.
/// </summary>
public sealed class LeaderboardScene : IScene
{
    private readonly ColorBlocksGame _game;
    private readonly string _levelId;
    private readonly int _levelVersion;
    private readonly string _levelName;
    private readonly LevelSelectMode _returnMode;

    private readonly Button _backButton = new("Back") { TextScale = 2 };
    private readonly Button _globalButton = new("Global") { TextScale = 2 };
    private readonly Button _friendsButton = new("Friends") { TextScale = 2 };
    private readonly Button _aroundYouButton = new("Around You") { TextScale = 2 };
    private readonly CycleSelector<int> _playerCountSelector;
    private readonly UIFocusManager _focus = new();
    private readonly FocusableButton _backFocus;
    private readonly FocusableButton _globalFocus;
    private readonly FocusableButton _friendsFocus;
    private readonly FocusableButton _aroundYouFocus;
    private readonly FocusableCycleSelector<int> _playerCountFocus;

    private LeaderboardScope _scope = LeaderboardScope.GlobalTop;
    private int _playerCount;
    private IReadOnlyList<SteamLeaderboardEntry> _entries = Array.Empty<SteamLeaderboardEntry>();
    private SteamLeaderboardEntry? _localEntry;
    private bool _loading = true;
    private bool _failed;
    private int _scrollOffset;
    private bool _downloadingReplay;
    private bool _scrollToLocalAfterLoad;
    private Rectangle _stickyBounds;
    private Rectangle _stickyReplayBounds;
    private readonly List<Rectangle> _visibleReplayBounds = new(16);
    private readonly List<int> _visibleReplayIndices = new(16);

    private const int MaxEntries = 50;
    private const int RowHeight = 36;

    public LeaderboardScene(ColorBlocksGame game, string levelId, LevelSelectMode returnMode = LevelSelectMode.PlayMode)
    {
        _game = game;
        _levelId = levelId;
        _returnMode = returnMode;
        LevelMetadata? metadata = LevelLibrary.GetLevel(levelId);
        _levelVersion = Math.Max(1, metadata?.Version ?? 1);
        _levelName = metadata?.Name ?? levelId;
        _playerCount = SteamLeaderboardService.ClampPlayerCount(Math.Max(1, game.Party.Members.Count));

        _playerCountSelector = new CycleSelector<int>(
            new List<int> { 1, 2, 3, 4 },
            count => count == 1 ? "1 Player" : $"{count} Players")
        {
            CurrentOption = _playerCount
        };

        _backFocus = new FocusableButton(_backButton);
        _globalFocus = new FocusableButton(_globalButton);
        _friendsFocus = new FocusableButton(_friendsButton);
        _aroundYouFocus = new FocusableButton(_aroundYouButton);
        _playerCountFocus = new FocusableCycleSelector<int>(_playerCountSelector);

        RequestDownload();
    }

    private void RequestDownload()
    {
        _loading = true;
        _failed = false;
        _entries = Array.Empty<SteamLeaderboardEntry>();
        _localEntry = null;
        _scrollOffset = 0;
        _playerCount = SteamLeaderboardService.ClampPlayerCount(_playerCountSelector.CurrentOption);

        if (!_game.SteamLeaderboards.IsAvailable || !SteamLeaderboardService.SupportsLeaderboards(_levelId))
        {
            _loading = false;
            _failed = true;
            return;
        }

        LeaderboardScope scope = _scope;
        int playerCount = _playerCount;
        bool scrollToLocal = _scrollToLocalAfterLoad;
        _scrollToLocalAfterLoad = false;

        _game.SteamLeaderboards.DownloadEntries(_levelId, _levelVersion, playerCount, scope, MaxEntries, entries =>
        {
            if (scope != _scope || playerCount != _playerCount)
            {
                return;
            }

            _loading = false;
            if (entries is null)
            {
                _failed = true;
                _entries = Array.Empty<SteamLeaderboardEntry>();
                return;
            }

            _failed = false;
            _entries = entries;
            TryAdoptLocalFromEntries();
            if (scrollToLocal)
            {
                ScrollToLocalEntry();
            }
        });

        // Always refresh sticky source even when Global top excludes the player.
        _game.SteamLeaderboards.DownloadLocalEntry(_levelId, _levelVersion, playerCount, local =>
        {
            if (playerCount != _playerCount)
            {
                return;
            }

            if (local is not null)
            {
                _localEntry = local;
            }
            else
            {
                TryAdoptLocalFromEntries();
            }
        });
    }

    private void TryAdoptLocalFromEntries()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].IsLocalUser)
            {
                _localEntry = _entries[i];
                return;
            }
        }
    }

    private void ScrollToLocalEntry()
    {
        int index = FindLocalIndexInEntries();
        if (index < 0)
        {
            return;
        }

        int visible = VisibleRowCount();
        int maxScroll = Math.Max(0, _entries.Count - visible);
        _scrollOffset = Math.Clamp(index - visible / 2, 0, maxScroll);
    }

    private int FindLocalIndexInEntries()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].IsLocalUser)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsLocalVisibleInWindow()
    {
        int index = FindLocalIndexInEntries();
        if (index < 0)
        {
            return false;
        }

        int visible = VisibleRowCount();
        return index >= _scrollOffset && index < _scrollOffset + visible;
    }

    private bool ShouldShowStickyPb() =>
        _localEntry is not null && !_loading && !_failed && !IsLocalVisibleInWindow();

    public void Update(GameTime gameTime)
    {
        Layout(gameTime);
        UpdateFocus(gameTime);

        if (_backFocus.WasActivated || _game.Input.ExitPressed || (_game.Input.MenuCancelPressed && !_focus.IsCapturingNavigation))
        {
            _game.ChangeScene(new LevelSelectScene(_game, _returnMode));
            return;
        }

        int selectedPlayers = SteamLeaderboardService.ClampPlayerCount(_playerCountSelector.CurrentOption);
        if (selectedPlayers != _playerCount)
        {
            _playerCount = selectedPlayers;
            RequestDownload();
            return;
        }

        if (_globalFocus.WasActivated && _scope != LeaderboardScope.GlobalTop)
        {
            _scope = LeaderboardScope.GlobalTop;
            RequestDownload();
            return;
        }

        if (_friendsFocus.WasActivated && _scope != LeaderboardScope.Friends)
        {
            _scope = LeaderboardScope.Friends;
            RequestDownload();
            return;
        }

        if (_aroundYouFocus.WasActivated && _scope != LeaderboardScope.AroundUser)
        {
            _scope = LeaderboardScope.AroundUser;
            RequestDownload();
            return;
        }

        if (_game.Input.MenuMoveUpPressed)
        {
            _scrollOffset = Math.Max(0, _scrollOffset - 1);
        }
        else if (_game.Input.MenuMoveDownPressed)
        {
            int maxScroll = Math.Max(0, _entries.Count - VisibleRowCount());
            _scrollOffset = Math.Min(maxScroll, _scrollOffset + 1);
        }

        HandleReplayActivation();
        HandleStickyActivation();
    }

    private void HandleReplayActivation()
    {
        if (_downloadingReplay || _loading || _failed || !_game.Input.UiPointerPressed)
        {
            return;
        }

        Point pointer = _game.Input.UiPointerPosition;
        if (_stickyReplayBounds != Rectangle.Empty
            && _stickyReplayBounds.Contains(pointer)
            && _localEntry is not null)
        {
            TryOpenEntryReplay(_localEntry);
            return;
        }

        for (int i = 0; i < _visibleReplayBounds.Count; i++)
        {
            if (_visibleReplayBounds[i].Contains(pointer))
            {
                int entryIndex = _visibleReplayIndices[i];
                if (entryIndex >= 0 && entryIndex < _entries.Count)
                {
                    TryOpenEntryReplay(_entries[entryIndex]);
                    return;
                }
            }
        }
    }

    private void HandleStickyActivation()
    {
        if (!ShouldShowStickyPb() || _localEntry is null || _downloadingReplay)
        {
            return;
        }

        if (!_game.Input.UiPointerPressed || !_stickyBounds.Contains(_game.Input.UiPointerPosition))
        {
            return;
        }

        if (_stickyReplayBounds.Contains(_game.Input.UiPointerPosition))
        {
            return;
        }

        JumpToLocalOnBoard();
    }

    private void JumpToLocalOnBoard()
    {
        if (_scope == LeaderboardScope.AroundUser)
        {
            ScrollToLocalEntry();
            if (FindLocalIndexInEntries() >= 0)
            {
                return;
            }
        }

        _scope = LeaderboardScope.AroundUser;
        _scrollToLocalAfterLoad = true;
        RequestDownload();
    }

    private void TryOpenEntryReplay(SteamLeaderboardEntry entry)
    {
        if (_downloadingReplay)
        {
            return;
        }

        if (SteamGhostService.ResolveGhostUgcHandle(entry) == 0)
        {
            return;
        }

        _downloadingReplay = true;
        int playerCount = _playerCount;
        string levelId = _levelId;
        LevelSelectMode returnMode = _returnMode;
        _game.SteamGhosts.EnsureEntryReplay(entry, levelId, playerCount, path =>
        {
            _downloadingReplay = false;
            if (string.IsNullOrEmpty(path))
            {
                DiagnosticsLog.Info("Leaderboard", $"Replay download failed level={levelId} rank={entry.Rank}");
                return;
            }

            _game.ChangeScene(new ReplayViewerScene(
                _game,
                levelId,
                path,
                playerCount,
                () => new LeaderboardScene(_game, levelId, returnMode)));
        });
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        Layout(gameTime);
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

        Rectangle titleBounds = new(20, 18, viewport.Width - 40, 40);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "LEADERBOARD", titleBounds, 3, Color.White);

        Rectangle subtitleBounds = new(20, 58, viewport.Width - 40, 24);
        SimpleTextRenderer.DrawCentered(
            spriteBatch,
            pixel,
            $"{_levelName}  ·  v{_levelVersion}  ·  {_playerCount}P",
            subtitleBounds,
            2,
            new Color(180, 190, 210));

        SimpleTextRenderer.DrawCentered(
            spriteBatch,
            pixel,
            "PLAYERS",
            new Rectangle(20, 88, viewport.Width - 40, 16),
            1,
            new Color(167, 178, 198));
        _playerCountSelector.Draw(spriteBatch, pixel);

        _globalButton.FillColor = ScopeFill(LeaderboardScope.GlobalTop);
        _friendsButton.FillColor = ScopeFill(LeaderboardScope.Friends);
        _aroundYouButton.FillColor = ScopeFill(LeaderboardScope.AroundUser);
        _globalButton.Draw(spriteBatch, pixel);
        _friendsButton.Draw(spriteBatch, pixel);
        _aroundYouButton.Draw(spriteBatch, pixel);

        Rectangle table = GetTableBounds(viewport);
        spriteBatch.Draw(pixel, table, new Color(25, 30, 40, 240));
        DrawHelper.DrawBorder(spriteBatch, pixel, table, new Color(95, 110, 135), 2);

        DrawHeader(spriteBatch, pixel, table);

        _visibleReplayBounds.Clear();
        _visibleReplayIndices.Clear();
        _stickyBounds = Rectangle.Empty;
        _stickyReplayBounds = Rectangle.Empty;

        if (_loading)
        {
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Loading...", table, 2, new Color(180, 190, 210));
        }
        else if (_failed)
        {
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Leaderboard unavailable", table, 2, new Color(230, 120, 120));
        }
        else if (_entries.Count == 0 && _localEntry is null)
        {
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "No scores yet", table, 2, new Color(180, 190, 210));
        }
        else
        {
            DrawRows(spriteBatch, pixel, table);
            DrawStickyPb(spriteBatch, pixel, table);
        }

        if (_downloadingReplay)
        {
            SimpleTextRenderer.DrawCentered(
                spriteBatch,
                pixel,
                "Downloading replay...",
                new Rectangle(table.X, table.Bottom - 28, table.Width, 24),
                2,
                new Color(200, 210, 230));
        }

        spriteBatch.Draw(pixel, new Rectangle(0, viewport.Height - 90, viewport.Width, 90), new Color(22, 26, 34));
        _backButton.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);

        spriteBatch.End();
    }

    private void DrawHeader(SpriteBatch spriteBatch, Texture2D pixel, Rectangle table)
    {
        Rectangle header = new(table.X + 8, table.Y + 8, table.Width - 16, 28);
        spriteBatch.Draw(pixel, header, new Color(38, 46, 62));
        DrawColumnLabels(spriteBatch, pixel, header, new Color(167, 178, 198));
    }

    private void DrawRows(SpriteBatch spriteBatch, Texture2D pixel, Rectangle table)
    {
        int visible = VisibleRowCount();
        int y = table.Y + 42;
        for (int i = 0; i < visible; i++)
        {
            int index = _scrollOffset + i;
            if (index >= _entries.Count)
            {
                break;
            }

            SteamLeaderboardEntry entry = _entries[index];
            Rectangle row = new(table.X + 8, y, table.Width - 16, RowHeight - 4);
            bool isWr = index == 0 && _scope == LeaderboardScope.GlobalTop && _scrollOffset == 0;
            Color fill = GetRowFill(entry, isWr);
            spriteBatch.Draw(pixel, row, fill);
            DrawEntryRow(spriteBatch, pixel, row, entry, registerReplayHit: true, entryIndex: index);
            y += RowHeight;
        }
    }

    private void DrawStickyPb(SpriteBatch spriteBatch, Texture2D pixel, Rectangle table)
    {
        if (!ShouldShowStickyPb() || _localEntry is null)
        {
            return;
        }

        int stickyY = table.Bottom - RowHeight - 8;
        Rectangle row = new(table.X + 8, stickyY, table.Width - 16, RowHeight - 4);
        _stickyBounds = row;
        spriteBatch.Draw(pixel, row, new Color(55, 90, 70));
        DrawHelper.DrawBorder(spriteBatch, pixel, row, new Color(120, 180, 130), 1);
        DrawEntryRow(spriteBatch, pixel, row, _localEntry, registerReplayHit: false, entryIndex: -1);

        LeaderboardColumns cols = ComputeColumns(row.Width);
        bool hasReplay = SteamGhostService.ResolveGhostUgcHandle(_localEntry) != 0;
        if (hasReplay)
        {
            _stickyReplayBounds = new Rectangle(row.X + cols.ReplayX - 4, row.Y, cols.ReplayWidth + 8, row.Height);
        }
    }

    private void DrawEntryRow(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle row,
        SteamLeaderboardEntry entry,
        bool registerReplayHit,
        int entryIndex)
    {
        LeaderboardColumns cols = ComputeColumns(row.Width);
        string players = FormatPlayers(entry, cols.PlayersWidth);
        string date = entry.CompletionDateUtc == default
            ? "--"
            : entry.CompletionDateUtc.ToLocalTime().ToString("yyyy-MM-dd");
        string mode = entry.PlayerCount <= 1 ? "Solo" : "Coop";
        string time = BestTimeStorage.FormatTime(entry.TimeSeconds);
        if (entry.IsSuspicious)
        {
            time = $"!{time}";
        }

        bool hasReplay = SteamGhostService.ResolveGhostUgcHandle(entry) != 0;
        string replayLabel = _downloadingReplay ? "..." : hasReplay ? "Replay" : "--";
        Color textColor = entry.IsSuspicious ? new Color(255, 180, 120) : Color.White;
        Color replayColor = hasReplay ? new Color(140, 200, 255) : new Color(120, 130, 145);

        DrawColumns(
            spriteBatch,
            pixel,
            row,
            cols,
            $"#{entry.Rank}",
            time,
            players,
            replayLabel,
            date,
            $"v{entry.LevelVersion}",
            mode,
            textColor,
            replayColor);

        if (registerReplayHit && hasReplay && !_downloadingReplay && entryIndex >= 0)
        {
            Rectangle hit = new(row.X + cols.ReplayX - 4, row.Y, cols.ReplayWidth + 8, row.Height);
            _visibleReplayBounds.Add(hit);
            _visibleReplayIndices.Add(entryIndex);
        }
    }

    private static Color GetRowFill(SteamLeaderboardEntry entry, bool isWorldRecord)
    {
        if (entry.IsSuspicious)
        {
            return new Color(70, 45, 40);
        }

        if (entry.IsLocalUser)
        {
            return new Color(55, 90, 70);
        }

        if (isWorldRecord)
        {
            return new Color(90, 75, 40);
        }

        if (entry.IsFriend)
        {
            return new Color(45, 70, 100);
        }

        return new Color(32, 40, 54);
    }

    private static string FormatPlayers(SteamLeaderboardEntry entry, int maxWidth)
    {
        if (entry.PlayerNames.Count == 0)
        {
            return entry.PlayerCount <= 1 ? "—" : $"{entry.PlayerCount}P";
        }

        string text = entry.PlayerNames.Count == 1
            ? entry.PlayerNames[0]
            : string.Join(", ", entry.PlayerNames);

        return TruncateToWidth(text, maxWidth, scale: 2);
    }

    private static string TruncateToWidth(string text, int maxWidth, int scale)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
        {
            return text ?? string.Empty;
        }

        if (SimpleTextRenderer.MeasureString(text, scale).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        int ellipsisWidth = SimpleTextRenderer.MeasureString(ellipsis, scale).X;
        if (ellipsisWidth >= maxWidth)
        {
            return string.Empty;
        }

        for (int len = text.Length - 1; len >= 1; len--)
        {
            string candidate = text[..len] + ellipsis;
            if (SimpleTextRenderer.MeasureString(candidate, scale).X <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }

    private void DrawColumnLabels(SpriteBatch spriteBatch, Texture2D pixel, Rectangle row, Color color)
    {
        LeaderboardColumns cols = ComputeColumns(row.Width);
        DrawColumns(
            spriteBatch,
            pixel,
            row,
            cols,
            "Rank",
            "Time",
            "Players",
            "Replay",
            "Date",
            "Ver",
            "Mode",
            color,
            color);
    }

    private struct LeaderboardColumns
    {
        public int RankX;
        public int TimeX;
        public int PlayersX;
        public int ReplayX;
        public int DateX;
        public int VerX;
        public int ModeX;
        public int PlayersWidth;
        public int ReplayWidth;
    }

    /// <summary>
    /// Flex table: Rank/Time/Replay/Date/Ver/Mode keep preferred widths; Players fills leftover.
    /// </summary>
    private static LeaderboardColumns ComputeColumns(int rowWidth)
    {
        const int edgePad = 8;
        const int gap = 12;
        int inner = Math.Max(1, rowWidth - (edgePad * 2));

        int rankW = 56;
        int timeW = 148;
        int replayW = 80;
        int dateW = 120;
        int verW = 56;
        int modeW = 56;
        int fixedTotal = rankW + timeW + replayW + dateW + verW + modeW + (gap * 6);
        int playersW = inner - fixedTotal;

        const int minPlayers = 72;
        if (playersW < minPlayers)
        {
            int need = minPlayers - Math.Max(0, playersW);
            while (need > 0)
            {
                int before = need;
                if (dateW > 72)
                {
                    dateW--;
                    need--;
                }
                else if (timeW > 120)
                {
                    timeW--;
                    need--;
                }
                else if (replayW > 64)
                {
                    replayW--;
                    need--;
                }
                else if (modeW > 40)
                {
                    modeW--;
                    need--;
                }
                else if (verW > 40)
                {
                    verW--;
                    need--;
                }
                else if (rankW > 40)
                {
                    rankW--;
                    need--;
                }
                else
                {
                    break;
                }

                if (need == before)
                {
                    break;
                }
            }

            fixedTotal = rankW + timeW + replayW + dateW + verW + modeW + (gap * 6);
            playersW = Math.Max(40, inner - fixedTotal);
        }

        int x = edgePad;
        int rankX = x;
        x += rankW + gap;
        int timeX = x;
        x += timeW + gap;
        int playersX = x;
        x += playersW + gap;
        int replayX = x;
        x += replayW + gap;
        int dateX = x;
        x += dateW + gap;
        int verX = x;
        x += verW + gap;
        int modeX = x;

        return new LeaderboardColumns
        {
            RankX = rankX,
            TimeX = timeX,
            PlayersX = playersX,
            ReplayX = replayX,
            DateX = dateX,
            VerX = verX,
            ModeX = modeX,
            PlayersWidth = playersW,
            ReplayWidth = replayW
        };
    }

    private static void DrawColumns(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle row,
        LeaderboardColumns cols,
        string rank,
        string time,
        string players,
        string replay,
        string date,
        string version,
        string mode,
        Color color,
        Color replayColor)
    {
        int y = row.Y + (row.Height - 16) / 2;
        int baseX = row.X;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, rank, new Vector2(baseX + cols.RankX, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, time, new Vector2(baseX + cols.TimeX, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, players, new Vector2(baseX + cols.PlayersX, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, replay, new Vector2(baseX + cols.ReplayX, y), 2, replayColor);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, date, new Vector2(baseX + cols.DateX, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, version, new Vector2(baseX + cols.VerX, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, mode, new Vector2(baseX + cols.ModeX, y), 2, color);
    }

    private Color ScopeFill(LeaderboardScope scope) =>
        _scope == scope ? new Color(74, 120, 180) : new Color(52, 61, 80);

    private void Layout(GameTime gameTime)
    {
        Viewport viewport = _game.Viewport;
        const int buttonHeight = 44;
        const int bottomMargin = 22;
        const int scopeGap = 12;
        _backButton.Bounds = new Rectangle(25, viewport.Height - buttonHeight - bottomMargin, 120, buttonHeight);

        int selectorWidth = Math.Min(360, Math.Max(220, viewport.Width / 3));
        _playerCountSelector.Bounds = new Rectangle((viewport.Width - selectorWidth) / 2, 106, selectorWidth, 40);

        int globalW = Math.Max(120, SimpleTextRenderer.MeasureString("Global", 2).X + 28);
        int friendsW = Math.Max(120, SimpleTextRenderer.MeasureString("Friends", 2).X + 28);
        int aroundW = Math.Max(140, SimpleTextRenderer.MeasureString("Around You", 2).X + 28);
        int totalW = globalW + friendsW + aroundW + scopeGap * 2;
        int scopeX = (viewport.Width - totalW) / 2;
        int scopeY = 156;
        _globalButton.Bounds = new Rectangle(scopeX, scopeY, globalW, buttonHeight);
        _friendsButton.Bounds = new Rectangle(scopeX + globalW + scopeGap, scopeY, friendsW, buttonHeight);
        _aroundYouButton.Bounds = new Rectangle(scopeX + globalW + friendsW + scopeGap * 2, scopeY, aroundW, buttonHeight);

        _ = gameTime;
    }

    private static Rectangle GetTableBounds(Viewport viewport) =>
        new(40, 214, Math.Max(200, viewport.Width - 80), Math.Max(120, viewport.Height - 320));

    private int VisibleRowCount()
    {
        Rectangle table = GetTableBounds(_game.Viewport);
        int rows = Math.Max(1, (table.Height - 48) / RowHeight);
        // Reserve bottom row slot when sticky PB is showing.
        if (_localEntry is not null && !_loading && !_failed && !IsLocalVisibleWithoutStickyReserve(rows))
        {
            rows = Math.Max(1, rows - 1);
        }

        return rows;
    }

    /// <summary>
    /// Visibility check that does not depend on VisibleRowCount (avoids recursion when
    /// deciding whether to reserve sticky space).
    /// </summary>
    private bool IsLocalVisibleWithoutStickyReserve(int fullVisibleRows)
    {
        int index = FindLocalIndexInEntries();
        if (index < 0)
        {
            return false;
        }

        return index >= _scrollOffset && index < _scrollOffset + fullVisibleRows;
    }

    private void UpdateFocus(GameTime gameTime)
    {
        _focus.Clear();
        int players = _focus.Add(_playerCountFocus, "Players");
        int global = _focus.Add(_globalFocus, "Global");
        int friends = _focus.Add(_friendsFocus, "Friends");
        int around = _focus.Add(_aroundYouFocus, "AroundYou");
        int back = _focus.Add(_backFocus, "Back");

        NavigationGraph nav = _focus.Navigation;
        nav.LinkVertical(players, global);
        nav.LinkHorizontal(global, friends);
        nav.LinkHorizontal(friends, around);
        nav.Link(global, NavigationDirection.Down, back);
        nav.Link(friends, NavigationDirection.Down, back);
        nav.Link(around, NavigationDirection.Down, back);
        nav.Link(back, NavigationDirection.Up, global);

        _focus.FinalizeFocus("Players");
        _focus.Update(gameTime, _game.Input);
    }
}
