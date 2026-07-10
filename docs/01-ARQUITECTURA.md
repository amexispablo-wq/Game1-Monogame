# 01 — Arquitectura

## Stack

- **Motor:** MonoGame 3.8 (`MonoGame.Framework.DesktopGL`) + `MonoGame.Content.Builder.Task`.
- **Runtime:** .NET 9 (`net9.0`), `WinExe`, `x64`, `RollForward=Major`.
- **Steam:** `Steamworks.NET` 2024.8.0.
- **Otros:** `System.Drawing.Common` 8.0.0 (usado por previews de nivel).
- **Namespace global:** `ColorBlocks`.
- **Render:** todo se dibuja con un único `Texture2D` blanco de 1x1 (`_pixel`) escalado; no hay sprites/atlas. El texto usa un renderer propio (`Graphics/SimpleTextRenderer.cs`).

## Estructura de carpetas

```
Core/         Entry point, clase Game, bucle de simulación
  Program.cs
  ColorBlocksGame.cs   -> Game de MonoGame, dueño de input/steam/escena actual
  GameSimulation.cs    -> bucle de tick fijo, autoridad de gameplay

Scenes/       Pantallas (patrón IScene)
  IScene.cs, MenuScene, PartyScene, LevelSelectScene, GameScene, EditorScene,
  OptionsScene, LevelInfoScene, RopeSandboxScene (dev), CustomizationScene,
  ReplayViewerScene, EditorObjectKind, EditorClipboardItem

Gameplay/     Tuning en vivo
  GameplayTuning, DeveloperTuningPanel

Developer/    Herramientas dev (benchmarks, fuzz)
  GameplayBenchmark/  BenchmarkRunner, RopeMechanicsSimulation, scenarios, CLI headless

Party/        Coop local + roster Steam
  PartyManager, PartyMember, PartyMemberId, PartyInputSource

UI/           Widgets + navegación
  Button, Slider, Checkbox, Dropdown, Popup, PauseMenuOverlay, PartyHudOverlay
  Navigation/ UIFocusManager, NavigationGraph, Focusables, EditModeController,
              NavigationDebug, VirtualCursor, ResolutionCatalog

Entities/     Objetos de juego
  Player, Rope, RopeNode, RopeConstraint, RopeTensionPhase, RopeGameplayMode,
  Platform, Goal, CheckpointFlag, LaunchPad, PlayerState, PlayerIdentity

Managers/     Servicios y estado
  PhysicsWorld, PlayerManager, InputManager, InputDevice, InputProfile,
  SettingsManager, LevelStorage, BestTimeStorage

Networking/   Coop online (andamiaje)
  GameSession, NetworkOwners, NetworkIdAllocator, NetworkEntityOwnership,
  INetworkEntity
  Prediction/  TickRate, SimulationTick, InputFrame, PlayerInputState,
               NetworkInputBuffer, ILocalPlayerInputSource
  Replication/ GameSnapshot, PlayerSnapshot, RopeSnapshot, LevelSnapshot,
               TimerSnapshot, NetworkVector2
  Packets/     NetworkPacket, InputFramePacket, GameSnapshotPacket

LevelSystem/  Niveles
  Level, LevelData, LevelMetadata, LevelManager, LevelMusicLibrary,
  LevelPreviewManager, DeveloperSettings

Graphics/     Camera, DrawHelper, SimpleTextRenderer
Utils/        GameColor, CollisionHelper, GameSettings, GridLayout
Steam/        SteamManager, SteamLobbyService, SteamPartyService,
              SteamCallbackManager, SteamConstants + Native/Windows-x64/steam_api64.dll
Content/      Content.mgcb, level.json (legado), Levels/ (en build output)
```

## Bucle principal

`ColorBlocksGame` (MonoGame `Game`):

1. **Constructor:** crea `GraphicsDeviceManager` (default 1280x720, vsync on), inicializa `SettingsManager` y aplica resolución guardada.
2. **`Initialize()`:** `Steam.Initialize()`, crea `InputManager`.
3. **`LoadContent()`:** crea `SpriteBatch`, el pixel 1x1, y arranca con `MenuScene`.
4. **`Update(gameTime)`:** `Steam.RunCallbacks()` → `Input.Update()` → `escenaActual.Update()`.
5. **`Draw(gameTime)`:** limpia a color `(23,27,34)` → `escenaActual.Draw()`.

La escena actual se cambia con `ChangeScene(IScene)`, que llama `OnExit()` a la saliente.

### Tick fijo vs. render

El render corre a la tasa de MonoGame (vsync), pero el **gameplay corre a tick fijo** dentro de `GameSimulation.Advance(frameSeconds, inputSource)`:

```16:76:Core/GameSimulation.cs
public int Advance(float frameSeconds, ILocalPlayerInputSource localInputSource)
```

- `FixedDeltaSeconds = 1/60` (configurable vía `GameSessionSettings.SimulationTicksPerSecond`).
- Acumulador de tiempo (`_fixedTimeAccumulator`), máximo `MaxFrameTime = 0.25s`, máximo `MaxTicksPerFrame = 5` por frame (anti spiral-of-death).
- Cada tick: captura input local → `NetworkInputBuffer` → física → checkpoints → timer → chequeo de meta → genera `GameSnapshot`.

**Regla de oro:** toda la lógica determinista de gameplay va en `StepFixedTick`. El render y la cámara (`GameScene.UpdateCamera`) pueden correr por frame.

## Capas de responsabilidad

```
ColorBlocksGame (host: input, steam, escena)
        │
        ▼
     IScene (Menu / LevelSelect / Game / Editor / Options)
        │  (GameScene es la que juega)
        ▼
  GameSimulation  ── autoridad: tick, timer, meta, snapshots
        │
        ├─ PlayerManager   (spawns, checkpoints, respawn)
        ├─ PhysicsWorld    (gravedad, colisiones, sogas, launch pads)
        ├─ NetworkInputBuffer (inputs por tick y por jugador)
        └─ Level           (geometría: platforms, goals, checkpoints, pads)
        │
        ▼
   GameSession  ── rol (LocalTest/Host/Client), ownership, peers, settings
```

## Flujo de escenas

```
MenuScene
├─ "Play"          → LevelSelectScene(PlayMode)
│                      └─ seleccionar nivel → GameScene(levelId, ropeMode)
├─ "Party"         → PartyScene (coop local + lobby Steam)
│                      └─ Play → LevelSelectScene(PlayMode) → GameScene
├─ "Level Editor"  → LevelSelectScene(EditMode)
│                      ├─ Edit   → EditorScene(levelId)
│                      ├─ Create → Popup texto → LevelManager.CreateNewLevel
│                      └─ Delete → Popup confirmación → LevelManager.DeleteLevel
├─ "Rope Sandbox"  → RopeSandboxScene (solo DeveloperMode)
└─ "Options"       → OptionsScene (display, audio, rebinding teclado/gamepad)
```

- `GameScene` crea sesión con `GameSession.CreateLocalTest(...)` (multijugador local en un proceso).
- `PartyScene` gestiona hasta 4 miembros (teclado + gamepads) y lobby Steam (invite, roster, kick).
- `LevelSelectScene` mantiene el `RopeGameplayMode` elegido en un campo `static` entre transiciones.
- Navegación UI en todas las escenas: ver [`07-UI-NAVEGACION.md`](07-UI-NAVEGACION.md).

## Patrón IScene

```1:12:Scenes/IScene.cs
```
Toda escena implementa `Update(GameTime)`, `Draw(GameTime, SpriteBatch)` y `OnExit()`. Las escenas hacen su propio layout responsive en cada frame (no hay layout cacheado global).

## Modelo de entidades en red

`INetworkEntity` define identidad y autoridad:

```1:11:Networking/INetworkEntity.cs
```

- `Player` y `Rope` lo implementan.
- `NetworkEntityOwnership(NetworkId, OwnerId, IsLocal, IsHostControlled)` configura cada entidad.
- `NetworkOwners.HostOwnerId = 0`, `UnassignedOwnerId = -1`.
- `PhysicsWorld.ShouldSimulate(player)` = `IsLocal || IsHostControlled` → en host se simula todo; en cliente sólo lo local (el resto se aplicará por snapshot).

Ver detalles de red en [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md).
