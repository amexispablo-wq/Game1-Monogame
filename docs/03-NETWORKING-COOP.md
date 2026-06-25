# 03 — Networking / Coop Online

> **Estado actual:** el coop online está **diseñado y andamiado** pero **NO funcional end-to-end**. Existen sesiones, ownership, snapshots, input frames, predicción y paquetes, pero **no hay capa de transporte** (ni sockets, ni Steam Networking, ni lobby online). Hoy sólo corre `GameSessionRole.LocalTest` (un proceso, multijugador local).

## Qué YA existe (andamiaje)

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

## Qué FALTA para coop online funcional

1. **Transporte.** Implementar capa de red. Opción natural dado el stack: **Steam Networking** (`SteamNetworkingSockets` / `SteamNetworkingMessages`) vía Steamworks.NET. Alternativa: UDP propio (LiteNetLib, etc.).
2. **Lobby / matchmaking.** `SteamMatchmaking` (crear/unirse a lobby), invitaciones por overlay, join desde amigos. Estados `SteamLobby`/`Party` ya están previstos en el enum.
3. **Serialización de paquetes.** Escribir/leer `InputFrame`, `GameSnapshot`, `SessionState` a/desde bytes (binario compacto; evitar JSON por tamaño/latencia).
4. **Factories de sesión.** `GameSession.CreateHost(...)` y `CreateClient(...)`, registro de peers reales con sus `OwnerId` y `SteamId`.
5. **Loop de host:** recibir `InputFramePacket` de clientes → `NetworkInputBuffer.StoreFrame` → simular → broadcast `GameSnapshotPacket`.
6. **Loop de cliente:** enviar input local cada tick → predecir local → recibir snapshots → `ApplySnapshot` + reconciliación (re-simular desde el último tick confirmado usando el input buffer).
7. **Spawning remoto.** `PlayerManager.SpawnRemotePlayer` ya existe; falta dispararlo cuando un peer se une.
8. **Interpolación/extrapolación** de entidades remotas para suavizar el render entre snapshots.
9. **Manejo de desconexión** (estado `Disconnected`) y reconexión.

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
