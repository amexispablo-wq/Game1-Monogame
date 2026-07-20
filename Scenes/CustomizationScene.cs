#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ColorBlocks.Replay;

namespace ColorBlocks;

public sealed class CustomizationScene : IScene
{
    private sealed class LibraryRow
    {
        public LibraryRow(string skinId, Button button, FocusableButton focus)
        {
            SkinId = skinId;
            Button = button;
            Focus = focus;
        }

        public string SkinId { get; }
        public Button Button { get; }
        public FocusableButton Focus { get; }
    }

    private readonly ColorBlocksGame _game;
    private readonly Button _applyButton = new("Apply") { TextScale = 2 };
    private readonly Button _backButton = new("Back To Menu") { TextScale = 2 };
    private readonly Button _newSkinButton = new("New Skin") { TextScale = 2 };
    private readonly Button _deleteSkinButton = new("Delete") { TextScale = 2 };
    private readonly Button _paintToolButton = new("Paint") { TextScale = 2 };
    private readonly Button _eraseToolButton = new("Erase") { TextScale = 2 };
    private readonly Button _brush1Button = new("") { TextScale = 1 };
    private readonly Button _brush2Button = new("") { TextScale = 1 };
    private readonly Button _brush3Button = new("") { TextScale = 1 };
    private readonly Button _previewRedButton = new("Red") { TextScale = 2 };
    private readonly Button _previewBlueButton = new("Blue") { TextScale = 2 };
    private readonly Button _previewGreenButton = new("Green") { TextScale = 2 };

    private readonly UIFocusManager _focus = new();
    private readonly FocusableButton _applyFocus;
    private readonly FocusableButton _backFocus;
    private readonly FocusableButton _newSkinFocus;
    private readonly FocusableButton _deleteSkinFocus;
    private readonly FocusableButton _paintToolFocus;
    private readonly FocusableButton _eraseToolFocus;
    private readonly FocusableButton _brush1Focus;
    private readonly FocusableButton _brush2Focus;
    private readonly FocusableButton _brush3Focus;
    private readonly FocusableButton _previewRedFocus;
    private readonly FocusableButton _previewBlueFocus;
    private readonly FocusableButton _previewGreenFocus;
    private readonly List<LibraryRow> _libraryRows = new();
    private readonly List<(Button Button, FocusableButton Focus, int MemberIndex)> _playerTabs = new();

    private readonly List<PartyMember> _localMembers = new();
    private readonly PlayerSkinData _workingSkin = new();
    private string? _selectedSkinId;
    private int _activeMemberIndex;
    private bool _eraseMode;
    private int _brushSize = 1;
    private GameColor _previewColor = GameColor.Red;
    private bool _isPainting;
    private bool _gamepadPaintHeld;
    private readonly VirtualCursor _virtualCursor = new();
    private Rectangle _canvasBounds;
    private Rectangle _previewBounds;
    private Rectangle _libraryPanelBounds;

    public CustomizationScene(ColorBlocksGame game)
    {
        _game = game;
        _applyFocus = new FocusableButton(_applyButton);
        _backFocus = new FocusableButton(_backButton);
        _newSkinFocus = new FocusableButton(_newSkinButton);
        _deleteSkinFocus = new FocusableButton(_deleteSkinButton);
        _paintToolFocus = new FocusableButton(_paintToolButton);
        _eraseToolFocus = new FocusableButton(_eraseToolButton);
        _brush1Focus = new FocusableButton(_brush1Button);
        _brush2Focus = new FocusableButton(_brush2Button);
        _brush3Focus = new FocusableButton(_brush3Button);
        _previewRedFocus = new FocusableButton(_previewRedButton);
        _previewBlueFocus = new FocusableButton(_previewBlueButton);
        _previewGreenFocus = new FocusableButton(_previewGreenButton);

        _applyButton.FillColor = new Color(74, 111, 93);
        _applyButton.BorderColor = new Color(154, 213, 181);
        _focus.ResetFocus();

        _game.Party.EnsureDefaultParty();
        RefreshLocalMembers();
        LoadMemberSelection();
        RebuildLibraryRows();
        RebuildPlayerTabs();
    }

    public void Update(GameTime gameTime)
    {
        LayoutUi();
        UpdateGamepadUi(gameTime);
        HandlePaintingInput();

        _focus.Clear();
        int firstLibraryIndex = -1;
        int lastLibraryIndex = -1;
        int previousLibraryIndex = -1;
        for (int i = 0; i < _libraryRows.Count; i++)
        {
            LibraryRow row = _libraryRows[i];
            int libraryIndex = _focus.Add(row.Focus, $"Skin{i}");
            if (firstLibraryIndex < 0)
            {
                firstLibraryIndex = libraryIndex;
            }

            lastLibraryIndex = libraryIndex;

            if (previousLibraryIndex >= 0)
            {
                _focus.Navigation.LinkVertical(previousLibraryIndex, libraryIndex);
            }

            previousLibraryIndex = libraryIndex;
        }

        int firstPlayerIndex = -1;
        int lastPlayerIndex = -1;
        int previousPlayerIndex = -1;
        for (int i = 0; i < _playerTabs.Count; i++)
        {
            (Button _, FocusableButton focus, int memberIndex) = _playerTabs[i];
            int playerIndex = _focus.Add(focus, $"Player{i}");
            if (firstPlayerIndex < 0)
            {
                firstPlayerIndex = playerIndex;
            }

            lastPlayerIndex = playerIndex;

            if (previousPlayerIndex >= 0)
            {
                _focus.Navigation.LinkHorizontal(previousPlayerIndex, playerIndex);
            }

            previousPlayerIndex = playerIndex;
        }

        int paintIndex = _focus.Add(_paintToolFocus, "PaintTool");
        int eraseIndex = _focus.Add(_eraseToolFocus, "EraseTool");
        int brush1Index = _focus.Add(_brush1Focus, "Brush1");
        int brush2Index = _focus.Add(_brush2Focus, "Brush2");
        int brush3Index = _focus.Add(_brush3Focus, "Brush3");
        int redIndex = _focus.Add(_previewRedFocus, "PreviewRed");
        int greenIndex = _focus.Add(_previewGreenFocus, "PreviewGreen");
        int blueIndex = _focus.Add(_previewBlueFocus, "PreviewBlue");
        int newSkinIndex = _focus.Add(_newSkinFocus, "NewSkin");
        int deleteIndex = _focus.Add(_deleteSkinFocus, "DeleteSkin");
        int applyIndex = _focus.Add(_applyFocus, "Apply");
        int backIndex = _focus.Add(_backFocus, "Back");

        WireCustomizationNavigation(
            firstLibraryIndex,
            lastLibraryIndex,
            firstPlayerIndex,
            lastPlayerIndex,
            paintIndex,
            eraseIndex,
            brush1Index,
            brush2Index,
            brush3Index,
            redIndex,
            greenIndex,
            blueIndex,
            newSkinIndex,
            deleteIndex,
            applyIndex,
            backIndex);

        _focus.FinalizeFocus(firstLibraryIndex >= 0 ? "Skin0" : "PaintTool");
        _focus.Update(gameTime, _game.Input);

        bool suppress = ShouldSuppressGamepadUiActivation();
        if (!suppress)
        {
            for (int i = 0; i < _libraryRows.Count; i++)
            {
                LibraryRow row = _libraryRows[i];
                if (row.Focus.WasActivated)
                {
                    SelectSkin(row.SkinId);
                    break;
                }
            }

            for (int i = 0; i < _playerTabs.Count; i++)
            {
                (_, FocusableButton focus, int memberIndex) = _playerTabs[i];
                if (focus.WasActivated)
                {
                    SwitchMember(memberIndex);
                    break;
                }
            }
        }

        if (!suppress && _paintToolFocus.WasActivated) _eraseMode = false;
        else if (!suppress && _eraseToolFocus.WasActivated) _eraseMode = true;
        else if (!suppress && _brush1Focus.WasActivated) _brushSize = 1;
        else if (!suppress && _brush2Focus.WasActivated) _brushSize = 2;
        else if (!suppress && _brush3Focus.WasActivated) _brushSize = 3;
        else if (!suppress && _previewRedFocus.WasActivated) _previewColor = GameColor.Red;
        else if (!suppress && _previewGreenFocus.WasActivated) _previewColor = GameColor.Green;
        else if (!suppress && _previewBlueFocus.WasActivated) _previewColor = GameColor.Blue;
        else if (!suppress && _newSkinFocus.WasActivated) CreateNewSkin();
        else if (!suppress && _deleteSkinFocus.WasActivated) DeleteSelectedSkin();
        else if (!suppress && _applyFocus.WasActivated)
        {
            ApplyChanges();
        }
        else if (_backFocus.WasActivated || _game.Input.ExitPressed || _game.Input.MenuCancelPressed)
        {
            _game.ChangeScene(new MenuScene(_game));
        }
    }

    private void WireCustomizationNavigation(
        int firstLibraryIndex,
        int lastLibraryIndex,
        int firstPlayerIndex,
        int lastPlayerIndex,
        int paintIndex,
        int eraseIndex,
        int brush1Index,
        int brush2Index,
        int brush3Index,
        int redIndex,
        int greenIndex,
        int blueIndex,
        int newSkinIndex,
        int deleteIndex,
        int applyIndex,
        int backIndex)
    {
        NavigationGraph nav = _focus.Navigation;

        if (firstPlayerIndex >= 0 && lastPlayerIndex > firstPlayerIndex)
        {
            nav.LinkHorizontal(firstPlayerIndex, lastPlayerIndex);
        }

        if (firstPlayerIndex >= 0)
        {
            nav.LinkHorizontal(eraseIndex, firstPlayerIndex);
        }

        nav.LinkHorizontal(paintIndex, eraseIndex);
        nav.LinkHorizontal(brush1Index, brush2Index);
        nav.LinkHorizontal(brush2Index, brush3Index);
        nav.LinkHorizontal(redIndex, greenIndex);
        nav.LinkHorizontal(greenIndex, blueIndex);
        nav.LinkHorizontal(deleteIndex, applyIndex);
        nav.LinkHorizontal(applyIndex, backIndex);

        if (lastLibraryIndex >= 0)
        {
            nav.LinkVertical(lastLibraryIndex, newSkinIndex);
        }

        nav.LinkVertical(newSkinIndex, deleteIndex);

        if (firstLibraryIndex >= 0)
        {
            nav.LinkHorizontal(firstLibraryIndex, paintIndex);
        }

        nav.LinkVertical(paintIndex, brush1Index);
        nav.LinkVertical(eraseIndex, brush3Index);
        nav.LinkVertical(brush1Index, redIndex);
        nav.LinkVertical(brush2Index, greenIndex);
        nav.LinkVertical(brush3Index, blueIndex);
        nav.LinkVertical(redIndex, applyIndex);
        nav.LinkVertical(greenIndex, applyIndex);
        nav.LinkVertical(blueIndex, applyIndex);
    }

    public void OnExit()
    {
        _virtualCursor.Reset();
        _game.Input.SetUiPointerOverride(null);
        _game.Input.Navigation.PreferMouse();
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutUi();

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

        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "CUSTOMIZATION", new Rectangle(0, 24, viewport.Width, 40), 3, Color.White);

        foreach ((Button button, _, _) in _playerTabs)
        {
            button.Draw(spriteBatch, pixel);
        }

        spriteBatch.Draw(pixel, _libraryPanelBounds, new Color(25, 30, 40, 240));
        DrawHelper.DrawBorder(spriteBatch, pixel, _libraryPanelBounds, new Color(95, 110, 135), 2);
        SimpleTextRenderer.DrawString(spriteBatch, pixel, "LIBRARY", new Vector2(_libraryPanelBounds.X + 12, _libraryPanelBounds.Y + 10), 2, new Color(255, 226, 122));

        foreach (LibraryRow row in _libraryRows)
        {
            row.Button.Draw(spriteBatch, pixel);
        }

        _newSkinButton.Draw(spriteBatch, pixel);
        _deleteSkinButton.Draw(spriteBatch, pixel);

        spriteBatch.Draw(pixel, _previewBounds, new Color(20, 26, 36));
        DrawHelper.DrawBorder(spriteBatch, pixel, _previewBounds, new Color(85, 100, 120), 2);
        Rectangle previewBody = new(
            _previewBounds.X + (_previewBounds.Width - 96) / 2,
            _previewBounds.Y + (_previewBounds.Height - 96) / 2,
            96,
            96);
        PlayerSkinRenderer.DrawBody(spriteBatch, pixel, previewBody, _previewColor.ToXnaColor(), _workingSkin);

        spriteBatch.Draw(pixel, _canvasBounds, new Color(18, 22, 30));
        DrawHelper.DrawBorder(spriteBatch, pixel, _canvasBounds, new Color(95, 110, 135), 2);
        DrawCanvas(spriteBatch, pixel);

        foreach ((Button button, _, _) in _playerTabs)
        {
            button.Draw(spriteBatch, pixel);
        }

        _paintToolButton.FillColor = !_eraseMode ? new Color(82, 94, 118) : new Color(48, 57, 74);
        _eraseToolButton.FillColor = _eraseMode ? new Color(82, 94, 118) : new Color(48, 57, 74);
        _brush1Button.FillColor = _brushSize == 1 ? new Color(82, 94, 118) : new Color(48, 57, 74);
        _brush2Button.FillColor = _brushSize == 2 ? new Color(82, 94, 118) : new Color(48, 57, 74);
        _brush3Button.FillColor = _brushSize == 3 ? new Color(82, 94, 118) : new Color(48, 57, 74);

        _paintToolButton.Draw(spriteBatch, pixel);
        _eraseToolButton.Draw(spriteBatch, pixel);
        DrawBrushSizeButton(spriteBatch, pixel, _brush1Button, 6);
        DrawBrushSizeButton(spriteBatch, pixel, _brush2Button, 10);
        DrawBrushSizeButton(spriteBatch, pixel, _brush3Button, 14);
        _previewRedButton.Draw(spriteBatch, pixel);
        _previewGreenButton.Draw(spriteBatch, pixel);
        _previewBlueButton.Draw(spriteBatch, pixel);

        _applyButton.Draw(spriteBatch, pixel);
        _backButton.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);

        if (_virtualCursor.IsActive)
        {
            Point pointer = _game.Input.UiPointerPosition;
            Rectangle cursor = new(pointer.X - 6, pointer.Y - 6, 12, 12);
            spriteBatch.Draw(pixel, cursor, new Color(255, 220, 80));
            DrawHelper.DrawBorder(spriteBatch, pixel, cursor, Color.White, 1);
        }

        if (_game.Input.Navigation.IsGamepadActive)
        {
            DrawGamepadPaintHint(spriteBatch, pixel);
        }

        spriteBatch.End();
    }

    private void RebuildLibraryRows()
    {
        _libraryRows.Clear();
        int maxVisible = Math.Min(8, SkinLibraryStorage.Skins.Count);
        for (int i = 0; i < maxVisible; i++)
        {
            PlayerSkinEntry entry = SkinLibraryStorage.Skins[i];
            var button = new Button(TrimSkinName(entry.Name)) { TextScale = 2 };
            if (entry.Id == _selectedSkinId)
            {
                button.FillColor = new Color(82, 94, 118);
                button.BorderColor = new Color(255, 226, 122);
            }

            _libraryRows.Add(new LibraryRow(entry.Id, button, new FocusableButton(button)));
        }
    }

    private void RebuildPlayerTabs()
    {
        _playerTabs.Clear();
        if (_localMembers.Count <= 1)
        {
            return;
        }

        for (int i = 0; i < _localMembers.Count; i++)
        {
            int captured = i;
            PartyMember member = _localMembers[captured];
            var button = new Button(member.DisplayName) { TextScale = 2 };
            if (captured == _activeMemberIndex)
            {
                button.FillColor = new Color(82, 94, 118);
                button.BorderColor = new Color(255, 226, 122);
            }

            _playerTabs.Add((button, new FocusableButton(button), captured));
        }
    }

    private void RefreshLocalMembers()
    {
        _localMembers.Clear();
        foreach (PartyMember member in _game.Party.Members)
        {
            if (member.IsLocallyOwned)
            {
                _localMembers.Add(member);
            }
        }

        if (_localMembers.Count == 0)
        {
            _game.Party.EnsureDefaultParty();
            foreach (PartyMember member in _game.Party.Members)
            {
                if (member.IsLocallyOwned)
                {
                    _localMembers.Add(member);
                }
            }
        }

        _activeMemberIndex = Math.Clamp(_activeMemberIndex, 0, Math.Max(0, _localMembers.Count - 1));
    }

    private void LoadMemberSelection()
    {
        if (_localMembers.Count == 0)
        {
            return;
        }

        PartyMember member = _localMembers[_activeMemberIndex];
        _selectedSkinId = SkinLibraryStorage.GetSelectedSkinId(member.Id);
        PlayerSkinEntry? entry = string.IsNullOrEmpty(_selectedSkinId) ? null : SkinLibraryStorage.FindSkin(_selectedSkinId);
        _workingSkin.CopyFrom(entry is not null ? entry.ToSkinData() : new PlayerSkinData());
        RebuildLibraryRows();
        RebuildPlayerTabs();
    }

    private void SelectSkin(string skinId)
    {
        _selectedSkinId = skinId;
        PlayerSkinEntry? entry = SkinLibraryStorage.FindSkin(skinId);
        if (entry is not null)
        {
            _workingSkin.CopyFrom(entry.ToSkinData());
        }

        RebuildLibraryRows();
    }

    private void SwitchMember(int index)
    {
        if (index == _activeMemberIndex)
        {
            return;
        }

        // Persist current member skin before switching so both locals can be set in one visit.
        ApplyChanges();
        _activeMemberIndex = index;
        LoadMemberSelection();
    }

    private void CreateNewSkin()
    {
        string name = $"Skin {SkinLibraryStorage.Skins.Count + 1}";
        PlayerSkinEntry entry = SkinLibraryStorage.AddSkin(name, _workingSkin.Clone());
        SelectSkin(entry.Id);
    }

    private void DeleteSelectedSkin()
    {
        if (string.IsNullOrEmpty(_selectedSkinId) || SkinLibraryStorage.Skins.Count <= 1)
        {
            return;
        }

        SkinLibraryStorage.DeleteSkin(_selectedSkinId);
        _selectedSkinId = SkinLibraryStorage.Skins[0].Id;
        SelectSkin(_selectedSkinId);
    }

    private void ApplyChanges()
    {
        if (_localMembers.Count == 0 || string.IsNullOrEmpty(_selectedSkinId))
        {
            return;
        }

        PlayerSkinEntry? entry = SkinLibraryStorage.FindSkin(_selectedSkinId);
        if (entry is not null)
        {
            SkinLibraryStorage.UpdateSkinPixels(_selectedSkinId, _workingSkin);
        }

        PartyMember member = _localMembers[_activeMemberIndex];
        SkinLibraryStorage.SetSelectedSkinId(member.Id, _selectedSkinId);
    }

    private void HandlePaintingInput()
    {
        InputManager input = _game.Input;
        Point pointer = input.UiPointerPosition;

        if (IsGamepadCursorMode())
        {
            bool triggerHeld = input.EditorRightTrigger > PaintTriggerThreshold;
            if (triggerHeld && _canvasBounds.Contains(pointer))
            {
                PaintAtPointer(pointer);
            }

            _gamepadPaintHeld = triggerHeld;
            return;
        }

        _gamepadPaintHeld = false;

        if (input.UiPointerPressed && _canvasBounds.Contains(pointer))
        {
            _isPainting = true;
            PaintAtPointer(pointer);
        }
        else if (input.UiPointerHeld && _isPainting)
        {
            PaintAtPointer(pointer);
        }
        else if (input.UiPointerReleased)
        {
            _isPainting = false;
        }
    }

    private const float PaintTriggerThreshold = 0.35f;

    private bool IsGamepadCursorMode() =>
        _virtualCursor.IsActive && _game.Input.Navigation.IsGamepadActive;

    private bool ShouldSuppressGamepadUiActivation() =>
        IsGamepadCursorMode() && _gamepadPaintHeld;

    private void UpdateGamepadUi(GameTime gameTime)
    {
        _virtualCursor.BeginFrame(_game.Viewport, _game.Input);
        _virtualCursor.Update(gameTime, _game.Input, _game.Viewport);
        _game.Input.SetUiPointerOverride(_virtualCursor.IsActive ? _virtualCursor.Position : null);
    }

    private void DrawGamepadPaintHint(SpriteBatch spriteBatch, Texture2D pixel)
    {
        const string hint = "HOLD RT / R2 TO PAINT";
        int textScale = 2;
        Point textSize = SimpleTextRenderer.MeasureString(hint, textScale);
        int paddingX = 14;
        int paddingY = 8;
        int panelWidth = textSize.X + (paddingX * 2);
        int panelHeight = textSize.Y + (paddingY * 2);
        int panelX = _canvasBounds.X + ((_canvasBounds.Width - panelWidth) / 2);
        int panelY = _canvasBounds.Bottom + 10;
        var panel = new Rectangle(panelX, panelY, panelWidth, panelHeight);
        spriteBatch.Draw(pixel, panel, new Color(18, 24, 34, 230));
        DrawHelper.DrawBorder(spriteBatch, pixel, panel, new Color(255, 226, 122), 2);
        SimpleTextRenderer.DrawString(
            spriteBatch,
            pixel,
            hint,
            new Vector2(panelX + paddingX, panelY + paddingY),
            textScale,
            new Color(255, 226, 122));
    }

    private void PaintAtPointer(Point pointer)
    {
        if (!_canvasBounds.Contains(pointer))
        {
            return;
        }

        int grid = PlayerSkinData.GridSize;
        int cell = Math.Max(8, _canvasBounds.Width / grid);
        int offsetX = _canvasBounds.X + ((_canvasBounds.Width - (cell * grid)) / 2);
        int offsetY = _canvasBounds.Y + ((_canvasBounds.Height - (cell * grid)) / 2);
        int localX = pointer.X - offsetX;
        int localY = pointer.Y - offsetY;
        int centerX = localX / cell;
        int centerY = localY / cell;
        int radius = _brushSize - 1;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = centerX + dx;
                int y = centerY + dy;
                if (x < 0 || y < 0 || x >= grid || y >= grid)
                {
                    continue;
                }

                _workingSkin.SetPixel(x, y, !_eraseMode);
            }
        }
    }

    private void DrawCanvas(SpriteBatch spriteBatch, Texture2D pixel)
    {
        int grid = PlayerSkinData.GridSize;
        int cell = Math.Max(8, _canvasBounds.Width / grid);
        int offsetX = _canvasBounds.X + ((_canvasBounds.Width - (cell * grid)) / 2);
        int offsetY = _canvasBounds.Y + ((_canvasBounds.Height - (cell * grid)) / 2);

        for (int y = 0; y < grid; y++)
        {
            for (int x = 0; x < grid; x++)
            {
                Rectangle cellBounds = new(offsetX + (x * cell), offsetY + (y * cell), cell, cell);
                Color fill = _workingSkin.GetPixel(x, y) ? Color.Black : new Color(52, 61, 80);
                spriteBatch.Draw(pixel, cellBounds, fill);
            }
        }
    }

    private void LayoutUi()
    {
        Viewport viewport = _game.Viewport;
        int margin = 24;
        int libraryWidth = Math.Clamp(220, 180, viewport.Width / 5);
        _libraryPanelBounds = new Rectangle(margin, 90, libraryWidth, viewport.Height - 180);

        for (int i = 0; i < _libraryRows.Count; i++)
        {
            _libraryRows[i].Button.Bounds = GetLibraryItemBounds(i);
        }

        int buttonY = _libraryPanelBounds.Bottom - 96;
        _newSkinButton.Bounds = new Rectangle(_libraryPanelBounds.X + 10, buttonY, libraryWidth - 20, 40);
        _deleteSkinButton.Bounds = new Rectangle(_libraryPanelBounds.X + 10, buttonY + 48, libraryWidth - 20, 40);

        int centerX = _libraryPanelBounds.Right + 24;
        int centerWidth = viewport.Width - centerX - margin;
        _previewBounds = new Rectangle(centerX, 90, Math.Min(220, centerWidth / 3), 180);

        int toolsX = _previewBounds.Right + 16;
        const int colorButtonWidth = 72;
        const int colorButtonHeight = 36;
        const int colorGap = 8;
        _paintToolButton.Bounds = new Rectangle(toolsX, 96, 90, 40);
        _eraseToolButton.Bounds = new Rectangle(toolsX + 98, 96, 90, 40);
        _brush1Button.Bounds = new Rectangle(toolsX, 146, 48, 40);
        _brush2Button.Bounds = new Rectangle(toolsX + 56, 146, 48, 40);
        _brush3Button.Bounds = new Rectangle(toolsX + 112, 146, 48, 40);

        _previewRedButton.Bounds = new Rectangle(toolsX, 196, colorButtonWidth, colorButtonHeight);
        _previewGreenButton.Bounds = new Rectangle(toolsX + colorButtonWidth + colorGap, 196, colorButtonWidth, colorButtonHeight);
        _previewBlueButton.Bounds = new Rectangle(toolsX + ((colorButtonWidth + colorGap) * 2), 196, colorButtonWidth, colorButtonHeight);
        _previewRedButton.TextScale = 2;
        _previewBlueButton.TextScale = 2;
        _previewGreenButton.TextScale = 2;

        int canvasSize = Math.Clamp(Math.Min(centerWidth, viewport.Height - 320), 180, 320);
        _canvasBounds = new Rectangle(centerX + ((centerWidth - canvasSize) / 2), 280, canvasSize, canvasSize);

        for (int i = 0; i < _playerTabs.Count; i++)
        {
            _playerTabs[i].Button.Bounds = GetPlayerTabBounds(i, viewport);
        }

        int bottomY = viewport.Height - 72;
        _applyButton.Bounds = new Rectangle(viewport.Width / 2 - 220, bottomY, 200, 48);
        _backButton.Bounds = new Rectangle(viewport.Width / 2 + 20, bottomY, 200, 48);
    }

    private Rectangle GetLibraryItemBounds(int visibleIndex)
    {
        int y = _libraryPanelBounds.Y + 48 + (visibleIndex * 44);
        return new Rectangle(_libraryPanelBounds.X + 10, y, _libraryPanelBounds.Width - 20, 38);
    }

    private Rectangle GetPlayerTabBounds(int index, Viewport viewport)
    {
        int tabWidth = 140;
        int gap = 12;
        int totalWidth = (_playerTabs.Count * tabWidth) + ((_playerTabs.Count - 1) * gap);
        int startX = (viewport.Width - totalWidth) / 2;
        const int tabY = 68;
        return new Rectangle(startX + (index * (tabWidth + gap)), tabY, tabWidth, 36);
    }

    private static void DrawBrushSizeButton(SpriteBatch spriteBatch, Texture2D pixel, Button button, int squareSize)
    {
        Rectangle bounds = button.Bounds;
        bool hovered = button.IsHovered;
        Color fill = button.FillColor;
        Color border = hovered ? button.HoverBorderColor : button.BorderColor;

        spriteBatch.Draw(pixel, new Rectangle(bounds.X + 3, bounds.Y + 4, bounds.Width, bounds.Height), new Color(5, 7, 12, 95));
        spriteBatch.Draw(pixel, bounds, fill);
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, border, hovered ? 3 : 2);

        int size = Math.Clamp(squareSize, 4, bounds.Height - 12);
        var square = new Rectangle(bounds.Center.X - (size / 2), bounds.Center.Y - (size / 2), size, size);
        spriteBatch.Draw(pixel, square, Color.Black);
    }

    private static string TrimSkinName(string name) => name.Length > 14 ? name[..14] : name;
}
