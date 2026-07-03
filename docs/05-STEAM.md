# 05 — Integración con Steam

## Estado

| Feature | Estado |
|---------|--------|
| Init/shutdown Steamworks | ✅ `SteamManager` |
| Tolerancia a fallos (correr sin Steam) | ✅ |
| Callbacks por frame | ✅ `SteamCallbackManager` |
| Lobby friends-only (crear/unirse/salir) | ✅ `SteamLobbyService` |
| Invitaciones overlay + join desde amigos | ✅ |
| Roster de party en lobby data | ✅ `SteamPartyService` + `PartyRosterCodec` |
| Sync inicio de nivel (líder → todos) | ✅ `BroadcastLevelStart` |
| Rich presence (connect string) | ✅ Parcial (`#StatusInParty`) |
| Kick vía lobby chat | ✅ |
| **Steam Input API** | ✅ `SteamInputService` (DualShock 4 / DualSense / Xbox) |
| **Gameplay networking (sockets)** | 🟡 Ver [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md) |
| Achievements / stats | ❌ |
| Leaderboards globales | ❌ Ver [`08-ROADMAP.md`](08-ROADMAP.md) Fase 3 |
| Steam Workshop / UGC | ❌ Ver [`08-ROADMAP.md`](08-ROADMAP.md) Fase 4 |

## SteamManager — `Steam/SteamManager.cs`

`IDisposable`, instanciado en `ColorBlocksGame` (`_steam`), expuesto vía `game.Steam`.

| Miembro | Descripción |
|---------|-------------|
| `Initialize()` | `SteamAPI.Init()`; setea `IsInitialized`, refresca info de usuario |
| `RunCallbacks()` | `SteamAPI.RunCallbacks()` cada `Update` + refresca info |
| `Shutdown()` / `Dispose()` | `SteamAPI.Shutdown()` |
| `IsInitialized` | si Steam arrancó OK |
| `Username` | `SteamFriends.GetPersonaName()` |
| `SteamId` | `SteamUser.GetSteamID()` |
| `IsOverlayEnabled` | `SteamUtils.IsOverlayEnabled()` |
| `Status` | texto de estado legible (para debug HUD) |

### Tolerancia a fallos

`Initialize`/`RunCallbacks` atrapan excepciones recuperables (`DllNotFoundException`, `BadImageFormatException`, etc.) → el juego sigue con `IsInitialized = false`. Permite desarrollar sin cliente Steam abierto.

## Steam Lobby — `Steam/SteamLobbyService.cs`

Servicio principal de multijugador social (sin transporte de gameplay):

- Crear lobby friends-only, unirse, salir.
- Overlay de invitación (`ActivateGameOverlayInviteDialog`).
- Lobby data: nivel seleccionado, `RopeGameplayMode`, lava rise.
- `BroadcastLevelStart` / `LevelStartReceived` — líder inicia partida, clientes cargan nivel.
- Rich presence join (`game_connect` string).
- Eventos: `LobbyStateChanged`, `LobbyReady`, `MemberLeft`, `ErrorOccurred`.

## Steam Party — `Steam/SteamPartyService.cs`

- Serializa roster de `PartyManager` a lobby data (`SteamConstants.PartyRosterCodec`).
- Reconstruye miembros locales/remotos al actualizar lobby.
- Integrado en `PartyScene` vía `Party.BindSteamServices`.

## Ciclo de vida (en `ColorBlocksGame`)

```csharp
// Initialize
_steam.Initialize();
_steamCallbacks = new SteamCallbackManager(_steam);
_steamLobby = new SteamLobbyService(_steam, _steamCallbacks);
_steamParty = new SteamPartyService(_steamLobby);
Party.BindSteamServices(_steamLobby, _steamParty);

// Update (cada frame)
_steam.RunCallbacks();

// Dispose
_steam.Shutdown();
```

## Steam Input — `Steam/SteamInputService.cs`

Juego usa layout **Xbox** (`GamePad` de MonoGame). Steam Input traduce DualShock 4, DualSense, Xbox, etc. a ese layout cuando el cliente Steam está activo.

| Paso | Qué hace |
|------|----------|
| `SteamInput.Init(false)` | Tras `SteamAPI.Init()` |
| `SetInputActionManifestFilePath` | Carga `steam_input_manifest.vdf` del output |
| `RunFrame()` | Cada frame, **antes** de `InputManager.Update()` |
| `GetControllerType` / `GetControllerLabel` | F3 debug: tipo real del pad (PS4, PS5, Xbox…) |

Archivos copiados al build: `Steam/steam_input_manifest.vdf`, `Steam/controller_gamepad.vdf`.

### Steam Partner (requerido para release)

1. **Steam Input** → habilitar.
2. Controllers: Xbox One, Generic Gamepad, **PlayStation 4**, **PlayStation 5**.
3. Template: **Custom Configuration (Bundled with Game)** → ruta `steam_input_manifest.vdf`.
4. Publicar cambios en Steamworks.

### Sin Steam (dev / exe directo)

PS4/PS5 dependen de **SDL2** (MonoGame DesktopGL) + drivers Windows. USB suele ir mejor; Bluetooth variable. Para pruebas locales con pad Sony en Windows, lanzar vía Steam o usar DS4Windows.

## Configuración / archivos

- **`steam_appid.txt`** (raíz): App ID `4796400` (verificar ID de producción antes de release). Copiado al output.
- **`Steam/Native/Windows-x64/steam_api64.dll`**: copiada al output como `steam_api64.dll`.
- **`Steamworks.NET` 2024.8.0** vía NuGet.
- **`app.manifest`**: DPI awareness, referenciado en csproj.

## Pendientes para release en Steam

Ver checklist completo en [`08-ROADMAP.md`](08-ROADMAP.md). Resumen:

1. **Coop online** — Steam Networking Sockets + serialización de snapshots/inputs.
2. **Leaderboards globales** — `SteamUserStats` por nivel.
3. **Workshop** — `SteamUGC` para niveles de comunidad.
4. **Rich presence** — strings localizados en Steam Partner backend.
5. **Achievements / cloud saves** (opcional v1).
6. **SteamPipe** — depots, build release x64, quitar `steam_appid.txt` de build publicada.
