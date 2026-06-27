# 03 — Networking / Coop Online

> **Estado actual:** coop **local** funcional (hasta 4 jugadores). Lobby **Steam** funcional para party, invites y sincronizar inicio de nivel. La **simulación online sincronizada NO funciona** — no hay transporte de red; cada cliente corre su propia simulación.

## Qué funciona hoy

### Coop local — `Party/PartyManager.cs`

- Hasta **4 miembros**: teclado + gamepads en la misma máquina.
- Join/leave gamepad: Start para unirse, Back para salir.
- `PartyScene`: asignar input por miembro, kick, Play → Level Select.
- `GameScene`: spawnea jugadores desde el party vía `PlayerManager.SpawnFromParty`.

### Steam lobby (party online, sin gameplay net) — `Steam/`

| Servicio | Qué hace |
|----------|----------|
| `SteamLobbyService` | Crear lobby friends-only, join/leave, overlay invite, rich presence connect string, lobby data (nivel, rope mode, lava), roster sync, kick vía chat, `BroadcastLevelStart` |
| `SteamPartyService` | Serializar/deserializar roster (`PartyRosterCodec`), publicar slots locales, reconstruir party desde lobby |

Flujo:

1. `PartyScene` crea/une lobby al entrar.
2. Líder elige nivel en `LevelSelectScene` → `BroadcastLevelStart`.
3. No-líderes reciben `LevelStartReceived` y cargan el mismo nivel.
4. Cada cliente corre **`GameSession.CreateLocalTest`** — simulación independiente.
5. Miembros remotos (`PartyMemberType.SteamRemote`) tienen input **vacío** (`PlayerInputState.Empty`).

Ver también [`05-STEAM.md`](05-STEAM.md).

## Qué YA existe (andamiaje de simulación en red)

### Sesión — `Networking/GameSession.cs`

- Roles: `LocalTest`, `Host`, `Client`.
- Estados: `MainMenu, SteamLobby, Party, LevelSelect, Loading, Playing, Completed, Disconnected`.
- `IsHost` = `LocalTest || Host`. La autoridad de simulación vive en el host.
- `Peers` (`SessionPeer`: OwnerId, DisplayName, ExternalUserId) y `Players` (`PlayerSessionInfo`).
- `GameSessionSettings`: `SimulationTicksPerSecond=60`, `MaxPlayers=4`, `HostAuthoritativeRopes=true`.
- Único factory implementado hoy: `CreateLocalTest(levelId, ropeMode)`. **Faltan** `CreateHost` / `CreateClient`.
- `NetworkIdAllocator` asigna ids de red; `RegisterPlayer` los reserva.

### Ownership — `NetworkEntityOwnership` / `INetworkEntity`

- Cada `Player`/`Rope` tiene `NetworkId, OwnerId, IsLocal, IsHostControlled`.
- `PhysicsWorld.ShouldSimulate = IsLocal || IsHostControlled`:
  - **Host:** simula todo (todo es host-controlled).
  - **Cliente (futuro):** simula sólo lo local (predicción) y aplica snapshots para el resto.
- `Rope.Simulate` ya hace early-return si `!IsHostControlled` → las sogas son autoritativas del host.

### Predicción / Input — `Networking/Prediction/`

- `TickRate` (Hz → `FixedDeltaSeconds`), `SimulationTick` (contador de ticks).
- `PlayerInputState` (input por jugador) y `InputFrame` (tick + ownerId + lista de inputs por `NetworkId`).
- `NetworkInputBuffer`: guarda inputs por tick (`StoreFrame`), los lee (`GetInputs(tick)`), y poda viejos (`TrimBefore`, retención 180 ticks ≈ 3s). Cuenta `DroppedFrameCount`.
- `ILocalPlayerInputSource`: abstracción de la fuente de input local (la implementa `InputManager`). Esto permite, en cliente, inyectar inputs remotos.

### Replicación / Snapshots — `Networking/Replication/`

- `GameSnapshot`: `Tick, Sequence, Level, Timer, RopeMode, Players[], Ropes[]`.
- `PlayerSnapshot`: id/owner/index/playerId + pos/vel/accel (`NetworkVector2`) + color + state + grounded/frozen.
- `RopeSnapshot`: ids, endpoints, modo, tensión, pull, y **posiciones de todos los nodos**.
- `LevelSnapshot` / `TimerSnapshot`: geometría del nivel y estado del cronómetro.
- `GameSimulation.CreateSnapshot()` se genera **cada tick**; `ApplySnapshot()` aplica uno entrante (ya implementado: copia a players/ropes/timer).
- `NetworkVector2`: serialización de `Vector2`.

### Paquetes — `Networking/Packets/`

- `INetworkPacket` (`PacketType`, `Tick`). Tipos: `InputFrame, GameSnapshot, SessionState`.
- `InputFramePacket(InputFrame)` y `GameSnapshotPacket(GameSnapshot)`.
- **No hay serializador binario** (a bytes) ni deserializador implementado todavía.

## Qué FALTA para coop online funcional (simulación sincronizada)

1. **Transporte de gameplay.** Steam Networking Sockets (`SteamNetworkingSockets` / `SteamNetworkingMessages`) vía Steamworks.NET. El lobby ya existe; falta enviar/recibir paquetes de juego.
2. **Serialización de paquetes.** Escribir/leer `InputFrame`, `GameSnapshot`, `SessionState` a/desde bytes (binario compacto).
3. **Factories de sesión.** `GameSession.CreateHost(...)` y `CreateClient(...)`, registro de peers con `OwnerId` y SteamId.
4. **Input remoto.** `InputManager` debe leer inputs de peers para `PartyMemberType.SteamRemote` (hoy stub).
5. **Loop de host:** recibir `InputFramePacket` → `NetworkInputBuffer.StoreFrame` → simular → broadcast `GameSnapshotPacket`.
6. **Loop de cliente:** enviar input local → predecir → recibir snapshots → `ApplySnapshot` + reconciliación.
7. **Spawning remoto.** `PlayerManager.SpawnRemotePlayer` existe; falta cablear al unirse un peer.
8. **Interpolación** de entidades remotas entre snapshots.
9. **Desconexión/reconexión** robusta (hoy: miembro sale → volver a `PartyScene` tras delay).

> Nota: items de lobby/matchmaking/invites del listado anterior **ya están implementados**. Ver [`08-ROADMAP.md`](08-ROADMAP.md) Fase 2.

## Diseño objetivo (host-authoritative + client prediction)

```
Cliente                              Host (autoridad)
───────                              ────────────────
captura input local (tick T)
  └─ predice local (simula T)
  └─ envía InputFramePacket(T) ───▶  guarda en NetworkInputBuffer
                                     simula tick T con TODOS los inputs
                                     genera GameSnapshot(T)
recibe GameSnapshot(T') ◀────────── broadcast GameSnapshotPacket(T')
  └─ ApplySnapshot(remotos)
  └─ reconciliación de local:
       reaplica inputs T'..ahora
```

- **Autoridad:** host simula y manda snapshots. Sogas y entidades no-locales son réplicas en el cliente.
- **Predicción:** el cliente simula su propio jugador sin esperar al host y corrige al recibir el snapshot.
- **Buffer de input** (180 ticks) ya soporta la re-simulación de reconciliación.

## Punto de entrada para implementar

- Hoy `GameScene` llama `GameSession.CreateLocalTest`. Para online, habría que:
  - Crear escenas/flujo de lobby (estados `SteamLobby`/`Party` ya reservados).
  - Inyectar un `ILocalPlayerInputSource` que combine input local + inputs remotos recibidos.
  - Conectar `GameSimulation.Advance` (host) y `GameSimulation.ApplySnapshot` (cliente) al transporte.
- El debug HUD (`F3`) ya muestra rol y autoridad por entidad → usarlo para validar la sincronización.
