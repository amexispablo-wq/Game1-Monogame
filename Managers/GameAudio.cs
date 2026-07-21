#nullable enable
namespace ColorBlocks;

/// <summary>
/// Presentation-side audio entry point. Safe no-op when SFX not loaded (benchmarks/headless).
/// </summary>
public static class GameAudio
{
    public static SfxManager? Sfx { get; set; }

    /// <summary>When true, gameplay cues stay silent — used by menu replay background.</summary>
    public static bool SuppressGameplaySfx { get; set; }

    public static void Update(float dt) => Sfx?.Update(dt);

    public static void Play(string key)
    {
        if (SuppressGameplaySfx)
        {
            return;
        }

        Sfx?.Play(key);
    }

    public static void PlayForce(string key)
    {
        if (SuppressGameplaySfx)
        {
            return;
        }

        Sfx?.PlayForce(key);
    }

    public static void PlayColor(GameColor color)
    {
        if (SuppressGameplaySfx)
        {
            return;
        }

        Sfx?.PlayColor(color);
    }

    public static void PlayMenuHover() => Sfx?.PlayMenuHover();

    public static void PlayMenuPress() => Sfx?.PlayMenuPress();

    public static void SetPullRopeLoop(bool active)
    {
        if (SuppressGameplaySfx)
        {
            active = false;
        }

        Sfx?.SetPullRopeLoop(active);
    }

    public static void UpdateLavaProximity(float distanceToSurface)
    {
        if (SuppressGameplaySfx)
        {
            distanceToSurface = float.MaxValue;
        }

        Sfx?.UpdateLavaProximity(distanceToSurface);
    }

    public static void BeginPhysicsExpulsion()
    {
        if (SuppressGameplaySfx)
        {
            return;
        }

        Sfx?.BeginPhysicsExpulsion();
    }

    public static void EndPhysicsExpulsion() => Sfx?.EndPhysicsExpulsion();

    public static void StopAllLoops() => Sfx?.StopAllLoops();
}
