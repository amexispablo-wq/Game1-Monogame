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
Core/           Entry point, clase Game, bucle de simulación, PresentationManager
  Program.cs
  ColorBlocksGame.cs   -> Game de MonoGame; dueño de input/steam/escena/servicios
  GameSimulation.cs    -> bucle de tick fijo, autoridad de gameplay

Scenes/         Pantallas (patrón IScene)
  MenuScene, PartyScene, LevelSelectScene, LevelInfoScene, GameScene,
  EditorScene, OptionsScene, CustomizationScene, LeaderboardScene,
  ReplayViewerScene, RopeSandboxScene (dev), IScene

Gameplay/       Tuning en vivo
  GameplayTuning, DeveloperTuningPanel

Developer/      Herramientas dev (benchmarks, fuzz)
  GameplayBenchmark/  BenchmarkRunner, RopeMechanicsSimulation, scenarios, CLI headless

Party/          Coop local + roster Steam
  PartyManager, PartyMember, PartyMemberId, PartyInputSource, PartyHudOverlay helpers

UI/             Widgets + navegación
  Button, Slider, Checkbox, Dropdown, Popup, PauseMenuOverlay, PartyHudOverlay
  Navigation/ UIFocusManager, NavigationGraph, Focusables, EditModeController,
              NavigationDebug, VirtualCursor, ResolutionCatalog

Entities/       Objetos de juego
  Player, Rope, RopeNode, RopeConstraint, RopeTensionPhase, RopeGameplayMode,
  Platform, Goal, CheckpointFlag, LaunchPad, PlayerState, PlayerIdentity

Managers/       Servicios y estado
  PhysicsWorld, PlayerManager, InputManager (+ Input/ backends), SettingsManager,
  BestTimeStorage, MusicManager, SfxManager, Haptics/

Networking/     Coop online
  GameSession, GameNetworkCoordinator, NetworkOwners, INetworkEntity
  Prediction/  TickRate, InputFrame, NetworkInputBuffer, …
  Replication/ GameSnapshot, PlayerSnapshot, RopeSnapshot, …
  Packets/     NetworkPacket, InputFramePacket, GameSnapshotPacket

LevelSystem/    Niveles (no hay LevelManager)
  LevelLibrary, Level, LevelData, LevelMetadata, LevelIdentity, LevelSource,
  LevelContentPaths, LevelRules, LevelMigration, LevelPreviewManager,
  LevelMusicLibrary, DeveloperSettings

Replay/         Grabación, reproducción, highlights, ghosts locales
  ReplayRecorder, ReplayPlayer, ReplayStorage, GhostPlayer, HighlightManager, …

Customization/  Skins de jugador
Accessibility/
Diagnostics/
Graphics/       Camera, DrawHelper, SimpleTextRenderer, PlayerSkinRenderer
Steam/          SteamManager, CallbackManager, Lobby, Party, Invite, Input,
                GameNetwork, Leaderboard, Workshop, Replay, Ghost + Native DLL
Content/        Content.mgcb, OfficialLevels/
```

## Servicios dueños en `ColorBlocksGame`

Instanciados en el ctor / `Initialize`; las escenas **no** crean Steam services sueltos:

| Servicio | Rol |
|----------|-----|
| `SteamManager` | Init / RunCallbacks / Shutdown |
| `SteamCallbackManager` | Callbacks Steamworks tipados |
| `SteamLobbyService` | Lobby friends-only, level-start broadcast |
| `SteamPartyService` | Roster ↔ lobby member data |
| `SteamInviteManager` | Overlay + Rich Presence join + prompt in-game |
| `SteamInputManager` | Steam Input + glyphs |
| `SteamGameNetworkService` | `ISteamNetworkingMessages` |
| `GameNetworkCoordinator` | Pump input/snapshots en sesión online |
| `SteamLeaderboardService` | Upload/download boards |
| `SteamWorkshopService` | Publish / sync subs |
| `SteamReplayService` / `SteamGhostService` | UGC replay + cache WR ghost |
| `LevelStartRouter` | Aplica START pendiente al cambiar escena |
| `PartyManager` | Party local + bind Steam |

## Bucle principal

`ColorBlocksGame` (MonoGame `Game`):

1. **Constructor:** `GraphicsDeviceManager`, `SettingsManager`, crea servicios Steam/net.
2. **`Initialize()`:** `Steam.Initialize()`, callbacks, bind party, workshop sync, launch-join.
3. **`LoadContent()`:** `SpriteBatch`, pixel 1x1, `MenuScene`.
4. **`Update(gameTime)`:** `Steam.RunCallbacks()` → Steam Input RunFrame → `Input.Update()` → prompt invite (si hay) **o** `escenaActual.Update()`.
5. **`Draw(gameTime)`:** presentation → escena → Party HUD → overlays → invite popup.

La escena actual se cambia con `ChangeScene(IScene)` (`OnExit` en la saliente; `SetInLevel` para suprimir invites en `GameScene`).

### Tick fijo vs. render

El render corre a la tasa de MonoGame (vsync), pero el **gameplay corre a tick fijo** dentro de `GameSimulation.Advance(frameSeconds, inputSource)`:

- `FixedDeltaSeconds = 1/60` (configurable vía `GameSessionSettings.SimulationTicksPerSecond`).
- Acumulador de tiempo, máximo `MaxFrameTime = 0.25s`, máximo `MaxTicksPerFrame = 5` (anti spiral-of-death).
- Cada tick: input local → `NetworkInputBuffer` → física → checkpoints → timer → meta → `GameSnapshot`.

**Regla de oro:** lógica determinista de gameplay en `StepFixedTick`. Render/cámara pueden correr por frame.

## Capas de responsabilidad

```
ColorBlocksGame (host: input, steam, escena, prompt invite)
        │
        ▼
     IScene (Menu / Party / LevelSelect / Game / Editor / Options / …)
        │  (GameScene es la que juega)
        ▼
  GameSimulation  ── autoridad: tick, timer, meta, snapshots
        │
        ├─ PlayerManager   (spawns, checkpoints, respawn)
        ├─ PhysicsWorld    (gravedad, colisiones, sogas, launch pads)
        ├─ NetworkInputBuffer (inputs por tick y por jugador)
        └─ Level           (geometría)
        │
        ▼
   GameSession  ── rol (LocalTest/Host/Client), ownership, peers, settings
```

## Flujo de escenas

```
MenuScene
├─ Play            → LevelSelectScene → GameScene
├─ Party           → PartyScene → LevelSelect → GameScene (online/local)
├─ Level Editor    → LevelSelectScene(EditMode) → EditorScene
├─ Customize       → CustomizationScene
├─ Leaderboards    → LeaderboardScene (vía Level Select / post-run)
├─ Replay viewer   → ReplayViewerScene (best local o WR path)
├─ Rope Sandbox    → RopeSandboxScene (solo DeveloperMode)
└─ Options         → OptionsScene
```

- `GameScene` usa `GameSession.CreateLocalTest` o `CreateOnline` según lobby.
- `PartyScene`: input por miembro, Kick (host) / Leave (guest Steam), Invite overlay.
- Invite Accept/Decline global: `ColorBlocksGame` + `SteamInviteManager` (oculto en `GameScene`).
- Navegación UI: [`07-UI-NAVEGACION.md`](07-UI-NAVEGACION.md).

## Patrón IScene

Toda escena implementa `Update(GameTime)`, `Draw(GameTime, SpriteBatch)` y `OnExit()`. Layout responsive por frame (sin cache global).

## Modelo de entidades en red

`INetworkEntity` + `NetworkEntityOwnership(NetworkId, OwnerId, IsLocal, IsHostControlled)`.

- `Player` y `Rope` lo implementan.
- `PhysicsWorld.ShouldSimulate(player)` = `IsLocal || IsHostControlled` → host simula todo; cliente (futuro) solo local + snapshots.

Ver [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md). Playbook reutilizable: [`monogame-playbook/`](monogame-playbook/README.md).
