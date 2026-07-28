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
- Plataformas pueden hacer ping-pong en un solo eje (V u H) con dirección Up/Down o Left/Right (`Platform.ApplyMotionAtTime`); colisiones y mezcla usan `Bounds` vivos. Jugadores grounded y nodos de rope se llevan con el delta (`PhysicsWorld.UpdateMovingPlatformsAndCarry`).
- Plataformas pueden ciclar color en loop con lista ordenada + `ColorChangePeriodSeconds` por paso (`Platform.ApplyColorAtTime`), mismo reloj que motion. Independiente del movimiento. `White` permitido (sólido para todos; rope no colisiona con White). Si el color vivo deja de coincidir, el jugador cae; si pasa a colisionable mientras hay overlap, eyección vía `TryStartEjectionFromOverlaps`. Últimos 3s de cada paso: borde parpadea progresivo (más rápido/fuerte) en el **próximo** color del ciclo.

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

- `Player.TryStartEjectionFromOverlaps` agrupa plataformas colisionables conectadas (overlap transitivo, incl. anidadas) en un cluster; scorea por influencia al centro + penetración sobre el **AABB unión**.
- Dirección plataformas = radial desde el centro de la unión (`normalize(playerCenter - unionCenter)`); se **recalcula cada tick** (offset L/R y cruzar centro vertical cambian la fuerza). Centro exacto → MTV cardinal. Player–player: MTV cardinal **lockeado** al inicio.
- Colisión ignora **todas** las plataformas del cluster hasta salir de la entidad falsa.
- Fuerza con rampa suave (`GetEjectionRampAmount`) y multiplicador por cercanía al centro (`EjectionCenterForceMultiplier`).
- Al salir, hay una ventana de "launch control" (`LaunchControlSeconds = 0.24`) con control aéreo reducido.
- Parámetros: `EjectionAcceleration=4400`, `EjectionMaxSpeed=820`, `EjectionDuration=0.28`, `EjectionControlFactor=0.35`.
- Eventos: `OnEjectionStart`, `OnEjectionPeak`, `OnEjectionEnd`.
- Salto: `JumpImpulse` fija `Velocity.Y` (no suma sobre caída); buffer ~0.1s para aterrizaje.

## Sogas / Rope (`Entities/Rope.cs`)

Soga **Verlet** entre pares consecutivos de jugadores. Con N jugadores hay N−1 sogas (`PhysicsWorld` constructor).

### Modelo actual (2026)

- **Nodos:** default 24 (`GameplayTuning.NodeCount`); mínimo **40** en `ColoredPhysics` (`Rope.ColoredPhysicsMinNodeCount`).
- **Integración:** Verlet + gravedad + damping; extremos acoplados por tangente del rope (no cuerda recta).
- **Constraints** (`RopeConstraint`): **solo resisten stretch**; permiten sag/compresión suave. Solver configurable (`SolverIterations`, default 8).
- **Rest length:** `_targetRestLength` acotado por `MinimumRopeLength` / `MaximumRopeLength`; path total no puede superar rest length (`EnforceMaximumPathLength`).
- **Colisión colored:** nodos y segmentos vs plataformas cuyo color coincide con flags del rope; snap + eject + routing en esquinas.
- **Pull:** acorta `_targetRestLength` (no arrastra nodos hacia el jugador); tensión transfiere fuerza al otro extremo.
- **Draw:** color puro sin tint blanco al estirar; grosor sube levemente con `FeedbackIntensity`.
- Solo simula si `IsHostControlled` (autoridad host en online).

Parámetros tuneables vía `GameplayTuning` + panel F6 (dev). Ver [`09-HERRAMIENTAS-DEV.md`](09-HERRAMIENTAS-DEV.md).

### Modos de soga (`RopeGameplayMode`)

| Modo | Comportamiento |
|------|----------------|
| `ColoredPhysics` (default en play) | colisión con plataformas según colores de **los dos extremos** del segmento; color visual = mezcla de esos dos PJ |
| `Neutral` | sin colisión con plataformas; color accent (`ColorType.Rope`); usado en Rope Sandbox |

Se elige en `LevelSelectScene` (campo `static` entre transiciones) → `GameScene` / `GameSession.RopeGameplayMode`.

### Color de la soga

- Flags de colisión: OR de `StartPlayer.PlayerColor` y `EndPlayer.PlayerColor` (ej. rojo + verde → colisiona rojo **y** verde).
- Color dibujado: `ColorPaletteManager.MixFlags` — mismas reglas que plataformas superpuestas (R+G = amarillo, R+B = magenta, etc.).
- Se recalcula cada tick en `RefreshGameplayModeState()` cuando un PJ cambia de color.

## Launch Pads (`Entities/LaunchPad.cs`)

Plataformas de impulso con rotación. Al tocarlas, lanzan al jugador en la dirección de la pad.

- `LaunchPadForce = 980` default por instancia (`LaunchForce`); `LaunchPadCooldown = 0.22s` (cooldown por jugador en `PhysicsWorld`).
- Dirección = `(sin(rot), -cos(rot))` normalizada; rotación en grados, normalizada.
- Default 96x36. Conserva algo de velocidad lateral (clampeada). Force por pad clampeado ~100–3000.

## Power-Ups (`Entities/PowerUp.cs`)

Pickups temporales. Overlap AABB con el jugador → buff instantáneo (sin inventario).

- Tipos v1: `Speed` (multiplica `MaxMoveSpeed` + `GroundAcceleration` + `AirAcceleration`) y `Jump` (multiplica `JumpImpulse`).
- Props por instancia: `Bounds`, `Type`, `DurationSeconds` (default 5), `Multiplier` (1–3, default 1.5), `RespawnSeconds` (0 = one-shot hasta reload), `Consumable` (default true; false = no se consume / queda disponible).
- Stacking: un buff activo **por tipo** por jugador; re-pickup refresca timer + reemplaza multiplier. Speed y Jump coexisten.
- `GameplayTuning.ApplyTo` corre cada tick; buffs se aplican **después** (y `MaxMoveSpeed` también viene del tuning). Al expirar, el próximo apply restaura base.
- Collect en `PhysicsWorld` (host/sim); estado de buff en `PlayerSnapshot`; disponibilidad/respawn en `LevelSnapshot` / paquete de snapshot (índice).

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

## Input (`Managers/InputManager.cs`, `PartyManager`)

- Hasta **4 jugadores locales** (`MaxLocalPlayers = 4`).
- Roster en `PartyManager`; cada `PartyMember` tiene `InputSource` (Keyboard / Gamepad / SteamRemote).
- Al spawnear: `PlayerManager.SpawnFromParty(members, input)` registra bindings `networkId → PartyMember` en `InputManager`.
- `GameplayInputBlocked`: cuando true (pause, muerte, level complete), no se leen inputs de gameplay. Se resetea en `ClearGameplayBindings()` al cambiar de escena.
- `PlayerInputState`: `HorizontalMovement, JumpPressed, RespawnPressed, FastFallHeld, PullRopeHeld, RequestedColor?`.
- Teclas configurables vía `SettingsManager` (ver `06-GUIA-DESARROLLO.md`).

### HUD / Debug

- `F3` togglea debug draw: tick/rate, snapshots, input buffer, checkpoint activo, respawn pos, launch pads, info de sesión, **estado de Steam**, y por entidad: rol (LOCAL/REMOTE) y autoridad (HOST/REPLICA). Útil para verificar el modelo de red.
