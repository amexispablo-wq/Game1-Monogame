#nullable enable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class LevelInfoScene : IScene
{
    private readonly ColorBlocksGame _game;
    private readonly string _levelId;
    private readonly Button _backButton = new("Back") { TextScale = 2 };
    private readonly Button _applyButton = new("Apply") { TextScale = 2 };
    private readonly TextInputComponent _nameInput;
    private readonly Dropdown<string> _musicDropdown = new();
    private readonly Checkbox _allPlayersCheckbox = new() { Label = "All Players" };
    private readonly Checkbox _player1Checkbox = new() { Label = "1 Player" };
    private readonly Checkbox _player2Checkbox = new() { Label = "2 Players" };
    private readonly Checkbox _player3Checkbox = new() { Label = "3 Players" };
    private readonly Checkbox _player4Checkbox = new() { Label = "4 Players" };
    private readonly Checkbox _coloredRopeCheckbox = new() { Label = "Colored Rope" };
    private readonly Checkbox _regularRopeCheckbox = new() { Label = "Regular Rope" };
    private readonly Checkbox _lavaRiseCheckbox = new() { Label = "Lava Rise" };

    private readonly UIFocusManager _focus = new();
    private readonly UIFocusManager _promptFocus = new();
    private readonly FocusableTextInput _nameFocus;
    private readonly FocusableDropdown<string> _musicFocus;
    private readonly FocusableCheckbox _allPlayersFocus;
    private readonly FocusableCheckbox _player1Focus;
    private readonly FocusableCheckbox _player2Focus;
    private readonly FocusableCheckbox _player3Focus;
    private readonly FocusableCheckbox _player4Focus;
    private readonly FocusableCheckbox _coloredRopeFocus;
    private readonly FocusableCheckbox _regularRopeFocus;
    private readonly FocusableCheckbox _lavaRiseFocus;
    private readonly FocusableButton _backFocus;
    private readonly FocusableButton _applyFocus;

    private Level _level;
    private bool _hasUnsavedChanges;
    private bool _showUnsavedChangesPrompt;
    private Rectangle _savePromptSaveBounds;
    private Rectangle _savePromptDiscardBounds;
    private Rectangle _savePromptCancelBounds;
    private LevelInfoState _savedState;

    public LevelInfoScene(ColorBlocksGame game, string levelId)
    {
        _game = game;
        _levelId = levelId;
        _level = LevelManager.LoadLevel(levelId);
        _nameInput = new TextInputComponent(_level.Name);

        _musicDropdown.Label = string.Empty;
        _musicDropdown.Options.AddRange(LevelMusicLibrary.AvailableMusicIds);
        _musicDropdown.SelectedOption = _level.MusicId;

        _allPlayersCheckbox.IsChecked = _level.AllPlayers;
        _player1Checkbox.IsChecked = _level.Player1;
        _player2Checkbox.IsChecked = _level.Player2;
        _player3Checkbox.IsChecked = _level.Player3;
        _player4Checkbox.IsChecked = _level.Player4;

        _coloredRopeCheckbox.IsChecked = _level.ColoredRope;
        _regularRopeCheckbox.IsChecked = _level.RegularRope;
        _lavaRiseCheckbox.IsChecked = _level.LavaRise;

        _nameFocus = new FocusableTextInput(_nameInput);
        _musicFocus = new FocusableDropdown<string>(_musicDropdown);
        _allPlayersFocus = new FocusableCheckbox(_allPlayersCheckbox);
        _player1Focus = new FocusableCheckbox(_player1Checkbox);
        _player2Focus = new FocusableCheckbox(_player2Checkbox);
        _player3Focus = new FocusableCheckbox(_player3Checkbox);
        _player4Focus = new FocusableCheckbox(_player4Checkbox);
        _coloredRopeFocus = new FocusableCheckbox(_coloredRopeCheckbox);
        _regularRopeFocus = new FocusableCheckbox(_regularRopeCheckbox);
        _lavaRiseFocus = new FocusableCheckbox(_lavaRiseCheckbox);
        _backFocus = new FocusableButton(_backButton);
        _applyFocus = new FocusableButton(_applyButton);

        _focus.ResetFocus();
        _savedState = CaptureCurrentState();
        _hasUnsavedChanges = false;
    }

    public void Update(GameTime gameTime)
    {
        LayoutControls();

        if (_showUnsavedChangesPrompt)
        {
            UpdateUnsavedChangesPrompt(gameTime);
            return;
        }

        _focus.Clear();
        int nameIdx = _focus.Add(_nameFocus, "LevelName");
        int musicIdx = _focus.Add(_musicFocus, "Music");
        int allPlayersIdx = _focus.Add(_allPlayersFocus, "AllPlayers");
        int p1Idx = _focus.Add(_player1Focus, "Player1");
        int p2Idx = _focus.Add(_player2Focus, "Player2");
        int p3Idx = _focus.Add(_player3Focus, "Player3");
        int p4Idx = _focus.Add(_player4Focus, "Player4");
        int coloredIdx = _focus.Add(_coloredRopeFocus, "ColoredRope");
        int regularIdx = _focus.Add(_regularRopeFocus, "RegularRope");
        int lavaIdx = _focus.Add(_lavaRiseFocus, "LavaRise");
        int backIdx = _focus.Add(_backFocus, "Back");
        int applyIdx = _focus.Add(_applyFocus, "Apply");

        NavigationGraph nav = _focus.Navigation;
        nav.LinkVertical(nameIdx, musicIdx);
        nav.LinkVertical(musicIdx, allPlayersIdx);
        nav.LinkVertical(allPlayersIdx, p1Idx);
        nav.WireGrid(p1Idx, 4, 2);
        NavigationGraphBuilder.LinkGridBottomRowTo(nav, p1Idx, 4, 2, coloredIdx);
        nav.LinkVertical(coloredIdx, regularIdx);
        nav.LinkVertical(regularIdx, lavaIdx);
        nav.LinkVertical(lavaIdx, backIdx);
        nav.LinkHorizontal(backIdx, applyIdx);

        _focus.FinalizeFocus("LevelName");
        _focus.Update(gameTime, _game.Input);

        if (_backFocus.WasActivated || _game.Input.ExitPressed)
        {
            if (HasUnsavedChanges())
            {
                _showUnsavedChangesPrompt = true;
                return;
            }

            ReturnToLevelSelect();
            return;
        }

        if (_applyFocus.WasActivated)
        {
            ApplyChanges();
        }

        if (_nameInput.IsFocused)
        {
            _nameInput.Update(gameTime, _game.Input);
        }

        _hasUnsavedChanges = HasUnsavedChanges();
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        LayoutControls();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        Viewport viewport = _game.Viewport;
        Texture2D pixel = _game.Pixel;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(29, 34, 45));

        var titleBounds = new Rectangle(20, 20, viewport.Width - 40, 50);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "LEVEL INFORMATION", titleBounds, 3, Color.White);

        Rectangle contentArea = new(40, 90, viewport.Width - 80, viewport.Height - 140);
        spriteBatch.Draw(pixel, contentArea, new Color(22, 26, 34));
        DrawHelper.DrawBorder(spriteBatch, pixel, contentArea, new Color(95, 110, 135), 2);

        int y = contentArea.Y + 24;
        const int sectionSpacing = 16;
        const int fieldHeight = 30;

        DrawSectionLabel(spriteBatch, pixel, "Level Name", new Vector2(contentArea.X + 18, y));
        y += 32;
        _nameInput.Bounds = new Rectangle(contentArea.X + 18, y, contentArea.Width - 36, fieldHeight);
        _nameInput.Draw(spriteBatch, pixel);
        y += fieldHeight + sectionSpacing;

        DrawSectionLabel(spriteBatch, pixel, "Level Music", new Vector2(contentArea.X + 18, y));
        y += 32;
        _musicDropdown.Bounds = new Rectangle(contentArea.X + 18, y, 360, 42);
        _musicDropdown.Draw(spriteBatch, pixel);
        y += 42 + sectionSpacing;

        DrawSectionLabel(spriteBatch, pixel, "Player Compatibility", new Vector2(contentArea.X + 18, y));
        y += 32;
        _allPlayersCheckbox.Bounds = new Rectangle(contentArea.X + 18, y, 260, 30);
        _allPlayersCheckbox.Draw(spriteBatch, pixel);
        y += 34;

        int rowX = contentArea.X + 18;
        int rowWidth = (contentArea.Width - 54) / 2;
        _player1Checkbox.Bounds = new Rectangle(rowX, y, rowWidth, 30);
        _player2Checkbox.Bounds = new Rectangle(rowX + rowWidth + 18, y, rowWidth, 30);
        _player3Checkbox.Bounds = new Rectangle(rowX, y + 36, rowWidth, 30);
        _player4Checkbox.Bounds = new Rectangle(rowX + rowWidth + 18, y + 36, rowWidth, 30);
        _player1Checkbox.Draw(spriteBatch, pixel);
        _player2Checkbox.Draw(spriteBatch, pixel);
        _player3Checkbox.Draw(spriteBatch, pixel);
        _player4Checkbox.Draw(spriteBatch, pixel);
        y += 36 * 2 + sectionSpacing;

        DrawSectionLabel(spriteBatch, pixel, "Rope Modes", new Vector2(contentArea.X + 18, y));
        y += 32;
        _coloredRopeCheckbox.Bounds = new Rectangle(contentArea.X + 18, y, 260, 30);
        _regularRopeCheckbox.Bounds = new Rectangle(contentArea.X + 18, y + 36, 260, 30);
        _coloredRopeCheckbox.Draw(spriteBatch, pixel);
        _regularRopeCheckbox.Draw(spriteBatch, pixel);
        y += 36 * 2 + sectionSpacing;

        DrawSectionLabel(spriteBatch, pixel, "Features", new Vector2(contentArea.X + 18, y));
        y += 32;
        _lavaRiseCheckbox.Bounds = new Rectangle(contentArea.X + 18, y, 260, 30);
        _lavaRiseCheckbox.Draw(spriteBatch, pixel);
        y += 36 + sectionSpacing;

        int buttonWidth = 140;
        int buttonGap = 16;
        int buttonX = contentArea.X + 18;
        int buttonY = viewport.Height - 70;

        _backButton.Bounds = new Rectangle(buttonX, buttonY, buttonWidth, 44);
        _applyButton.Bounds = new Rectangle(buttonX + buttonWidth + buttonGap, buttonY, buttonWidth, 44);

        _backButton.Draw(spriteBatch, pixel);
        _applyButton.Draw(spriteBatch, pixel);
        _focus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);

        if (_showUnsavedChangesPrompt)
        {
            DrawUnsavedChangesPrompt(spriteBatch, pixel, viewport, gameTime);
        }

        spriteBatch.End();
    }

    public void OnExit()
    {
    }

    private void LayoutControls()
    {
        Viewport viewport = _game.Viewport;
        Rectangle contentArea = new(40, 90, viewport.Width - 80, viewport.Height - 140);
        int y = contentArea.Y + 24;
        const int sectionSpacing = 16;
        const int fieldHeight = 30;

        y += 32;
        _nameInput.Bounds = new Rectangle(contentArea.X + 18, y, contentArea.Width - 36, fieldHeight);
        y += fieldHeight + sectionSpacing;

        y += 32;
        _musicDropdown.Bounds = new Rectangle(contentArea.X + 18, y, 360, 42);
        y += 42 + sectionSpacing;

        y += 32;
        _allPlayersCheckbox.Bounds = new Rectangle(contentArea.X + 18, y, 260, 30);
        y += 34;

        int rowX = contentArea.X + 18;
        int rowWidth = (contentArea.Width - 54) / 2;
        _player1Checkbox.Bounds = new Rectangle(rowX, y, rowWidth, 30);
        _player2Checkbox.Bounds = new Rectangle(rowX + rowWidth + 18, y, rowWidth, 30);
        _player3Checkbox.Bounds = new Rectangle(rowX, y + 36, rowWidth, 30);
        _player4Checkbox.Bounds = new Rectangle(rowX + rowWidth + 18, y + 36, rowWidth, 30);
        y += 36 * 2 + sectionSpacing;

        y += 32;
        _coloredRopeCheckbox.Bounds = new Rectangle(contentArea.X + 18, y, 260, 30);
        _regularRopeCheckbox.Bounds = new Rectangle(contentArea.X + 18, y + 36, 260, 30);
        y += 36 * 2 + sectionSpacing;

        y += 32;
        _lavaRiseCheckbox.Bounds = new Rectangle(contentArea.X + 18, y, 260, 30);

        int buttonWidth = 140;
        int buttonGap = 16;
        int buttonX = contentArea.X + 18;
        int buttonY = viewport.Height - 70;
        _backButton.Bounds = new Rectangle(buttonX, buttonY, buttonWidth, 44);
        _applyButton.Bounds = new Rectangle(buttonX + buttonWidth + buttonGap, buttonY, buttonWidth, 44);
    }

    private void DrawSectionLabel(SpriteBatch spriteBatch, Texture2D pixel, string text, Vector2 position)
    {
        SimpleTextRenderer.DrawString(spriteBatch, pixel, text, position, 2, new Color(190, 200, 220));
    }

    private bool HasUnsavedChanges()
    {
        return !_savedState.Equals(CaptureCurrentState());
    }

    private LevelInfoState CaptureCurrentState()
    {
        return new LevelInfoState
        {
            Name = _nameInput.Text,
            MusicId = _musicDropdown.SelectedOption ?? string.Empty,
            AllPlayers = _allPlayersCheckbox.IsChecked,
            Player1 = _player1Checkbox.IsChecked,
            Player2 = _player2Checkbox.IsChecked,
            Player3 = _player3Checkbox.IsChecked,
            Player4 = _player4Checkbox.IsChecked,
            ColoredRope = _coloredRopeCheckbox.IsChecked,
            RegularRope = _regularRopeCheckbox.IsChecked,
            LavaRise = _lavaRiseCheckbox.IsChecked
        };
    }

    private void ApplyChanges()
    {
        _level.Name = _nameInput.Text;
        _level.MusicId = _musicDropdown.SelectedOption ?? string.Empty;
        _level.AllPlayers = _allPlayersCheckbox.IsChecked;
        _level.Player1 = _player1Checkbox.IsChecked;
        _level.Player2 = _player2Checkbox.IsChecked;
        _level.Player3 = _player3Checkbox.IsChecked;
        _level.Player4 = _player4Checkbox.IsChecked;
        _level.ColoredRope = _coloredRopeCheckbox.IsChecked;
        _level.RegularRope = _regularRopeCheckbox.IsChecked;
        _level.LavaRise = _lavaRiseCheckbox.IsChecked;

        SaveLevel();
        _savedState = CaptureCurrentState();
        _hasUnsavedChanges = false;
    }

    private void SaveLevel()
    {
        LevelManager.SaveLevel(_level, _levelId);
        LevelPreviewManager.GenerateAndSavePreview(_game.GraphicsDevice, _game.Pixel, _level, _levelId);
    }

    private void ReturnToLevelSelect()
    {
        _game.ChangeScene(new LevelSelectScene(_game, LevelSelectMode.EditMode));
    }

    private void UpdateUnsavedChangesPrompt(GameTime gameTime)
    {
        LayoutUnsavedPromptBounds();

        _promptFocus.Clear();
        int saveIdx = _promptFocus.Add(new FocusableAction(_savePromptSaveBounds, () =>
        {
            ApplyChanges();
            _showUnsavedChangesPrompt = false;
            ReturnToLevelSelect();
            return true;
        }), "Save");
        int discardIdx = _promptFocus.Add(new FocusableAction(_savePromptDiscardBounds, () =>
        {
            _showUnsavedChangesPrompt = false;
            ReturnToLevelSelect();
            return true;
        }), "Discard");
        int cancelIdx = _promptFocus.Add(new FocusableAction(_savePromptCancelBounds, () =>
        {
            _showUnsavedChangesPrompt = false;
            return true;
        }), "Cancel");

        NavigationGraph promptNav = _promptFocus.Navigation;
        promptNav.LinkHorizontal(saveIdx, discardIdx);
        promptNav.LinkHorizontal(discardIdx, cancelIdx);

        _promptFocus.Name = "Unsaved Changes";
        _promptFocus.FinalizeFocus("Save");
        _promptFocus.Update(gameTime, _game.Input);

        if (_game.Input.ExitPressed)
        {
            _showUnsavedChangesPrompt = false;
        }
    }

    private void LayoutUnsavedPromptBounds()
    {
        Viewport viewport = _game.Viewport;
        const int popupWidth = 460;
        const int popupHeight = 220;
        int popupX = (viewport.Width - popupWidth) / 2;
        int popupY = (viewport.Height - popupHeight) / 2;

        int buttonWidth = 120;
        int buttonHeight = 40;
        int buttonGap = 14;
        int totalWidth = buttonWidth * 3 + buttonGap * 2;
        int buttonsX = popupX + (popupWidth - totalWidth) / 2;
        int buttonsY = popupY + popupHeight - 64;

        _savePromptSaveBounds = new Rectangle(buttonsX, buttonsY, buttonWidth, buttonHeight);
        _savePromptDiscardBounds = new Rectangle(buttonsX + buttonWidth + buttonGap, buttonsY, buttonWidth, buttonHeight);
        _savePromptCancelBounds = new Rectangle(buttonsX + (buttonWidth + buttonGap) * 2, buttonsY, buttonWidth, buttonHeight);
    }

    private void DrawUnsavedChangesPrompt(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, GameTime gameTime)
    {
        LayoutUnsavedPromptBounds();
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(0, 0, 0, 180));

        const int popupWidth = 460;
        const int popupHeight = 220;
        int popupX = (viewport.Width - popupWidth) / 2;
        int popupY = (viewport.Height - popupHeight) / 2;
        Rectangle popupBounds = new(popupX, popupY, popupWidth, popupHeight);

        spriteBatch.Draw(pixel, popupBounds, new Color(35, 41, 55));
        DrawHelper.DrawBorder(spriteBatch, pixel, popupBounds, new Color(100, 110, 130), 3);

        var titleBounds = new Rectangle(popupX + 20, popupY + 20, popupWidth - 40, 34);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Save changes before leaving?", titleBounds, 2, Color.White);

        var messageBounds = new Rectangle(popupX + 20, popupY + 60, popupWidth - 40, 60);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, "Your metadata changes will be lost if you leave without saving.", messageBounds, 1, new Color(200, 200, 220));

        int buttonWidth = 120;
        int buttonHeight = 40;
        int buttonGap = 14;
        int totalWidth = buttonWidth * 3 + buttonGap * 2;
        int buttonsX = popupX + (popupWidth - totalWidth) / 2;
        int buttonsY = popupY + popupHeight - 64;

        DrawPopupButton(spriteBatch, pixel, _savePromptSaveBounds, "Save");
        DrawPopupButton(spriteBatch, pixel, _savePromptDiscardBounds, "Discard");
        DrawPopupButton(spriteBatch, pixel, _savePromptCancelBounds, "Cancel");
        _promptFocus.DrawFocusHighlights(spriteBatch, pixel, gameTime, _game.Input);
    }

    private void DrawPopupButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, string label)
    {
        spriteBatch.Draw(pixel, bounds, new Color(55, 64, 80));
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, new Color(120, 140, 170), 2);
        SimpleTextRenderer.DrawCentered(spriteBatch, pixel, label, bounds, 2, Color.White);
    }

    private struct LevelInfoState : IEquatable<LevelInfoState>
    {
        public string Name;
        public string MusicId;
        public bool AllPlayers;
        public bool Player1;
        public bool Player2;
        public bool Player3;
        public bool Player4;
        public bool ColoredRope;
        public bool RegularRope;
        public bool LavaRise;

        public bool Equals(LevelInfoState other)
        {
            return Name == other.Name
                && MusicId == other.MusicId
                && AllPlayers == other.AllPlayers
                && Player1 == other.Player1
                && Player2 == other.Player2
                && Player3 == other.Player3
                && Player4 == other.Player4
                && ColoredRope == other.ColoredRope
                && RegularRope == other.RegularRope
                && LavaRise == other.LavaRise;
        }

        public override bool Equals(object? obj) => obj is LevelInfoState other && Equals(other);
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Name);
            hash.Add(MusicId);
            hash.Add(AllPlayers);
            hash.Add(Player1);
            hash.Add(Player2);
            hash.Add(Player3);
            hash.Add(Player4);
            hash.Add(ColoredRope);
            hash.Add(RegularRope);
            hash.Add(LavaRise);
            return hash.ToHashCode();
        }
    }
}
