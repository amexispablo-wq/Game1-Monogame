#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace ColorBlocks;

public sealed class SfxManager : IDisposable
{
    public const string Jump = "Jump";
    public const string PullRope = "PullRope";
    public const string Red = "Red";
    public const string Blue = "Blue";
    public const string Green = "Green";
    public const string Checkpoint = "Checkpoint";
    public const string PhysicsExpulsion = "PhysicsExpulsion";
    public const string LaunchPad = "LaunchPad";
    public const string Lava = "Lava";
    public const string MenuNavigation = "MenuNavigation";
    public const string LevelComplete = "LevelComplete";
    public const string Lost = "Lost";

    private const float LavaMaxHearDistance = 450f;
    private const float MenuHoverDebounceSeconds = 0.12f;

    private readonly Dictionary<string, SoundEffect> _effects = new(StringComparer.Ordinal);
    private SoundEffect? _hover1;
    private SoundEffect? _hover2;
    private SoundEffect? _hover3;
    private SoundEffect? _buttonPress;
    private SoundEffect? _pullRope;
    private SoundEffect? _lava;
    private SoundEffect? _physicsExpulsion;

    private SoundEffectInstance? _pullRopeLoop;
    private SoundEffectInstance? _lavaLoop;
    private SoundEffectInstance? _physicsExpulsionLoop;
    private int _physicsExpulsionActiveCount;
    private int _hoverCycleIndex;
    private float _menuHoverCooldown;
    private bool _loaded;

    public void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        TryRegister(Jump, "Audio/SFX/jump.wav");
        TryRegister(PullRope, "Audio/SFX/pullrope.wav");
        TryRegister(Red, "Audio/SFX/red.wav");
        TryRegister(Blue, "Audio/SFX/blue.wav");
        TryRegister(Green, "Audio/SFX/green.wav");
        TryRegister(Checkpoint, "Audio/SFX/checkpoint.wav");
        TryRegister(PhysicsExpulsion, "Audio/SFX/physicexpulsion.wav");
        TryRegister(LaunchPad, "Audio/SFX/launchpad.wav");
        TryRegister(Lava, "Audio/SFX/lava.wav");
        TryRegister(LevelComplete, "Audio/SFX/levelcomplete.wav");
        TryRegister(Lost, "Audio/SFX/Lost.wav");

        _hover1 = ContentResolver.TryLoadSoundEffect("Audio/SFX/hover1.wav");
        _hover2 = ContentResolver.TryLoadSoundEffect("Audio/SFX/hover2.wav");
        _hover3 = ContentResolver.TryLoadSoundEffect("Audio/SFX/hover3.wav");
        _buttonPress = ContentResolver.TryLoadSoundEffect("Audio/SFX/buttonpress.wav");

        _effects.TryGetValue(PullRope, out _pullRope);
        _effects.TryGetValue(Lava, out _lava);
        _effects.TryGetValue(PhysicsExpulsion, out _physicsExpulsion);
    }

    public void Update(float dt)
    {
        if (_menuHoverCooldown > 0f)
        {
            _menuHoverCooldown = Math.Max(0f, _menuHoverCooldown - dt);
        }
    }

    public static bool IsEnabled(string key) =>
        SettingsManager.CurrentSettings.SoundEffects.TryGetValue(key, out bool enabled) && enabled;

    public void Play(string key)
    {
        if (!IsEnabled(key))
        {
            return;
        }

        PlayForce(key);
    }

    /// <summary>Play cue ignoring options toggles (level complete / death).</summary>
    public void PlayForce(string key)
    {
        if (_effects.TryGetValue(key, out SoundEffect? effect) && effect is not null)
        {
            effect.Play();
        }
    }

    public void PlayColor(GameColor color)
    {
        switch (color)
        {
            case GameColor.Red:
                Play(Red);
                break;
            case GameColor.Blue:
                Play(Blue);
                break;
            case GameColor.Green:
                Play(Green);
                break;
        }
    }

    public void PlayMenuHover()
    {
        if (!IsEnabled(MenuNavigation) || _menuHoverCooldown > 0f)
        {
            return;
        }

        SoundEffect? effect = _hoverCycleIndex switch
        {
            0 => _hover1,
            1 => _hover2,
            _ => _hover3
        };

        _hoverCycleIndex = (_hoverCycleIndex + 1) % 3;
        _menuHoverCooldown = MenuHoverDebounceSeconds;
        effect?.Play();
    }

    public void PlayMenuPress()
    {
        if (!IsEnabled(MenuNavigation))
        {
            return;
        }

        _buttonPress?.Play();
    }

    public void BeginPhysicsExpulsion()
    {
        if (!IsEnabled(PhysicsExpulsion))
        {
            return;
        }

        _physicsExpulsionActiveCount++;
        EnsureLoop(ref _physicsExpulsionLoop, _physicsExpulsion, volume: 1f);
    }

    public void EndPhysicsExpulsion()
    {
        if (_physicsExpulsionActiveCount > 0)
        {
            _physicsExpulsionActiveCount--;
        }

        if (_physicsExpulsionActiveCount <= 0)
        {
            _physicsExpulsionActiveCount = 0;
            StopInstance(ref _physicsExpulsionLoop);
        }
    }

    public void SetPullRopeLoop(bool active)
    {
        if (!active || !IsEnabled(PullRope))
        {
            StopInstance(ref _pullRopeLoop);
            return;
        }

        EnsureLoop(ref _pullRopeLoop, _pullRope, volume: 1f);
    }

    public void UpdateLavaProximity(float distanceToSurface)
    {
        if (!IsEnabled(Lava) || _lava is null)
        {
            StopInstance(ref _lavaLoop);
            return;
        }

        float volume = distanceToSurface <= 0f
            ? 1f
            : Math.Clamp(1f - (distanceToSurface / LavaMaxHearDistance), 0f, 1f);

        if (volume <= 0.01f)
        {
            StopInstance(ref _lavaLoop);
            return;
        }

        EnsureLoop(ref _lavaLoop, _lava, volume);
        if (_lavaLoop is not null)
        {
            _lavaLoop.Volume = volume;
        }
    }

    public void StopAllLoops()
    {
        _physicsExpulsionActiveCount = 0;
        StopInstance(ref _pullRopeLoop);
        StopInstance(ref _lavaLoop);
        StopInstance(ref _physicsExpulsionLoop);
    }

    public void Dispose()
    {
        StopAllLoops();
        foreach (SoundEffect effect in _effects.Values)
        {
            effect.Dispose();
        }

        _effects.Clear();
        _hover1?.Dispose();
        _hover2?.Dispose();
        _hover3?.Dispose();
        _buttonPress?.Dispose();
        _hover1 = null;
        _hover2 = null;
        _hover3 = null;
        _buttonPress = null;
        _pullRope = null;
        _lava = null;
        _physicsExpulsion = null;
    }

    private void TryRegister(string key, string relativePath)
    {
        SoundEffect? effect = ContentResolver.TryLoadSoundEffect(relativePath);
        if (effect is not null)
        {
            _effects[key] = effect;
        }
    }

    private static void EnsureLoop(ref SoundEffectInstance? instance, SoundEffect? source, float volume)
    {
        if (source is null)
        {
            return;
        }

        if (instance is null || instance.IsDisposed)
        {
            instance = source.CreateInstance();
            instance.IsLooped = true;
            instance.Volume = volume;
            instance.Play();
            return;
        }

        if (instance.State != SoundState.Playing)
        {
            instance.Volume = volume;
            instance.Play();
        }
    }

    private static void StopInstance(ref SoundEffectInstance? instance)
    {
        if (instance is null)
        {
            return;
        }

        if (!instance.IsDisposed)
        {
            instance.Stop();
            instance.Dispose();
        }

        instance = null;
    }

    public static float DistanceToLavaSurface(float playerBottom, float lavaSurfaceY) =>
        Math.Max(0f, lavaSurfaceY - playerBottom);
}
