using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class PartyScene : IScene
{
    private readonly ColorBlocksGame _game;
    private readonly Button _playButton = new("Play");
    private readonly Button _inviteButton = new("Invite");
    private readonly Button _backButton = new("Back");
    private readonly UIFocusManager _focus = new();
    private readonly FocusableButton _playFocus;
    private readonly FocusableButton _inviteFocus;
    private readonly FocusableButton _backFocus;
    private readonly Rectangle[] _memberInputBounds = new Rectangle[PartyManager.MaxMembers];
    private readonly Rectangle[] _memberRowBounds = new Rectangle[PartyManager.MaxMembers];
    private readonly Rectangle[] _memberKickBounds = new Rectangle[PartyManager.MaxMembers];
    private readonly List<IFocusable> _memberFocusables = new();
    private AlertPopup? _alertPopup;
    private int _titleY;
    private int _titleScale = 4;
    private int _lobbyY;
    private const int LobbyTextScale = 2;
    private Rectangle _panelBounds;

    private static readonly Color Background = new(29, 34, 45);
    private static readonly Color PanelFill = new(38, 46, 62);
    private static readonly Color PanelBorder = new(118, 132, 158);
    private static readonly Color Accent = new(255, 220, 80);
    private static readonly Color LabelColor = Color.White;
    private static readonly Color MutedColor = new(167, 178, 198);
    private static readonly Color SelectorFill = new(52, 61, 80);
    private static readonly Color SelectorHover = new(74, 86, 110);

    public PartyScene(ColorBlocksGame game)
    {
        _game = game;
        _playFocus = new FocusableButton(_playButton);
        _inviteFocus = new FocusableButton(_inviteButton);
        _backFocus = new FocusableButton(_backButton);
        _focus.ResetFocus();
        _game.Party.EnsureSteamParty();
        _game.Party.ErrorOccurred += OnPartyError;
        _game.SteamLobby.ErrorOccurred += OnPartyError;
        _game.SteamLobby.LevelStartReceived += OnLevelStartReceived;
    }

    public void Update(GameTime gameTime)
    {
        Layout();
        _game.Party.ProcessGamepadJoinLeave(_game.Input);

        if (_alertPopup is not null)
        {
            _alertPopup.Update(gameTime, _game.Input, _game.Viewport.Width, _game.Viewport.Height);
            if (_alertPopup.IsDismissed)
            {
                _alertPopup = null;
            }

            return;
        }

        _memberFocusables.Clear();
        _focus.Clear();
        IReadOnlyList<PartyMember> members = _game.Party.Members;

        int? prevSelAnchor = null;
        int? prevKickAnchor = null;
        for (int slot = 0; slot < members.Count; slot++)
        {
            PartyMember member = members[slot];
            int? selIndex = null;
            int? kickIndex = null;

            if (member.IsLocallyOwned)
            {
                var selFocus = new FocusableCycleMemberInput(_memberInputBounds[slot], member, CycleMemberInput);
                _memberFocusables.Add(selFocus);
                selIndex = _focus.Add(selFocus, $"Member{slot}Input");

                if (prevSelAnchor is int prev)
                {
                    _focus.Navigation.LinkVertical(prev, selIndex.Value);
                }

                prevSelAnchor = selIndex;
            }

            if (CanKickMember(member))
            {
                int capturedSlot = slot;
                var kickFocus = new FocusableAction(
                    _memberKickBounds[slot],
                    () => _game.Party.TryKickMember(members[capturedSlot].Id));
                _memberFocusables.Add(kickFocus);
                kickIndex = _focus.Add(kickFocus, $"Member{slot}Kick");

                if (selIndex is null && prevKickAnchor is int prevKick)
                {
                    _focus.Navigation.LinkVertical(prevKick, kickIndex.Value);
                }

                prevKickAnchor = kickIndex;
            }

            if (selIndex is int s && kickIndex is int k)
            {
                _focus.Navigation.LinkHorizontal(s, k);
            }
        }

        int playIndex = _focus.Add(_playFocus, "Play");
        int inviteIndex = _focus.Add(_inviteFocus, "Invite");
        int backIndex = _focus.Add(_backFocus, "Back");

        NavigationGraph nav = _focus.Navigation;

        if (prevSelAnchor is int lastSel)
        {
            nav.LinkVertical(lastSel, playIndex);
        }
        else if (prevKickAnchor is int lastKick)
        {
            nav.LinkVertical(lastKick, playIndex);
        }

        nav.LinkHorizontal(playIndex, inviteIndex);
        nav.LinkHorizontal(inviteIndex, backIndex);

        _focus.FinalizeFocus("Play");
        _focus.Update(gameTime, _game.Input);

        if (_game.Input.ExitPressed || _game.Input.MenuCancelPressed || _backFocus.WasActivated)
        {
            LeaveAndReturnToMenu();
            return;
        }

        if (_playFocus.WasActivated)
        {
            StartPlayFlow();
            return;
        }

        if (_inviteFocus.WasActivated)
        {
            _game.SteamLobby.InviteFriends();
            return;
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        Layout();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Viewport viewport = _game.Viewport;
        Texture2D pixel = _game.Pixel;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Background);

        Point titleSize = SimpleTextRenderer.MeasureString("PARTY", _titleScale);
        DrawCenteredText(spriteBatch, pixel, "PARTY", viewport.Width / 2, _titleY, _titleScale, Accent);

        if (_game.Party.IsInSteamLobby)
        {
            string lobbyText = $"Lobby {_game.Party.LobbyId}";
            DrawCenteredText(spriteBatch, pixel, lobbyText, viewport.Width / 2, _lobbyY, LobbyTextScale, MutedColor);
        }

        Rectangle panel = _panelBounds;
        spriteBatch.Draw(pixel, panel, PanelFill);
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, PanelBorder, 2);

        int labelScale = Math.Clamp(viewport.Height / 260, 2, 3);
        int selectorScale = Math.Clamp(viewport.Height / 300, 2, 3);
        IReadOnlyList<PartyMember> members = _game.Party.Members;

        for (int slot = 0; slot < PartyManager.MaxMembers; slot++)
        {
            Rectangle row = _memberRowBounds[slot];
            string slotLabel = slot < members.Count ? GetSlotLabel(members[slot], slot) : $"Player {slot + 1}";
            SimpleTextRenderer.DrawString(spriteBatch, pixel, slotLabel, new Vector2(row.X, row.Y + 8), labelScale, LabelColor);

            if (slot < members.Count)
            {
                PartyMember member = members[slot];
                string selectorText = GetSelectorText(member);
                Rectangle selector = _memberInputBounds[slot];
                Color fill = SelectorFill;
                spriteBatch.Draw(pixel, selector, fill);
                DrawHelper.DrawBorder(spriteBatch, pixel, selector, PanelBorder, 1);
                SimpleTextRenderer.DrawCentered(spriteBatch, pixel, selectorText, selector, selectorScale, LabelColor);

                if (CanKickMember(member))
                {
                    Rectangle kick = _memberKickBounds[slot];
                    spriteBatch.Draw(pixel, kick, new Color(92, 48, 48));
                    DrawHelper.DrawBorder(spriteBatch, pixel, kick, new Color(220, 120, 120), 1);
                    SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Kick", kick, Math.Max(1, selectorScale - 1), Color.White);
                }
            }
            else
            {
                Rectangle selector = _memberInputBounds[slot];
                spriteBatch.Draw(pixel, selector, new Color(34, 40, 54));
                DrawHelper.DrawBorder(spriteBatch, pixel, selector, new Color(78, 88, 108), 1);
                SimpleTextRenderer.DrawCentered(
                    spriteBatch,
                    pixel,
                    "Press Start To Join",
                    selector,
                    Math.Max(1, selectorScale - 1),
                    MutedColor);
            }
        }

        if (_game.SteamLobby.IsInLobby && !_game.Party.IsLeader)
        {
            DrawCenteredText(
                spriteBatch,
                pixel,
                "Waiting for Party Leader...",
                viewport.Width / 2,
                _playButton.Bounds.Y - 28,
                2,
                MutedColor);
        }

        _playButton.Draw(spriteBatch, pixel);
        _inviteButton.Draw(spriteBatch, pixel);
        _backButton.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);
        _alertPopup?.Draw(spriteBatch, pixel, viewport.Width, viewport.Height, gameTime, _game.Input);

        spriteBatch.End();
    }

    public void OnExit()
    {
        _game.Party.ErrorOccurred -= OnPartyError;
        _game.SteamLobby.ErrorOccurred -= OnPartyError;
        _game.SteamLobby.LevelStartReceived -= OnLevelStartReceived;
    }

    private void OnLevelStartReceived(PartyStartMessage message)
    {
        _game.ChangeScene(new GameScene(_game, message.LevelId, message.RopeMode, message.LavaRiseEnabled));
    }

    private void OnPartyError(SteamPartyError error, string message)
    {
        _alertPopup = new AlertPopup(FormatErrorTitle(error), message);
    }

    private void LeaveAndReturnToMenu()
    {
        _game.ChangeScene(new MenuScene(_game));
    }

    private void StartPlayFlow()
    {
        if (_game.SteamLobby.IsInLobby && !_game.Party.IsLeader)
        {
            _alertPopup = new AlertPopup("Party", "Waiting for Party Leader...");
            return;
        }

        _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.PlayMode));
    }

    private void Layout()
    {
        Viewport viewport = _game.Viewport;
        int titleY = Math.Max(24, viewport.Height / 20);
        int titleScale = Math.Clamp(viewport.Height / 180, 3, 5);
        _titleY = titleY;
        _titleScale = titleScale;
        int headerBottom = titleY + SimpleTextRenderer.MeasureString("PARTY", titleScale).Y + 8;

        if (_game.Party.IsInSteamLobby)
        {
            _lobbyY = headerBottom;
            string lobbyText = $"Lobby {_game.Party.LobbyId}";
            headerBottom = _lobbyY + SimpleTextRenderer.MeasureString(lobbyText, LobbyTextScale).Y + 12;
        }

        _panelBounds = GetPanelBounds(viewport, headerBottom);
        Rectangle panel = _panelBounds;
        int rowHeight = Math.Clamp((panel.Height - 24) / PartyManager.MaxMembers, 52, 78);
        int selectorWidth = Math.Clamp(panel.Width / 3, 180, 280);
        int rowGap = 8;

        for (int slot = 0; slot < PartyManager.MaxMembers; slot++)
        {
            int y = panel.Y + 12 + slot * (rowHeight + rowGap);
            _memberRowBounds[slot] = new Rectangle(panel.X + 16, y, panel.Width - 32, rowHeight);
            int kickWidth = 72;
            bool showKick = slot < _game.Party.Members.Count && CanKickMember(_game.Party.Members[slot]);
            int selectorRight = showKick ? panel.Right - kickWidth - 24 : panel.Right - 16;
            _memberInputBounds[slot] = new Rectangle(
                selectorRight - selectorWidth,
                y + 6,
                selectorWidth,
                rowHeight - 12);
            _memberKickBounds[slot] = showKick
                ? new Rectangle(panel.Right - kickWidth - 16, y + 6, kickWidth, rowHeight - 12)
                : Rectangle.Empty;
        }

        int buttonWidth = Math.Clamp(viewport.Width / 5, 160, 220);
        int buttonHeight = 52;
        int buttonY = panel.Bottom + Math.Max(20, viewport.Height / 30);
        int gap = 16;
        int totalWidth = buttonWidth * 3 + gap * 2;
        int startX = (viewport.Width - totalWidth) / 2;
        _playButton.Bounds = new Rectangle(startX, buttonY, buttonWidth, buttonHeight);
        _inviteButton.Bounds = new Rectangle(startX + buttonWidth + gap, buttonY, buttonWidth, buttonHeight);
        _backButton.Bounds = new Rectangle(startX + (buttonWidth + gap) * 2, buttonY, buttonWidth, buttonHeight);

        bool leaderCanPlay = !_game.SteamLobby.IsInLobby || _game.Party.IsLeader;
        _playButton.FillColor = leaderCanPlay ? new Color(52, 61, 80) : new Color(40, 46, 58);
        _playButton.TextColor = leaderCanPlay ? Color.White : MutedColor;
    }

    private static bool CanKickMember(PartyMember member) => !member.IsLeader;

    private void CycleMemberInput(PartyMember member, int direction)
    {
        if (!member.IsLocallyOwned)
        {
            return;
        }

        _game.Party.TryCycleMemberInput(member.Id, direction, _game.Input.IsGamepadConnected);
    }

    private static string GetSlotLabel(PartyMember member, int slot)
    {
        if (member.IsLeader)
        {
            return $"{member.DisplayName} (Leader)";
        }

        return member.IsLocallyOwned ? $"Player {slot + 1}" : member.DisplayName;
    }

    private static string GetSelectorText(PartyMember member)
    {
        if (!member.IsLocallyOwned)
        {
            return member.DisplayName;
        }

        return $"{member.GetInputLabel()} ▼";
    }

    private static string FormatErrorTitle(SteamPartyError error)
    {
        return error switch
        {
            SteamPartyError.SteamOffline => "Steam Offline",
            SteamPartyError.LobbyFull => "Lobby Full",
            SteamPartyError.VersionMismatch => "Version Mismatch",
            SteamPartyError.JoinFailed => "Join Failed",
            SteamPartyError.LobbyClosed => "Lobby Closed",
            SteamPartyError.CreateFailed => "Lobby Error",
            _ => "Party Error"
        };
    }

    private static void DrawCenteredText(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        string text,
        int centerX,
        int y,
        int scale,
        Color color)
    {
        Point size = SimpleTextRenderer.MeasureString(text, scale);
        Vector2 position = new(centerX - (size.X * 0.5f), y);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, text, position, scale, color);
    }

    private static Rectangle GetPanelBounds(Viewport viewport, int headerBottom)
    {
        int width = Math.Clamp((int)(viewport.Width * 0.72f), 520, 860);
        int height = Math.Clamp((int)(viewport.Height * 0.52f), 300, 420);
        int x = (viewport.Width - width) / 2;
        int y = headerBottom + 8;
        int maxY = viewport.Height - height - 100;
        if (y > maxY)
        {
            y = Math.Max(headerBottom + 4, maxY);
        }

        return new Rectangle(x, y, width, height);
    }
}
