#nullable enable
using System;
using System.IO;
using System.Text;
using Steamworks;

namespace ColorBlocks;

/// <summary>
/// Steam Input layer only: init, shutdown, RunFrame, controller discovery,
/// local-player handle mapping, digital/analog reads, glyphs, controller type.
/// No gameplay logic.
/// </summary>
public sealed class SteamInputManager
{
    private static readonly TimeSpan DetectionRetryInterval = TimeSpan.FromSeconds(1);

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
    private DateTime _lastDetectionAttemptUtc = DateTime.MinValue;
    private int _detectionRetryCount;
    private bool _actionHandlesComplete;

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

    /// <summary>Human-readable pipeline status for diagnostics (F3 panel).</summary>
    public string InitializationStatus { get; private set; } = "Not initialized";

    /// <summary>How many 1s detection retries have run while no controller was connected.</summary>
    public int DetectionRetryCount => _detectionRetryCount;

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        if (!_steam.IsInitialized)
        {
            InitializationStatus = $"SteamAPI.Init failed ({_steam.Status})";
            SteamInputLog.Log($"Init skipped: SteamAPI not initialized ({_steam.Status})");
            return;
        }

        SteamInputLog.Log("SteamAPI.Init OK — initializing Steam Input");

        try
        {
            // Manifest path must be registered before SteamInput.Init so Steam
            // resolves in-game actions from the bundled VDF instead of Workshop.
            string manifestPath = Path.Combine(AppContext.BaseDirectory, "steam_input_manifest.vdf");
            if (File.Exists(manifestPath))
            {
                bool manifestOk = SteamInput.SetInputActionManifestFilePath(manifestPath);
                SteamInputLog.Log($"SetInputActionManifestFilePath('{manifestPath}') -> {manifestOk}");
            }
            else
            {
                SteamInputLog.Log($"Manifest NOT FOUND at '{manifestPath}' — relying on Steam-side config");
            }

            if (!SteamInput.Init(bExplicitlyCallRunFrame: true))
            {
                InitializationStatus = "SteamInput.Init returned false";
                SteamInputLog.Log("SteamInput.Init FAILED (returned false)");
                return;
            }

            SteamInputLog.Log("SteamInput.Init OK (explicit RunFrame mode)");

            // Handles may resolve as 0 until Steam finishes loading the action
            // manifest; the periodic tick re-caches until all are valid.
            CacheActionHandles();
            RegisterCallbacks();
            RefreshConnectedControllers(reason: "startup");
            _isInitialized = true;
            InitializationStatus = "OK";
            _lastLayoutRefreshUtc = DateTime.UtcNow;
            _lastDetectionAttemptUtc = DateTime.UtcNow;
            Glyphs.Invalidate();

            if (_connectedCount == 0)
            {
                SteamInputLog.Log("No controllers at startup — retrying detection every 1s");
            }
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            _isInitialized = false;
            InitializationStatus = $"Init exception: {ex.GetType().Name}";
            SteamInputLog.Log($"Init EXCEPTION: {ex.GetType().Name}: {ex.Message}");
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
            PeriodicDetectionTick();
            ActivateGameplaySetOnAll();
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            _isInitialized = false;
            InitializationStatus = $"RunFrame exception: {ex.GetType().Name}";
            SteamInputLog.Log($"RunFrame EXCEPTION: {ex.GetType().Name}: {ex.Message}");
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
            SteamInputLog.Log("SteamInput.Shutdown OK");
        }
        catch (Exception ex) when (IsRecoverableException(ex))
        {
            SteamInputLog.Log($"Shutdown EXCEPTION: {ex.GetType().Name}");
        }
        finally
        {
            _isInitialized = false;
            InitializationStatus = "Shut down";
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

    // ---- Diagnostics (F3 panel) ----

    /// <summary>Raw slot-to-handle mapping value; 0 = unassigned.</summary>
    public ulong GetSlotHandleRaw(int localPlayerSlot) => GetHandleForSlot(localPlayerSlot).m_InputHandle;

    public ulong GameplayActionSetRaw => _gameplaySet.m_InputActionSetHandle;

    /// <summary>Comma-separated action names whose handles resolved to 0. Empty when all valid.</summary>
    public string GetMissingActionSummary()
    {
        StringBuilder? sb = null;
        void Append(string name)
        {
            sb ??= new StringBuilder();
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            sb.Append(name);
        }

        if (_gameplaySet.m_InputActionSetHandle == 0)
        {
            Append($"set:{SteamInputActionNames.ActionSetGameplay}");
        }

        for (int i = 0; i < _digitalNames.Length; i++)
        {
            if (_digitalHandles[i].m_InputDigitalActionHandle == 0)
            {
                Append(_digitalNames[i]);
            }
        }

        for (int i = 0; i < _analogNames.Length; i++)
        {
            if (_analogHandles[i].m_InputAnalogActionHandle == 0)
            {
                Append(_analogNames[i]);
            }
        }

        return sb?.ToString() ?? string.Empty;
    }

    /// <summary>Compact snapshot of pressed digital actions for one slot, e.g. "Jump ColorRed".</summary>
    public string GetDigitalStateSummary(int localPlayerSlot)
    {
        if (!_isInitialized || GetSlotHandleRaw(localPlayerSlot) == 0)
        {
            return string.Empty;
        }

        StringBuilder? sb = null;
        for (int i = 0; i < _digitalNames.Length; i++)
        {
            if (GetDigital(localPlayerSlot, _digitalNames[i]))
            {
                sb ??= new StringBuilder();
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(_digitalNames[i]);
            }
        }

        return sb?.ToString() ?? string.Empty;
    }

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

    private void PeriodicDetectionTick()
    {
        DateTime now = DateTime.UtcNow;
        if (now - _lastDetectionAttemptUtc < DetectionRetryInterval)
        {
            return;
        }

        _lastDetectionAttemptUtc = now;

        // Action handles can resolve to 0 until Steam loads the manifest;
        // keep re-caching until the full set is valid.
        if (!_actionHandlesComplete)
        {
            CacheActionHandles();
        }

        if (_connectedCount == 0)
        {
            _detectionRetryCount++;
            RefreshConnectedControllers(reason: $"retry #{_detectionRetryCount}");
        }
        else
        {
            // Cheap periodic re-sync so slot mapping stays correct even if a
            // connect/disconnect callback was missed.
            RefreshConnectedControllers(reason: null);
        }
    }

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

        string missing = GetMissingActionSummary();
        bool complete = missing.Length == 0;
        if (complete && !_actionHandlesComplete)
        {
            SteamInputLog.Log($"Action handles OK: set=0x{_gameplaySet.m_InputActionSetHandle:X} " +
                $"digital={_digitalNames.Length} analog={_analogNames.Length}");
        }
        else if (!complete)
        {
            SteamInputLog.Log($"Action handles INCOMPLETE, missing: {missing}");
        }

        _actionHandlesComplete = complete;
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
        SteamInputLog.Log($"ConfigurationLoaded: app={data.m_unAppID} handle=0x{data.m_ulDeviceHandle.m_InputHandle:X}");
        CacheActionHandles();
        RefreshConnectedControllers(reason: "config loaded");
        ForceRefreshGlyphs();
    }

    private void OnDeviceConnected(SteamInputDeviceConnected_t data)
    {
        SteamInputLog.Log($"DeviceConnected: handle=0x{data.m_ulConnectedDeviceHandle.m_InputHandle:X}");
        RefreshConnectedControllers(reason: "device connected");
        ForceRefreshGlyphs();
    }

    private void OnDeviceDisconnected(SteamInputDeviceDisconnected_t data)
    {
        SteamInputLog.Log($"DeviceDisconnected: handle=0x{data.m_ulDisconnectedDeviceHandle.m_InputHandle:X}");
        RefreshConnectedControllers(reason: "device disconnected");
        ForceRefreshGlyphs();
    }

    private void RefreshConnectedControllers(string? reason)
    {
        int previousCount = _connectedCount;

        try
        {
            _connectedCount = SteamInput.GetConnectedControllers(_connectedHandles);
        }
        catch (Exception ex)
        {
            _connectedCount = 0;
            SteamInputLog.Log($"GetConnectedControllers EXCEPTION: {ex.GetType().Name}");
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

        bool countChanged = _connectedCount != previousCount;
        bool isRetry = reason is not null && reason.StartsWith("retry", StringComparison.Ordinal);
        if (countChanged || (reason is not null && !isRetry) || (isRetry && _connectedCount > 0))
        {
            LogControllerSnapshot(reason ?? "periodic");
        }

        if (isRetry && _connectedCount > 0)
        {
            SteamInputLog.Log($"Controller detected after {_detectionRetryCount} retries");
            _detectionRetryCount = 0;
        }
    }

    private void LogControllerSnapshot(string reason)
    {
        SteamInputLog.Log($"Controllers ({reason}): count={_connectedCount}");
        for (int slot = 0; slot < InputManager.MaxLocalPlayers; slot++)
        {
            ulong raw = _slotHandles[slot].m_InputHandle;
            if (raw == 0)
            {
                continue;
            }

            string type;
            try
            {
                type = FormatControllerType(SteamInput.GetInputTypeForHandle(_slotHandles[slot]));
            }
            catch (Exception)
            {
                type = "?";
            }

            SteamInputLog.Log($"  slot {slot}: handle=0x{raw:X} type={type}");
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
