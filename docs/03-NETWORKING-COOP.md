# 03 — Networking / Coop Online

> **Estado actual:** coop **local** funcional. Lobby Steam funcional. **v1 online host-authoritative** implementado: `SteamGameNetworkService` + `GameNetworkCoordinator` + loop en `GameScene`. Cliente aplica snapshots (sin predicción aún). Host mergea input remoto vía `NetworkInputBuffer`.

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
4. En lobby: host usa `GameSession.CreateOnline(Host)`, clientes `CreateOnline(Client)`.
5. Host simula; clientes envían `InputFrame` y aplican `GameSnapshot`. Input remoto en host vía buffer de red (no `InputManager`).

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

## Qué FALTA para coop online v1 completo

1. ~~**Transporte de gameplay.**~~ Hecho: `SteamGameNetworkService` (`ISteamNetworkingMessages`, canales 0=input / 1=snapshot).
2. ~~**Serialización de paquetes.**~~ Hecho: `NetworkPacketCodec` + `PacketBuffer`.
3. ~~**Factories de sesión online.**~~ Hecho: `GameSession.CreateOnline` (Host/Client) en `GameScene`.
4. ~~**Loop host/client.**~~ Hecho en `GameScene` + `GameNetworkCoordinator` (cliente snapshot-only por ahora).
5. **Predicción cliente + reconciliación** — cliente no predice local aún.
6. **Interpolación** de entidades remotas entre snapshots.
7. **Spawning dinámico** mid-game (`SpawnRemotePlayer` sin cablear; roster pre-spawn sí funciona).
8. **Desconexión/reconexión** robusta (parcial: `MemberLeft` → `PartyScene`).

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
