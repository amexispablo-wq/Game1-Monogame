using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public sealed class Level
{
    private readonly List<Platform> _platforms = new();
    private readonly List<Goal> _goals = new();

    public Level()
    {
        PlayerStart = new Vector2(100f, 300f);
        WorldSize = new Point(1280, 720);
        Name = string.Empty;
    }

    public Level(Vector2 playerStart, IEnumerable<Platform> platforms, IEnumerable<Goal> goals, string name = "")
    {
        PlayerStart = playerStart;
        Name = name;
        _platforms.AddRange(platforms);
        _goals.AddRange(goals);
        RecalculateWorldSize();
    }

    public string Name { get; set; }
    public Vector2 PlayerStart { get; set; }
    public Point WorldSize { get; private set; }
    public IReadOnlyList<Platform> Platforms => _platforms;
    public IReadOnlyList<Goal> Goals => _goals;

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

    public static Level FromData(LevelData data)
    {
        List<Platform> platforms = new();
        List<Goal> goals = new();

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

        return new Level(new Vector2(data.PlayerSpawn.X, data.PlayerSpawn.Y), platforms, goals, data.Name);
    }

    public LevelData ToData()
    {
        LevelData data = new()
        {
            Name = Name,
            PlayerSpawn = new Vector2Data { X = PlayerStart.X, Y = PlayerStart.Y }
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

        WorldSize = new Point(width, height);
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

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, bool debugDraw)
    {
        DrawBackground(spriteBatch, pixel);
        DrawPlatforms(spriteBatch, pixel, debugDraw);
        DrawGoals(spriteBatch, pixel, debugDraw);
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

                Color mixedColor = MixColors(new List<Color>
                {
                    first.PlatformColor.ToXnaColor(),
                    second.PlatformColor.ToXnaColor()
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

                    Color mixedColor = MixColors(new List<Color>
                    {
                        first.PlatformColor.ToXnaColor(),
                        second.PlatformColor.ToXnaColor(),
                        third.PlatformColor.ToXnaColor()
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

    public static Color MixColors(List<Color> colors)
    {
        bool hasRed = colors.Any(color => color == GameColor.Red.ToXnaColor());
        bool hasGreen = colors.Any(color => color == GameColor.Green.ToXnaColor());
        bool hasBlue = colors.Any(color => color == GameColor.Blue.ToXnaColor());

        if (hasRed && hasGreen && hasBlue)
        {
            return Color.White;
        }

        if (hasRed && hasGreen)
        {
            return Color.Yellow;
        }

        if (hasRed && hasBlue)
        {
            return Color.Magenta;
        }

        if (hasGreen && hasBlue)
        {
            return Color.Cyan;
        }

        if (hasRed)
        {
            return GameColor.Red.ToXnaColor();
        }

        if (hasGreen)
        {
            return GameColor.Green.ToXnaColor();
        }

        if (hasBlue)
        {
            return GameColor.Blue.ToXnaColor();
        }

        return Color.White;
    }
}
