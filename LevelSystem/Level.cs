using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public sealed class Level
{
    private readonly List<Platform> _platforms = new();
    private readonly List<Goal> _goals = new();
    private readonly List<CheckpointFlag> _checkpointFlags = new();
    private readonly List<LaunchPad> _launchPads = new();

    public Level()
    {
        PlayerStart = new Vector2(100f, 300f);
        WorldSize = new Point(1280, 720);
        Name = string.Empty;
    }

    public Level(
        Vector2 playerStart,
        IEnumerable<Platform> platforms,
        IEnumerable<Goal> goals,
        IEnumerable<CheckpointFlag> checkpointFlags = null,
        IEnumerable<LaunchPad> launchPads = null,
        string name = "")
    {
        PlayerStart = playerStart;
        Name = name;
        _platforms.AddRange(platforms);
        _goals.AddRange(goals);
        if (checkpointFlags is not null)
        {
            _checkpointFlags.AddRange(checkpointFlags);
            EnsureCheckpointIds();
        }

        if (launchPads is not null)
        {
            _launchPads.AddRange(launchPads);
        }

        RecalculateWorldSize();
    }

    public string Name { get; set; }
    public Vector2 PlayerStart { get; set; }
    public Point WorldSize { get; private set; }
    public string MusicId { get; set; } = LevelMusicLibrary.DefaultMusicId;
    public bool AllPlayers { get; set; } = true;
    public bool Player1 { get; set; }
    public bool Player2 { get; set; }
    public bool Player3 { get; set; }
    public bool Player4 { get; set; }
    public bool ColoredRope { get; set; }
    public bool RegularRope { get; set; }
    public bool LavaRise { get; set; }
    public LavaLine Lava { get; set; }
    public IReadOnlyList<Platform> Platforms => _platforms;
    public IReadOnlyList<Goal> Goals => _goals;
    public IReadOnlyList<CheckpointFlag> CheckpointFlags => _checkpointFlags;
    public IReadOnlyList<LaunchPad> LaunchPads => _launchPads;

    public static Level CreateDefault()
    {
        LevelData data = new()
        {
            PlayerSpawn = new Vector2Data { X = 100f, Y = 300f }
        };

        data.Platforms.Add(new PlatformData { X = 0, Y = 400, Width = 800, Height = 40, Color = GameColor.Red });
        data.Platforms.Add(new PlatformData { X = 260, Y = 330, Width = 160, Height = 28, Color = GameColor.Blue });
        data.Platforms.Add(new PlatformData { X = 520, Y = 270, Width = 160, Height = 28, Color = GameColor.Green });
        data.Platforms.Add(new PlatformData { X = 760, Y = 500, Width = 260, Height = 40, Color = GameColor.Blue });
        data.Platforms.Add(new PlatformData { X = 1080, Y = 420, Width = 220, Height = 32, Color = GameColor.Green });
        data.Goals.Add(new GoalData { X = 1216, Y = 356 });

        return FromData(data);
    }

    public static Level CreateRopeSandbox()
    {
        return new Level(
            new Vector2(260f, 320f),
            new[]
            {
                new Platform(new Rectangle(0, 420, 1800, 48), GameColor.Red),
                new Platform(new Rectangle(120, 340, 520, 28), GameColor.Red),
                new Platform(new Rectangle(760, 340, 520, 28), GameColor.Red)
            },
            Array.Empty<Goal>(),
            name: "Rope Sandbox")
        {
            AllPlayers = true,
            RegularRope = true
        };
    }

    public static Level FromData(LevelData data)
    {
        List<Platform> platforms = new();
        List<Goal> goals = new();
        List<CheckpointFlag> checkpointFlags = new();
        List<LaunchPad> launchPads = new();

        foreach (PlatformData platform in data.Platforms)
        {
            if (platform.Width <= 0 || platform.Height <= 0)
            {
                continue;
            }

            platforms.Add(new Platform(
                new Rectangle(platform.X, platform.Y, platform.Width, platform.Height),
                platform.Color));
        }

        foreach (GoalData goal in data.Goals)
        {
            goals.Add(new Goal(new Point(goal.X, goal.Y)));
        }

        foreach (CheckpointFlagData checkpoint in data.CheckpointFlags)
        {
            checkpointFlags.Add(new CheckpointFlag(new Point(checkpoint.X, checkpoint.Y), checkpoint.Id));
        }

        foreach (LaunchPadData launchPad in data.LaunchPads)
        {
            if (launchPad.Width <= 0 || launchPad.Height <= 0)
            {
                continue;
            }

            launchPads.Add(new LaunchPad(
                new Rectangle(launchPad.X, launchPad.Y, launchPad.Width, launchPad.Height),
                launchPad.RotationDegrees));
        }

        Level level = new Level(
            new Vector2(data.PlayerSpawn.X, data.PlayerSpawn.Y),
            platforms,
            goals,
            checkpointFlags,
            launchPads,
            data.Name)
        {
            MusicId = string.IsNullOrWhiteSpace(data.MusicId) ? LevelMusicLibrary.DefaultMusicId : data.MusicId,
            AllPlayers = data.AllPlayers,
            Player1 = data.Player1,
            Player2 = data.Player2,
            Player3 = data.Player3,
            Player4 = data.Player4,
            ColoredRope = data.ColoredRope,
            RegularRope = data.RegularRope,
            LavaRise = data.LavaRise
        };

        if (data.LavaLine is not null)
        {
            level.Lava = new LavaLine(data.LavaLine.SurfaceY, data.LavaLine.RiseSpeed);
        }

        return level;
    }

    public LevelData ToData()
    {
        LevelData data = new()
        {
            Name = Name,
            PlayerSpawn = new Vector2Data { X = PlayerStart.X, Y = PlayerStart.Y },
            MusicId = MusicId,
            AllPlayers = AllPlayers,
            Player1 = Player1,
            Player2 = Player2,
            Player3 = Player3,
            Player4 = Player4,
            ColoredRope = ColoredRope,
            RegularRope = RegularRope,
            LavaRise = LavaRise,
            LavaLine = Lava is null ? null : new LavaLineData { SurfaceY = Lava.SurfaceY, RiseSpeed = Lava.RiseSpeed }
        };

        foreach (Platform platform in _platforms)
        {
            data.Platforms.Add(new PlatformData
            {
                X = platform.Bounds.X,
                Y = platform.Bounds.Y,
                Width = platform.Bounds.Width,
                Height = platform.Bounds.Height,
                Color = platform.PlatformColor
            });
        }

        foreach (Goal goal in _goals)
        {
            data.Goals.Add(new GoalData
            {
                X = goal.Position.X,
                Y = goal.Position.Y
            });
        }

        foreach (CheckpointFlag checkpoint in _checkpointFlags)
        {
            data.CheckpointFlags.Add(new CheckpointFlagData
            {
                Id = checkpoint.Id,
                X = checkpoint.Position.X,
                Y = checkpoint.Position.Y
            });
        }

        foreach (LaunchPad launchPad in _launchPads)
        {
            data.LaunchPads.Add(new LaunchPadData
            {
                X = launchPad.Bounds.X,
                Y = launchPad.Bounds.Y,
                Width = launchPad.Bounds.Width,
                Height = launchPad.Bounds.Height,
                RotationDegrees = LaunchPad.NormalizeRotation(launchPad.RotationDegrees)
            });
        }

        return data;
    }

    public void AddPlatform(Platform platform)
    {
        _platforms.Add(platform);
        RecalculateWorldSize();
    }

    public void RemovePlatform(Platform platform)
    {
        _platforms.Remove(platform);
        RecalculateWorldSize();
    }

    public void AddGoal(Goal goal)
    {
        _goals.Add(goal);
        RecalculateWorldSize();
    }

    public void RemoveGoal(Goal goal)
    {
        _goals.Remove(goal);
        RecalculateWorldSize();
    }

    public void AddCheckpointFlag(CheckpointFlag checkpoint)
    {
        if (checkpoint.Id <= 0 || _checkpointFlags.Exists(existing => existing.Id == checkpoint.Id))
        {
            checkpoint.Id = GetNextCheckpointId();
        }

        _checkpointFlags.Add(checkpoint);
        RecalculateWorldSize();
    }

    public void RemoveCheckpointFlag(CheckpointFlag checkpoint)
    {
        _checkpointFlags.Remove(checkpoint);
        RecalculateWorldSize();
    }

    public void AddLaunchPad(LaunchPad launchPad)
    {
        _launchPads.Add(launchPad);
        RecalculateWorldSize();
    }

    public void RemoveLaunchPad(LaunchPad launchPad)
    {
        _launchPads.Remove(launchPad);
        RecalculateWorldSize();
    }

    public void RecalculateWorldSize()
    {
        int width = 1280;
        int height = 720;

        foreach (Platform platform in _platforms)
        {
            width = System.Math.Max(width, platform.Bounds.Right + 200);
            height = System.Math.Max(height, platform.Bounds.Bottom + 200);
        }

        foreach (Goal goal in _goals)
        {
            width = System.Math.Max(width, goal.Bounds.Right + 200);
            height = System.Math.Max(height, goal.Bounds.Bottom + 200);
        }

        foreach (CheckpointFlag checkpoint in _checkpointFlags)
        {
            width = System.Math.Max(width, checkpoint.Bounds.Right + 200);
            height = System.Math.Max(height, checkpoint.Bounds.Bottom + 200);
        }

        foreach (LaunchPad launchPad in _launchPads)
        {
            width = System.Math.Max(width, launchPad.Bounds.Right + 200);
            height = System.Math.Max(height, launchPad.Bounds.Bottom + 200);
        }

        WorldSize = new Point(width, height);
    }

    /// <summary>Creates a lava line below the level content if one does not exist yet (editor use).</summary>
    public void EnsureLava()
    {
        if (Lava is not null)
        {
            return;
        }

        int maxBottom = int.MinValue;
        foreach (Platform platform in _platforms) maxBottom = System.Math.Max(maxBottom, platform.Bounds.Bottom);
        foreach (Goal goal in _goals) maxBottom = System.Math.Max(maxBottom, goal.Bounds.Bottom);
        foreach (CheckpointFlag checkpoint in _checkpointFlags) maxBottom = System.Math.Max(maxBottom, checkpoint.Bounds.Bottom);
        foreach (LaunchPad launchPad in _launchPads) maxBottom = System.Math.Max(maxBottom, launchPad.Bounds.Bottom);

        if (maxBottom == int.MinValue)
        {
            maxBottom = (int)PlayerStart.Y + 400;
        }

        Lava = new LavaLine(maxBottom + 240);
    }

    public IEnumerable<Platform> GetCollidablePlatforms(GameColor playerColor)
    {
        foreach (Platform platform in _platforms)
        {
            if (platform.PlatformColor == playerColor)
            {
                yield return platform;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw, float animationSeconds = 0f, bool isEditorMode = false)
    {
        DrawBackground(spriteBatch, pixel);
        DrawPlatforms(spriteBatch, pixel, debugDraw);
        DrawGoals(spriteBatch, pixel, debugDraw);
        DrawCheckpointFlags(spriteBatch, pixel, debugDraw);
        DrawLaunchPads(spriteBatch, pixel, debugDraw, animationSeconds, isEditorMode);
    }

    public void DrawPlatforms(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        foreach (Platform platform in _platforms)
        {
            platform.Draw(spriteBatch, pixel, debugDraw);
        }

        DrawMixedPlatformIntersections(spriteBatch, pixel);
    }

    public void DrawBackground(SpriteBatch spriteBatch, Texture2D pixel)
    {
        spriteBatch.Draw(pixel, new Rectangle(0, 0, WorldSize.X, WorldSize.Y), new Color(36, 41, 52));
        spriteBatch.Draw(pixel, new Rectangle(0, 560, WorldSize.X, WorldSize.Y - 560), new Color(24, 29, 36));
    }

    public void DrawGoals(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        foreach (Goal goal in _goals)
        {
            goal.Draw(spriteBatch, pixel, debugDraw);
        }
    }

    public void DrawCheckpointFlags(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        foreach (CheckpointFlag checkpoint in _checkpointFlags)
        {
            checkpoint.Draw(spriteBatch, pixel, debugDraw);
        }
    }

    public void DrawLaunchPads(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw, float animationSeconds = 0f, bool isEditorMode = false)
    {
        foreach (LaunchPad launchPad in _launchPads)
        {
            launchPad.Draw(spriteBatch, pixel, debugDraw, animationSeconds, isEditorMode: isEditorMode);
        }
    }

    public int GetNextCheckpointId()
    {
        int maxId = 0;
        foreach (CheckpointFlag checkpoint in _checkpointFlags)
        {
            maxId = System.Math.Max(maxId, checkpoint.Id);
        }

        return maxId + 1;
    }

    private void EnsureCheckpointIds()
    {
        HashSet<int> usedIds = new();
        foreach (CheckpointFlag checkpoint in _checkpointFlags)
        {
            if (checkpoint.Id <= 0 || usedIds.Contains(checkpoint.Id))
            {
                checkpoint.Id = GetNextUnusedCheckpointId(usedIds);
            }

            usedIds.Add(checkpoint.Id);
        }
    }

    private static int GetNextUnusedCheckpointId(HashSet<int> usedIds)
    {
        int id = 1;
        while (usedIds.Contains(id))
        {
            id++;
        }

        return id;
    }

    private void DrawMixedPlatformIntersections(SpriteBatch spriteBatch, Texture2D pixel)
    {
        List<Rectangle> mixedBounds = new();

        for (int firstIndex = 0; firstIndex < _platforms.Count; firstIndex++)
        {
            Platform first = _platforms[firstIndex];

            for (int secondIndex = firstIndex + 1; secondIndex < _platforms.Count; secondIndex++)
            {
                Platform second = _platforms[secondIndex];
                Rectangle pairIntersection = GetIntersection(first.Bounds, second.Bounds);
                if (pairIntersection.IsEmpty)
                {
                    continue;
                }

                Color mixedColor = MixGameColors(new List<GameColor>
                {
                    first.PlatformColor,
                    second.PlatformColor
                });

                spriteBatch.Draw(pixel, pairIntersection, mixedColor);
                mixedBounds.Add(pairIntersection);
            }
        }

        for (int firstIndex = 0; firstIndex < _platforms.Count; firstIndex++)
        {
            Platform first = _platforms[firstIndex];

            for (int secondIndex = firstIndex + 1; secondIndex < _platforms.Count; secondIndex++)
            {
                Platform second = _platforms[secondIndex];
                Rectangle pairIntersection = GetIntersection(first.Bounds, second.Bounds);
                if (pairIntersection.IsEmpty)
                {
                    continue;
                }

                for (int thirdIndex = secondIndex + 1; thirdIndex < _platforms.Count; thirdIndex++)
                {
                    Platform third = _platforms[thirdIndex];
                    Rectangle tripleIntersection = GetIntersection(pairIntersection, third.Bounds);
                    if (tripleIntersection.IsEmpty)
                    {
                        continue;
                    }

                    Color mixedColor = MixGameColors(new List<GameColor>
                    {
                        first.PlatformColor,
                        second.PlatformColor,
                        third.PlatformColor
                    });

                    spriteBatch.Draw(pixel, tripleIntersection, mixedColor);
                    mixedBounds.Add(tripleIntersection);
                }
            }
        }

        foreach (Rectangle bounds in mixedBounds)
        {
            DrawHelper.DrawBorder(spriteBatch, pixel, bounds, Color.Black, 2);
        }
    }

    public static Rectangle GetIntersection(Rectangle a, Rectangle b)
    {
        int left = System.Math.Max(a.Left, b.Left);
        int top = System.Math.Max(a.Top, b.Top);
        int right = System.Math.Min(a.Right, b.Right);
        int bottom = System.Math.Min(a.Bottom, b.Bottom);

        if (right <= left || bottom <= top)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(left, top, right - left, bottom - top);
    }

    public static Color MixGameColors(IReadOnlyList<GameColor> colors) =>
        ColorPaletteManager.MixGameColors(colors);
}
