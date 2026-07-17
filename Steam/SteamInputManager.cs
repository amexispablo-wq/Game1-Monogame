#nullable enable
using System;
using System.IO;
using Steamworks;

namespace ColorBlocks;

/// <summary>
/// Steam Input layer only: init, shutdown, RunFrame, controller discovery,
/// local-player handle mapping, digital/analog reads, glyphs, controller type.
/// No gameplay logic.
/// </summary>
public sealed class SteamInputManager
{
    private readonly SteamManager _steam;
    private readonly InputHandle_t[] _connectedHandles = new InputHandle_t[Constants.STEAM_INPUT_MAX_COUNT];
    private readonly InputHandle_t[] _slotHandles = new InputHandle_t[InputManager.MaxLocalPlayers];
    private readonly InputDigitalActionHandle_t[] _digitalHandles;
    private readonly InputAnalogActionHandle_t[] _analogHandles;
    private readonly string[] _digitalNames;
    private readonly string[] _analogNames;

    private bool _isInitialized;
    private int _connectedCount;
    private InputActionSetHandle_t _gameplaySet;
    private Callback<SteamInputConfigurationLoaded_t>? _configLoaded;
    private Callback<SteamInputDeviceConnected_t>? _deviceConnected;
    private Callback<SteamInputDeviceDisconnected_t>? _deviceDisconnected;
    private string _activeLayoutLabel = "—";
    private DateTime _lastLayoutRefreshUtc = DateTime.MinValue;

    public SteamInputManager(SteamManager steam)
    {
        _steam = steam;
        _digitalNames = SteamInputActionNames.DigitalActions;
        _analogNames = SteamInputActionNames.AnalogActions;
        _digitalHandles = new InputDigitalActionHandle_t[_digitalNames.Length];
        _analogHandles = new InputAnalogActionHandle_t[_analogNames.Length];
        Glyphs = new SteamInputGlyphProvider(this);
    }

    public bool IsInitialized => _isInitialized;
    public bool IsEnabled => _isInitialized;
    public int ConnectedControllerCount => _connectedCount;
    public InputActionSetHandle_t GameplayActionSet => _gameplaySet;
    public SteamInputGlyphProvider Glyphs { get; }
    public string CurrentActionSetName => SteamInputActionNames.ActionSetGameplay;
    public string ActiveLayoutLabel => _activeLayoutLabel;
    public DateTime LastLayoutRefreshUtc => _lastLayoutRefreshUtc;
    public string GlyphSource => Glyphs.GlyphSource;

    public void Initialize()
    {
        if (_isInitialized || !_steam.IsInitialized)
        {
            return;
        }

        try
        {
            if (!SteamInput.Init(bExplicitlyCallRunFrame: true))
            {
                return;
            }

            string manifestPath = Path.Combine(AppContext.BaseDirectory, "steam_input_manifest.vdf");
            if (File.Exists(manifestPath))
            {
                SteamInput.SetInputActionManifestFilePath(manifestPath);
            }

            CacheActionHandles();
            RegisterCallbacks();
            RefreshConnectedControllers();
            _isInitialized = true;
            _lastLayoutRefreshUtc = DateTime.UtcNow;
            Glyphs.Invalidate();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            _isInitialized = false;
        }
    }

    public void RunFrame()
    {
        if (!_isInitialized)
        {
            return;
        }

        try
        {
            SteamInput.RunFrame();
            RefreshConnectedControllers();
            ActivateGameplaySetOnAll();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            _isInitialized = false;
        }
    }

    public void Shutdown()
    {
        if (!_isInitialized)
        {
            return;
        }

        try
        {
            UnregisterCallbacks();
            SteamInput.Shutdown();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
        }
        finally
        {
            _isInitialized = false;
            _connectedCount = 0;
            Array.Clear(_connectedHandles, 0, _connectedHandles.Length);
            Array.Clear(_slotHandles, 0, _slotHandles.Length);
        }
    }

    public InputHandle_t GetHandleForSlot(int localPlayerSlot)
    {
        if (!_isInitialized || localPlayerSlot < 0 || localPlayerSlot >= InputManager.MaxLocalPlayers)
        {
            return default;
        }

        return _slotHandles[localPlayerSlot];
    }

    public InputHandle_t GetPrimaryHandle()
    {
        if (!_isInitialized || _connectedCount <= 0)
        {
            return default;
        }

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (_slotHandles[i].m_InputHandle != 0)
            {
                return _slotHandles[i];
            }
        }

        return _connectedHandles[0];
    }

    public bool TryGetSlotForHandle(InputHandle_t handle, out int slot)
    {
        slot = -1;
        if (handle.m_InputHandle == 0)
        {
            return false;
        }

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (_slotHandles[i].m_InputHandle == handle.m_InputHandle)
            {
                slot = i;
                return true;
            }
        }

        return false;
    }

    public SteamInputControllerType GetControllerType(int localPlayerSlot)
    {
        return ToControllerType(GetSteamInputType(localPlayerSlot));
    }

    public ESteamInputType GetSteamInputType(int localPlayerSlot)
    {
        if (!_isInitialized)
        {
            return ESteamInputType.k_ESteamInputType_Unknown;
        }

        InputHandle_t handle = GetHandleForSlot(localPlayerSlot);
        if (handle.m_InputHandle == 0)
        {
            return ESteamInputType.k_ESteamInputType_Unknown;
        }

        try
        {
            return SteamInput.GetInputTypeForHandle(handle);
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            return ESteamInputType.k_ESteamInputType_Unknown;
        }
    }

    public string GetControllerLabel(int localPlayerSlot)
    {
        return FormatControllerType(GetSteamInputType(localPlayerSlot));
    }

    public InputDigitalActionHandle_t GetDigitalHandle(string actionName)
    {
        for (int i = 0; i < _digitalNames.Length; i++)
        {
            if (string.Equals(_digitalNames[i], actionName, StringComparison.Ordinal))
            {
                return _digitalHandles[i];
            }
        }

        return default;
    }

    public InputAnalogActionHandle_t GetAnalogHandle(string actionName)
    {
        for (int i = 0; i < _analogNames.Length; i++)
        {
            if (string.Equals(_analogNames[i], actionName, StringComparison.Ordinal))
            {
                return _analogHandles[i];
            }
        }

        return default;
    }

    public bool GetDigital(int localPlayerSlot, string actionName)
    {
        InputHandle_t handle = GetHandleForSlot(localPlayerSlot);
        if (handle.m_InputHandle == 0)
        {
            return false;
        }

        InputDigitalActionHandle_t action = GetDigitalHandle(actionName);
        if (action.m_InputDigitalActionHandle == 0)
        {
            return false;
        }

        try
        {
            InputDigitalActionData_t data = SteamInput.GetDigitalActionData(handle, action);
            return data.bActive != 0 && data.bState != 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool GetDigitalAny(string actionName)
    {
        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            if (GetDigital(i, actionName))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetAnalog(int localPlayerSlot, string actionName, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        InputHandle_t handle = GetHandleForSlot(localPlayerSlot);
        if (handle.m_InputHandle == 0)
        {
            return false;
        }

        InputAnalogActionHandle_t action = GetAnalogHandle(actionName);
        if (action.m_InputAnalogActionHandle == 0)
        {
            return false;
        }

        try
        {
            InputAnalogActionData_t data = SteamInput.GetAnalogActionData(handle, action);
            if (data.bActive == 0)
            {
                return false;
            }

            x = data.x;
            y = data.y;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool OpenSteamControllerConfiguration(int localPlayerSlot = 0)
    {
        if (!_isInitialized || !_steam.IsOverlayEnabled)
        {
            return false;
        }

        InputHandle_t handle = GetHandleForSlot(localPlayerSlot);
        if (handle.m_InputHandle == 0)
        {
            handle = GetPrimaryHandle();
        }

        if (handle.m_InputHandle == 0)
        {
            return false;
        }

        try
        {
            return SteamInput.ShowBindingPanel(handle);
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            return false;
        }
    }

    public void ForceRefreshGlyphs()
    {
        _lastLayoutRefreshUtc = DateTime.UtcNow;
        _activeLayoutLabel = "refreshed";
        Glyphs.Invalidate();
    }

    public void DumpActionOriginsToConsole() => Glyphs.DumpOriginsToConsole();

    public static SteamInputControllerType ToControllerType(ESteamInputType type) => type switch
    {
        ESteamInputType.k_ESteamInputType_XBox360Controller
            or ESteamInputType.k_ESteamInputType_XBoxOneController => SteamInputControllerType.Xbox,
        ESteamInputType.k_ESteamInputType_PS3Controller
            or ESteamInputType.k_ESteamInputType_PS4Controller
            or ESteamInputType.k_ESteamInputType_PS5Controller => SteamInputControllerType.PlayStation,
        ESteamInputType.k_ESteamInputType_SwitchProController
            or ESteamInputType.k_ESteamInputType_SwitchJoyConSingle
            or ESteamInputType.k_ESteamInputType_SwitchJoyConPair => SteamInputControllerType.Switch,
        ESteamInputType.k_ESteamInputType_SteamDeckController => SteamInputControllerType.SteamDeck,
        ESteamInputType.k_ESteamInputType_SteamController => SteamInputControllerType.SteamController,
        ESteamInputType.k_ESteamInputType_GenericGamepad => SteamInputControllerType.Generic,
        _ => SteamInputControllerType.Unknown
    };

    public static string FormatControllerType(ESteamInputType type) => type switch
    {
        ESteamInputType.k_ESteamInputType_PS4Controller => "DualShock 4",
        ESteamInputType.k_ESteamInputType_PS5Controller => "DualSense",
        ESteamInputType.k_ESteamInputType_XBox360Controller => "Xbox 360",
        ESteamInputType.k_ESteamInputType_XBoxOneController => "Xbox One",
        ESteamInputType.k_ESteamInputType_SwitchProController => "Switch Pro",
        ESteamInputType.k_ESteamInputType_SteamDeckController => "Steam Deck",
        ESteamInputType.k_ESteamInputType_SteamController => "Steam Controller",
        ESteamInputType.k_ESteamInputType_GenericGamepad => "Generic Gamepad",
        ESteamInputType.k_ESteamInputType_PS3Controller => "DualShock 3",
        _ => "Gamepad"
    };

    private void CacheActionHandles()
    {
        _gameplaySet = SteamInput.GetActionSetHandle(SteamInputActionNames.ActionSetGameplay);
        for (int i = 0; i < _digitalNames.Length; i++)
        {
            _digitalHandles[i] = SteamInput.GetDigitalActionHandle(_digitalNames[i]);
        }

        for (int i = 0; i < _analogNames.Length; i++)
        {
            _analogHandles[i] = SteamInput.GetAnalogActionHandle(_analogNames[i]);
        }
    }

    private void RegisterCallbacks()
    {
        _configLoaded = Callback<SteamInputConfigurationLoaded_t>.Create(OnConfigurationLoaded);
        _deviceConnected = Callback<SteamInputDeviceConnected_t>.Create(OnDeviceConnected);
        _deviceDisconnected = Callback<SteamInputDeviceDisconnected_t>.Create(OnDeviceDisconnected);
    }

    private void UnregisterCallbacks()
    {
        _configLoaded?.Dispose();
        _deviceConnected?.Dispose();
        _deviceDisconnected?.Dispose();
        _configLoaded = null;
        _deviceConnected = null;
        _deviceDisconnected = null;
    }

    private void OnConfigurationLoaded(SteamInputConfigurationLoaded_t data)
    {
        _activeLayoutLabel = $"cfg:{data.m_unAppID}";
        ForceRefreshGlyphs();
    }

    private void OnDeviceConnected(SteamInputDeviceConnected_t data)
    {
        RefreshConnectedControllers();
        ForceRefreshGlyphs();
    }

    private void OnDeviceDisconnected(SteamInputDeviceDisconnected_t data)
    {
        RefreshConnectedControllers();
        ForceRefreshGlyphs();
    }

    private void RefreshConnectedControllers()
    {
        try
        {
            _connectedCount = SteamInput.GetConnectedControllers(_connectedHandles);
        }
        catch (Exception)
        {
            _connectedCount = 0;
            return;
        }

        for (int i = 0; i < InputManager.MaxLocalPlayers; i++)
        {
            _slotHandles[i] = default;
        }

        // Prefer Steam's XInput slot mapping when available.
        for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
        {
            try
            {
                InputHandle_t mapped = SteamInput.GetControllerForGamepadIndex(slot);
                if (mapped.m_InputHandle != 0)
                {
                    _slotHandles[slot] = mapped;
                }
            }
            catch (Exception)
            {
            }
        }

        // Fill remaining local slots from connected handles not yet assigned.
        for (int c = 0; c < _connectedCount; c++)
        {
            InputHandle_t handle = _connectedHandles[c];
            if (handle.m_InputHandle == 0)
            {
                continue;
            }

            bool alreadyMapped = false;
            for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
            {
                if (_slotHandles[slot].m_InputHandle == handle.m_InputHandle)
                {
                    alreadyMapped = true;
                    break;
                }
            }

            if (alreadyMapped)
            {
                continue;
            }

            int preferredSlot = -1;
            try
            {
                preferredSlot = SteamInput.GetGamepadIndexForController(handle);
            }
            catch (Exception)
            {
                preferredSlot = -1;
            }

            if (preferredSlot >= 0
                && preferredSlot < InputManager.MaxLocalPlayers
                && _slotHandles[preferredSlot].m_InputHandle == 0)
            {
                _slotHandles[preferredSlot] = handle;
                continue;
            }

            for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
            {
                if (_slotHandles[slot].m_InputHandle == 0)
                {
                    _slotHandles[slot] = handle;
                    break;
                }
            }
        }
    }

    private void ActivateGameplaySetOnAll()
    {
        if (_gameplaySet.m_InputActionSetHandle == 0)
        {
            return;
        }

        for (int i = 0; i < _connectedCount; i++)
        {
            InputHandle_t handle = _connectedHandles[i];
            if (handle.m_InputHandle == 0)
            {
                continue;
            }

            try
            {
                SteamInput.ActivateActionSet(handle, _gameplaySet);
            }
            catch (Exception)
            {
            }
        }
    }

    private static bool IsRecoverableException(Exception exception) =>
        exception is DllNotFoundException
            or BadImageFormatException
            or EntryPointNotFoundException
            or TypeInitializationException;
}

/// <summary>
/// Compatibility alias — prefer <see cref="SteamInputManager"/>.
/// </summary>
public sealed class SteamInputService
{
    private readonly SteamInputManager _manager;

    public SteamInputService(SteamManager steam)
    {
        _manager = new SteamInputManager(steam);
    }

    public SteamInputService(SteamInputManager manager)
    {
        _manager = manager;
    }

    public SteamInputManager Manager => _manager;
    public bool IsInitialized => _manager.IsInitialized;
    public SteamInputGlyphProvider Glyphs => _manager.Glyphs;

    public void Initialize() => _manager.Initialize();
    public void RunFrame() => _manager.RunFrame();
    public void Shutdown() => _manager.Shutdown();
    public ESteamInputType GetControllerType(int gamepadIndex) => _manager.GetSteamInputType(gamepadIndex);
    public string GetControllerLabel(int gamepadIndex) => _manager.GetControllerLabel(gamepadIndex);
    public bool OpenSteamControllerConfiguration(int localPlayerSlot = 0) =>
        _manager.OpenSteamControllerConfiguration(localPlayerSlot);

    public static string FormatControllerType(ESteamInputType type) =>
        SteamInputManager.FormatControllerType(type);
}
