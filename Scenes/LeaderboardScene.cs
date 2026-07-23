#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ColorBlocks.Replay;

namespace ColorBlocks;

/// <summary>
/// Dedicated Steam leaderboard browser for Official and Workshop levels.
/// One Steam board per level version AND player count — switching 1P/2P/3P/4P
/// loads a different board. Reuses SteamLeaderboardService.DownloadEntries.
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
    private bool _loading = true;
    private bool _failed;
    private int _scrollOffset;
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
        _game.SteamLeaderboards.DownloadEntries(_levelId, _levelVersion, playerCount, scope, MaxEntries, entries =>
        {
            // Ignore stale callbacks if the user switched scope / player count mid-flight.
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
        });
    }

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

        if (_loading)
        {
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Loading...", table, 2, new Color(180, 190, 210));
        }
        else if (_failed)
        {
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Leaderboard unavailable", table, 2, new Color(230, 120, 120));
        }
        else if (_entries.Count == 0)
        {
            SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "No scores yet", table, 2, new Color(180, 190, 210));
        }
        else
        {
            DrawRows(spriteBatch, pixel, table);
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
            Color fill = GetRowFill(entry, index == 0 && _scope == LeaderboardScope.GlobalTop && _scrollOffset == 0);
            spriteBatch.Draw(pixel, row, fill);

            string players = FormatPlayers(entry);
            string date = entry.CompletionDateUtc == default
                ? "--"
                : entry.CompletionDateUtc.ToLocalTime().ToString("yyyy-MM-dd");
            string mode = entry.PlayerCount <= 1 ? "Solo" : "Coop";
            string time = BestTimeStorage.FormatTime(entry.TimeSeconds);

            DrawColumns(
                spriteBatch,
                pixel,
                row,
                $"#{entry.Rank}",
                time,
                players,
                date,
                $"v{entry.LevelVersion}",
                mode,
                Color.White);

            y += RowHeight;
        }
    }

    private static Color GetRowFill(SteamLeaderboardEntry entry, bool isWorldRecord)
    {
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

    private static string FormatPlayers(SteamLeaderboardEntry entry)
    {
        if (entry.PlayerNames.Count == 0)
        {
            return entry.PlayerCount <= 1 ? "—" : $"{entry.PlayerCount}P";
        }

        if (entry.PlayerNames.Count == 1)
        {
            return Truncate(entry.PlayerNames[0], 18);
        }

        return Truncate(string.Join(", ", entry.PlayerNames), 22);
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
        {
            return text;
        }

        return text[..(maxChars - 1)] + "…";
    }

    private void DrawColumnLabels(SpriteBatch spriteBatch, Texture2D pixel, Rectangle row, Color color)
    {
        DrawColumns(spriteBatch, pixel, row, "Rank", "Time", "Players", "Date", "Ver", "Mode", color);
    }

    private static void DrawColumns(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle row,
        string rank,
        string time,
        string players,
        string date,
        string version,
        string mode,
        Color color)
    {
        int x = row.X + 8;
        int y = row.Y + (row.Height - 16) / 2;
        SimpleTextRenderer.DrawString(spriteBatch, pixel, rank, new Vector2(x, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, time, new Vector2(x + 70, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, players, new Vector2(x + 180, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, date, new Vector2(x + 420, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, version, new Vector2(x + 560, y), 2, color);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, mode, new Vector2(x + 620, y), 2, color);
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
        return Math.Max(1, (table.Height - 48) / RowHeight);
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
