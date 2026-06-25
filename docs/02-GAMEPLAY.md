# 02 — Gameplay y Mecánicas

## Concepto

Plataformas 2D donde los jugadores cambian de **color** (Rojo / Azul / Verde). Sólo las plataformas del **mismo color** que el jugador son sólidas para él. Las plataformas de otros colores son atravesables. Los jugadores están unidos por **sogas** físicas (Verlet). Objetivo: llegar a la **meta (Goal)**; el tiempo se cronometra y se guarda el mejor récord por nivel.

## Colores

`Utils/GameColor.cs`: enum `Red, Blue, Green`. Colores RGB:

| Color | RGB |
|-------|-----|
| Red   | (224, 64, 64) |
| Blue  | (64, 128, 224) |
| Green | (72, 184, 96) |

- Plataformas superpuestas de distinto color se dibujan mezcladas (amarillo, magenta, cian, blanco si las 3) — `Level.MixColors` / `DrawMixedPlatformIntersections`.
- El jugador colisiona sólo con `Level.GetCollidablePlatforms(playerColor)` (mismo color).

### Cambio de color

`InputManager` lee teclas configurables (default `J`=Red, `K`=Blue, `L`=Green; legado `R/B/G` en editor). El cambio se procesa en `Player.HandleColorChange`. Si al cambiar de color el jugador queda **dentro** de una plataforma ahora sólida, se dispara una **eyección**.

## Jugador (`Entities/Player.cs`)

Cuerpo AABB de 40x40. Física basada en acumulador de fuerzas + integración semi-implícita, resuelta por `PhysicsWorld`.

Parámetros notables (todos públicos, tuneables):

| Propiedad | Default | Qué hace |
|-----------|---------|----------|
| `GroundAcceleration` | 2600 | aceleración horizontal en suelo |
| `AirAcceleration` | 1050 | aceleración horizontal en aire |
| `GroundFriction` | 16 | drag en suelo |
| `AirDrag` | 0.35 | drag en aire |
| `MaxMoveSpeed` | 260 | velocidad objetivo de caminar |
| `MaxHorizontalVelocity` | 760 | clamp horizontal duro |
| `MaxVerticalVelocity` | 1150 | clamp vertical duro |
| `JumpImpulse` | 560 | impulso de salto (sólo si grounded) |
| `FastFallGravityMultiplier` | 1.5 | caída rápida al mantener tecla |
| `GravityScale` | 1 | escala de gravedad por jugador |

Constante global: `PhysicsWorld.Gravity = 1600`, `FixedTimeStep = 1/60`.

### Estados (`PlayerState`)

- `Normal`
- `Ejecting` — siendo expulsado de una plataforma de su color.

### Eyección (mecánica central)

Cuando el jugador se solapa con una plataforma sólida (por cambio de color o por moverse dentro), entra en `Ejecting` y es empujado hacia afuera:

- `Player.TryStartEjectionFromOverlaps` busca la mejor plataforma candidata (mayor influencia al centro + penetración).
- Dirección = del centro de la plataforma hacia el centro del jugador (con fallbacks: velocidad, última normal, o arriba).
- Fuerza con rampa suave (`GetEjectionRampAmount`) y multiplicador por cercanía al centro (`EjectionCenterForceMultiplier`).
- Al salir, hay una ventana de "launch control" (`LaunchControlSeconds = 0.24`) con control aéreo reducido.
- Parámetros: `EjectionAcceleration=4400`, `EjectionMaxSpeed=820`, `EjectionDuration=0.28`, `EjectionControlFactor=0.35`.
- Eventos: `OnEjectionStart`, `OnEjectionPeak`, `OnEjectionEnd`.

## Sogas / Rope (`Entities/Rope.cs`)

Soga física **Verlet** que conecta pares de jugadores consecutivos. Con N jugadores hay N-1 sogas (`PhysicsWorld` constructor).

- 24 nodos por defecto; extremos "pinned" a los jugadores (`SyncPinnedNodesToPlayers`).
- Integración Verlet + resolución de constraints (10 iteraciones) + colisión de nodos con plataformas.
- Transfiere impulsos a los jugadores en los extremos según tensión (`ApplyEndpointImpulses`).
- **Pull:** mantener tecla (default `Space`) tira de los nodos cercanos hacia el jugador (`ApplyPullForces`).
- Sólo simula si `IsHostControlled` (preparado para autoridad de host).

### Modos de soga (`RopeGameplayMode`)

| Modo | Comportamiento |
|------|----------------|
| `ColoredPhysics` (default) | la soga colisiona con plataformas de los colores presentes entre los jugadores; toma color mezclado |
| `Neutral` | la soga ignora plataformas y se queda beige |

Se elige en `LevelSelectScene` (selector que persiste en `static`), y se pasa a `GameScene` → `GameSession.RopeGameplayMode`.

## Launch Pads (`Entities/LaunchPad.cs`)

Plataformas de impulso con rotación. Al tocarlas, lanzan al jugador en la dirección de la pad.

- `LaunchPadForce = 980`, `LaunchPadCooldown = 0.22s` (cooldown por jugador en `PhysicsWorld`).
- Dirección = `(sin(rot), -cos(rot))` normalizada; rotación en grados, normalizada.
- Default 96x36. Conserva algo de velocidad lateral (clampeada).

## Checkpoints (`Entities/CheckpointFlag.cs`)

- Cada checkpoint tiene `Id` único (auto-asignado por `Level`).
- Al tocar uno, `PlayerManager.ActivateCheckpoint` lo marca activo y desactiva el anterior.
- `RespawnPosition` = checkpoint activo, o `Level.PlayerStart` si no hay.
- Respawn manual: tecla `R` (configurable). Procesado en `GameSimulation.HandleRespawnInputs` → `PlayerManager.RespawnPlayer` + reset de sogas.

## Meta y Timer (`Core/GameSimulation.cs`)

- El timer corre mientras `TimerRunning` (suma `FixedDeltaSeconds` por tick).
- Si **cualquier** jugador toca un `Goal` (`IsAnyPlayerTouchingGoal`), se completa el nivel:
  - `FinalTime` redondeado a centisegundos (`BestTimeStorage.RoundToCentiseconds`).
  - `BestTimeStorage.SaveIfRecord(levelId, finalTime)` → `NewRecord`.
  - Todos los jugadores se congelan (`Freeze`).
  - `GameSession.State = Completed`.
- `GameScene` muestra UI de "LEVEL COMPLETE" con tiempo y "NEW RECORD", con delay de 3s antes de poder volver.

## Input (`Managers/InputManager.cs`, `InputProfile`, `InputDevice`)

- Hasta **4 jugadores locales** (`MaxLocalPlayers = 4`).
- Perfiles activos = jugadores que se spawnean (`PlayerManager.SpawnLocalPlayers(input.ActiveProfiles)`).
- Por ahora el teclado controla **un** jugador a la vez (`KeyboardControlledPlayerId`); se puede cambiar clickeando un jugador en `GameScene` (`HandlePlayerSelectionClick`).
- `PlayerInputState` (lo que consume la simulación): `HorizontalMovement, JumpPressed, RespawnPressed, FastFallHeld, PullRopeHeld, RequestedColor?`.
- Teclas configurables vía `SettingsManager` (ver `06-GUIA-DESARROLLO.md`).

### HUD / Debug

- `F3` togglea debug draw: tick/rate, snapshots, input buffer, checkpoint activo, respawn pos, launch pads, info de sesión, **estado de Steam**, y por entidad: rol (LOCAL/REMOTE) y autoridad (HOST/REPLICA). Útil para verificar el modelo de red.
