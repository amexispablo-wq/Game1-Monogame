#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

/// <summary>
/// Main-menu stage: local seat centered with party friends beside, arms + boombox when music volume &gt; 0.
/// </summary>
public sealed class MenuPartyDanceStage
{
    private enum MenuDanceStyle
    {
        Wave = 0,
        Floss = 1,
        Raise = 2,
        Clap = 3
    }

    private static readonly GameColor[] DisplayColors =
    {
        GameColor.Red,
        GameColor.Green,
        GameColor.Blue,
        GameColor.White
    };

    private static float s_restoreVolume = 0.75f;

    private Rectangle _bounds;
    private Rectangle _boomboxHitBounds;
    private Rectangle _prevHitBounds;
    private Rectangle _nextHitBounds;
    private float _danceTime;

    public void SetBounds(Rectangle bounds) => _bounds = bounds;

    public void Update(GameTime gameTime, ColorBlocksGame game)
    {
        float volume = SettingsManager.GetMusicVolume();
        if (volume > 0f)
        {
            s_restoreVolume = volume;
            _danceTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        game.Party.EnsureMenuDanceStyles();

        if (!game.Input.LeftMousePressed)
        {
            return;
        }

        Point pointer = game.Input.UiPointerPosition;
        if (_prevHitBounds.Width > 0 && _prevHitBounds.Contains(pointer))
        {
            GameAudio.PlayMenuPress();
            game.Music.SkipPreviousMenuTrack();
            return;
        }

        if (_nextHitBounds.Width > 0 && _nextHitBounds.Contains(pointer))
        {
            GameAudio.PlayMenuPress();
            game.Music.SkipNextMenuTrack();
            return;
        }

        if (_boomboxHitBounds.Width > 0 && _boomboxHitBounds.Contains(pointer))
        {
            ToggleMusicMute(game);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, ColorBlocksGame game)
    {
        _boomboxHitBounds = Rectangle.Empty;
        _prevHitBounds = Rectangle.Empty;
        _nextHitBounds = Rectangle.Empty;

        if (_bounds.Width <= 0 || _bounds.Height <= 0)
        {
            return;
        }

        IReadOnlyList<PartyMember> members = game.Party.Members;
        if (members.Count == 0)
        {
            return;
        }

        List<PartyMember> ordered = OrderForLocalView(members);
        bool musicOn = SettingsManager.GetMusicVolume() > 0f;
        SteamLobbyService? lobby = game.SteamLobby;

        int count = ordered.Count;
        int bodySize = Math.Clamp(Math.Min(_bounds.Width / (count + 1), _bounds.Height / 3), 48, 96);
        int gap = Math.Max(12, bodySize / 4);
        int rowWidth = (count * bodySize) + ((count - 1) * gap);
        int boomboxHeight = Math.Max(28, bodySize / 2);
        int boomboxWidth = Math.Max(48, (int)(bodySize * 1.15f));
        // Solo: body is large — fixed 12px looked glued. Multi already has visual air beside boombox.
        int bodyToBoomGap = count <= 1
            ? Math.Max(40, bodySize / 2)
            : Math.Max(12, bodySize / 5);
        int skipBtnSize = Math.Max(18, boomboxHeight / 2);
        int boomToSkipGap = Math.Max(8, boomboxHeight / 5);
        int stackHeight = bodySize + bodyToBoomGap + boomboxHeight + boomToSkipGap + skipBtnSize;
        int originX = _bounds.X + Math.Max(0, (_bounds.Width - rowWidth) / 2);
        int originY = _bounds.Y + Math.Max(0, (_bounds.Height - stackHeight) / 2);

        int boomX = _bounds.X + (_bounds.Width - boomboxWidth) / 2;
        int boomY = originY + bodySize + bodyToBoomGap;
        int antennaPad = Math.Max(10, boomboxHeight / 3) + 6;
        _boomboxHitBounds = new Rectangle(
            boomX,
            boomY - antennaPad,
            boomboxWidth,
            boomboxHeight + antennaPad);

        float boomPulse = musicOn ? (1f + (0.06f * MathF.Sin(_danceTime * 10f))) : 1f;
        float boomBob = musicOn ? (3f * MathF.Sin(_danceTime * 8f)) : 0f;
        int pulsedW = Math.Max(8, (int)MathF.Round(boomboxWidth * boomPulse));
        int pulsedH = Math.Max(8, (int)MathF.Round(boomboxHeight * boomPulse));
        int drawBoomX = _bounds.X + (_bounds.Width - pulsedW) / 2;
        int drawBoomY = boomY + (int)MathF.Round(boomBob);
        DrawBoombox(spriteBatch, pixel, new Rectangle(drawBoomX, drawBoomY, pulsedW, pulsedH), musicOn);

        int skipY = Math.Max(boomY + boomboxHeight, drawBoomY + pulsedH) + boomToSkipGap;
        int skipGap = Math.Max(10, skipBtnSize / 2);
        int skipRowW = (skipBtnSize * 2) + skipGap;
        int skipLeft = _bounds.X + (_bounds.Width - skipRowW) / 2;
        _prevHitBounds = new Rectangle(skipLeft, skipY, skipBtnSize, skipBtnSize);
        _nextHitBounds = new Rectangle(skipLeft + skipBtnSize + skipGap, skipY, skipBtnSize, skipBtnSize);

        Point pointer = game.Input.UiPointerPosition;
        bool prevHover = _prevHitBounds.Contains(pointer);
        bool nextHover = _nextHitBounds.Contains(pointer);
        DrawSkipButton(spriteBatch, pixel, _prevHitBounds, previous: true, musicOn, prevHover);
        DrawSkipButton(spriteBatch, pixel, _nextHitBounds, previous: false, musicOn, nextHover);

        for (int i = 0; i < count; i++)
        {
            PartyMember member = ordered[i];
            (PlayerSkinData? skin, _) = PlayerSkinCodec.ResolveForMember(lobby, member, members);
            GameColor gameColor = DisplayColors[i % DisplayColors.Length];
            Color bodyColor = gameColor.ToXnaColor();

            MenuDanceStyle style = MenuDanceStyle.Wave;
            if (game.Party.TryGetMenuDanceStyle(member.Id, out int styleIndex))
            {
                style = (MenuDanceStyle)Math.Clamp(styleIndex, 0, PartyManager.MenuDanceStyleCount - 1);
            }

            EvaluateDance(style, musicOn, _danceTime, i, out float leftAngle, out float rightAngle, out float bounce);

            int x = originX + (i * (bodySize + gap));
            var bodyBounds = new Rectangle(
                x,
                originY + (int)MathF.Round(bounce),
                bodySize,
                bodySize);

            DrawArms(spriteBatch, pixel, bodyBounds, bodyColor, leftAngle, rightAngle);
            PlayerSkinRenderer.DrawBody(spriteBatch, pixel, bodyBounds, bodyColor, skin);
        }
    }

    private static void ToggleMusicMute(ColorBlocksGame game)
    {
        float current = SettingsManager.GetMusicVolume();
        float next;
        if (current > 0f)
        {
            s_restoreVolume = current;
            next = 0f;
        }
        else
        {
            next = s_restoreVolume > 0f ? s_restoreVolume : 0.75f;
        }

        SettingsManager.PendingSettings.MusicVolume = next;
        SettingsManager.SaveSettings(SettingsManager.PendingSettings);
        game.Music.ApplyVolume(next);
    }

    private static void EvaluateDance(
        MenuDanceStyle style,
        bool musicOn,
        float time,
        int memberIndex,
        out float leftAngle,
        out float rightAngle,
        out float bounce)
    {
        const float NeutralLeft = MathF.PI + 0.35f;
        const float NeutralRight = -0.35f;

        if (!musicOn)
        {
            leftAngle = NeutralLeft;
            rightAngle = NeutralRight;
            bounce = 0f;
            return;
        }

        float t = time + (memberIndex * 0.15f);

        switch (style)
        {
            case MenuDanceStyle.Floss:
            {
                float wave = MathF.Sin(t * 9f) * 0.95f;
                leftAngle = NeutralLeft + wave;
                rightAngle = NeutralRight + wave;
                bounce = 2f * MathF.Sin(t * 9f + 0.5f);
                break;
            }
            case MenuDanceStyle.Raise:
            {
                float pump = 0.5f + (0.5f * MathF.Sin(t * 10f));
                leftAngle = MathF.PI + 1.35f - (pump * 0.45f);
                rightAngle = -1.35f + (pump * 0.45f);
                bounce = 4f * pump;
                break;
            }
            case MenuDanceStyle.Clap:
            {
                float open = 0.5f + (0.5f * MathF.Sin(t * 8f));
                leftAngle = MathF.PI + 0.15f + (open * 0.7f);
                rightAngle = -0.15f - (open * 0.7f);
                bounce = 1.5f * MathF.Sin(t * 8f);
                break;
            }
            case MenuDanceStyle.Wave:
            default:
            {
                float wave = MathF.Sin(t * 8.5f) * 0.85f;
                leftAngle = NeutralLeft + wave;
                rightAngle = NeutralRight - wave;
                bounce = 2.5f * MathF.Sin(t * 9f);
                break;
            }
        }
    }

    private static List<PartyMember> OrderForLocalView(IReadOnlyList<PartyMember> members)
    {
        PartyMember? center = null;
        var others = new List<PartyMember>(members.Count);

        for (int i = 0; i < members.Count; i++)
        {
            PartyMember member = members[i];
            if (center is null && member.IsLocallyOwned)
            {
                center = member;
                continue;
            }

            others.Add(member);
        }

        center ??= members[0];

        var ordered = new List<PartyMember>(members.Count) { center };
        for (int i = 0; i < others.Count; i++)
        {
            if ((i & 1) == 0)
            {
                ordered.Insert(0, others[i]);
            }
            else
            {
                ordered.Add(others[i]);
            }
        }

        return ordered;
    }

    private static void DrawArms(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bodyBounds,
        Color bodyColor,
        float leftAngle,
        float rightAngle)
    {
        int armLength = Math.Max(14, bodyBounds.Width / 2);
        int armThickness = Math.Max(5, bodyBounds.Width / 7);
        Color armColor = Color.Lerp(bodyColor, Color.Black, 0.35f);

        float shoulderY = bodyBounds.Y + (bodyBounds.Height * 0.32f);
        var leftShoulder = new Vector2(bodyBounds.Left + 2, shoulderY);
        var rightShoulder = new Vector2(bodyBounds.Right - 2, shoulderY);

        DrawArm(spriteBatch, pixel, leftShoulder, leftAngle, armLength, armThickness, armColor);
        DrawArm(spriteBatch, pixel, rightShoulder, rightAngle, armLength, armThickness, armColor);
    }

    private static void DrawArm(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Vector2 shoulder,
        float angle,
        int length,
        int thickness,
        Color color)
    {
        spriteBatch.Draw(
            pixel,
            shoulder,
            null,
            color,
            angle,
            new Vector2(0f, 0.5f),
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);

        spriteBatch.Draw(
            pixel,
            shoulder,
            null,
            Color.Black * 0.55f,
            angle,
            new Vector2(0f, 0.5f),
            new Vector2(length, Math.Max(2, thickness / 4)),
            SpriteEffects.None,
            0f);

        Vector2 tip = shoulder + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length;
        int handSize = Math.Max(6, thickness);
        var hand = new Rectangle(
            (int)MathF.Round(tip.X - (handSize * 0.5f)),
            (int)MathF.Round(tip.Y - (handSize * 0.5f)),
            handSize,
            handSize);
        spriteBatch.Draw(pixel, hand, color);
        DrawHelper.DrawBorder(spriteBatch, pixel, hand, Color.Black, 1);
    }

    private static void DrawBoombox(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, bool musicOn)
    {
        Color casing = new(48, 52, 62);
        Color accent = musicOn ? new Color(220, 90, 70) : new Color(110, 118, 132);
        Color speaker = new(28, 30, 36);
        Color cone = musicOn ? new Color(70, 160, 210) : new Color(55, 62, 74);

        int antennaX = bounds.X + (bounds.Width / 2);
        int antennaTop = bounds.Y - Math.Max(8, bounds.Height / 3);
        spriteBatch.Draw(pixel, new Rectangle(antennaX - 1, antennaTop, 2, bounds.Y - antennaTop), accent);
        spriteBatch.Draw(pixel, new Rectangle(antennaX - 3, antennaTop - 3, 6, 6), accent);

        spriteBatch.Draw(pixel, bounds, casing);
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 2);

        int handleY = bounds.Y + 3;
        int handleW = Math.Max(12, bounds.Width / 3);
        spriteBatch.Draw(
            pixel,
            new Rectangle(bounds.X + (bounds.Width - handleW) / 2, handleY, handleW, 3),
            accent);

        int pad = Math.Max(4, bounds.Width / 10);
        int speakerSize = Math.Min(bounds.Height - (pad * 2) - 4, (bounds.Width / 2) - (pad * 2));
        speakerSize = Math.Max(8, speakerSize);
        int speakerY = bounds.Y + ((bounds.Height - speakerSize) / 2) + 2;
        var leftSpeaker = new Rectangle(bounds.X + pad, speakerY, speakerSize, speakerSize);
        var rightSpeaker = new Rectangle(bounds.Right - pad - speakerSize, speakerY, speakerSize, speakerSize);
        DrawSpeaker(spriteBatch, pixel, leftSpeaker, speaker, cone, musicOn);
        DrawSpeaker(spriteBatch, pixel, rightSpeaker, speaker, cone, musicOn);

        int badgeW = Math.Max(6, bounds.Width / 6);
        int badgeH = Math.Max(6, bounds.Height / 4);
        var badge = new Rectangle(
            bounds.X + (bounds.Width - badgeW) / 2,
            bounds.Y + (bounds.Height - badgeH) / 2 + 2,
            badgeW,
            badgeH);
        spriteBatch.Draw(pixel, badge, accent);
    }

    private static void DrawSkipButton(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        bool previous,
        bool musicOn,
        bool hover)
    {
        Color casing = hover ? new Color(72, 78, 92) : new Color(48, 52, 62);
        Color accent = musicOn
            ? (hover ? new Color(255, 120, 95) : new Color(220, 90, 70))
            : (hover ? new Color(150, 158, 172) : new Color(110, 118, 132));

        spriteBatch.Draw(pixel, bounds, casing);
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 2);

        int pad = Math.Max(4, bounds.Width / 5);
        int innerW = Math.Max(4, bounds.Width - (pad * 2));
        int innerH = Math.Max(4, bounds.Height - (pad * 2));
        int cx = bounds.X + (bounds.Width / 2);
        int cy = bounds.Y + (bounds.Height / 2);
        int bar = Math.Max(2, bounds.Width / 8);

        if (previous)
        {
            // |◀
            spriteBatch.Draw(pixel, new Rectangle(bounds.X + pad, cy - (innerH / 2), bar, innerH), accent);
            DrawChevron(spriteBatch, pixel, cx + 1, cy, innerW / 2, innerH, accent, pointLeft: true);
        }
        else
        {
            // ▶|
            DrawChevron(spriteBatch, pixel, cx - 1, cy, innerW / 2, innerH, accent, pointLeft: false);
            spriteBatch.Draw(
                pixel,
                new Rectangle(bounds.Right - pad - bar, cy - (innerH / 2), bar, innerH),
                accent);
        }
    }

    private static void DrawChevron(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        int tipX,
        int tipY,
        int halfW,
        int halfH,
        Color color,
        bool pointLeft)
    {
        int steps = Math.Max(3, halfH / 2);
        for (int i = 0; i < steps; i++)
        {
            float t = steps <= 1 ? 1f : i / (float)(steps - 1);
            int rowHalf = Math.Max(1, (int)MathF.Round(halfH * 0.5f * (1f - t)));
            int x = pointLeft
                ? tipX - (int)MathF.Round(halfW * t)
                : tipX + (int)MathF.Round(halfW * t) - 1;
            spriteBatch.Draw(pixel, new Rectangle(x, tipY - rowHalf, 2, (rowHalf * 2) + 1), color);
        }
    }

    private static void DrawSpeaker(
        SpriteBatch spriteBatch,
        Texture2D pixel,
        Rectangle bounds,
        Color rim,
        Color cone,
        bool musicOn)
    {
        spriteBatch.Draw(pixel, bounds, rim);
        DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 1);

        int inset = Math.Max(2, bounds.Width / 5);
        var inner = new Rectangle(
            bounds.X + inset,
            bounds.Y + inset,
            Math.Max(2, bounds.Width - (inset * 2)),
            Math.Max(2, bounds.Height - (inset * 2)));
        spriteBatch.Draw(pixel, inner, cone);

        if (musicOn)
        {
            int dot = Math.Max(2, inner.Width / 3);
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    inner.X + (inner.Width - dot) / 2,
                    inner.Y + (inner.Height - dot) / 2,
                    dot,
                    dot),
                Color.White * 0.7f);
        }
    }
}
