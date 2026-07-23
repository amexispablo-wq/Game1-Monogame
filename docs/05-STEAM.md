# 05 — Integración con Steam

## Estado

| Feature | Estado |
|---------|--------|
| Init/shutdown Steamworks | ✅ `SteamManager` |
| Tolerancia a fallos (correr sin Steam) | ✅ |
| Callbacks por frame | ✅ `SteamCallbackManager` |
| Lobby friends-only (crear/unirse/salir) | ✅ `SteamLobbyService` |
| Invitaciones overlay + join desde amigos | ✅ `SteamInviteManager` |
| Roster de party en lobby data | ✅ `SteamPartyService` + `PartyRosterCodec` |
| Sync inicio de nivel (líder → todos) | ✅ `BroadcastLevelStart` |
| Rich presence (connect string) | ✅ Parcial (`connect` + `#StatusInParty`; tokens Partner pendientes) |
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

- Crear lobby friends-only, unirse, salir (leave-before-join + cancel create en vuelo si hay join pendiente).
- Overlay de invitación diferido (`InviteFriends` abre overlay al `LobbyReady` si aún no había lobby).
- Lobby data: nivel seleccionado, `RopeGameplayMode`, lava rise.
- `BroadcastLevelStart` / `LevelStartReceived` — líder inicia partida, clientes cargan nivel.
- Eventos: `LobbyStateChanged`, `LobbyReady`, `MemberLeft`, `ErrorOccurred`.
- `LobbyInvite_t` solo se loguea; el join ocurre al Accept (`GameLobbyJoinRequested`).

## Steam Invites — `Steam/SteamInviteManager.cs`

Dueño único de invites + join externo (escenas llaman vía `ColorBlocksGame.SteamInvites`):

- Overlay in-game (`OpenInviteOverlay` → `InviteFriends`).
- Rich Presence: key `connect` = `lobby:<id>`, `steam_display` = `#StatusInParty`, player group keys.
- Friends panel **Invite to Game** → `GameLobbyJoinRequested` → `AcceptLobbyInvite`.
- Friends panel **Join Game** → `GameRichPresenceJoinRequested` / launch `+connect_lobby`.
- Tras join como no-owner fuera de Party/Game, `ColorBlocksGame` navega a `PartyScene`.

Archivo local de tokens: [`Steam/rich_presence_english.txt`](../Steam/rich_presence_english.txt). Hay que subir los tokens al backend de **Steam Partner** para que el friends list muestre “In Party” / Join Game limpio; el juego ya publica `connect` en runtime.

## Steam Party — `Steam/SteamPartyService.cs`

- Serializa roster de `PartyManager` a lobby data (`SteamConstants.PartyRosterCodec`).
- Reconstruye miembros locales/remotos al actualizar lobby.
- Integrado en `PartyScene` vía `Party.BindSteamServices`.

## Ciclo de vida (en `ColorBlocksGame`)

```csharp
// Initialize
_steam.Initialize();
_steamCallbacks.Register();
Party.BindSteamServices(_steamLobby, _steamParty);
_steamInvites.TryConsumeLaunchJoin(Environment.GetCommandLineArgs());

// Update (cada frame)
_steam.RunCallbacks();

// Dispose
_steam.Shutdown();
```

## Steam Input — `Steam/SteamInputManager.cs`

Juego usa layout **Xbox** (`GamePad` de MonoGame). Steam Input traduce DualShock 4, DualSense, Xbox, etc. a ese layout cuando el cliente Steam está activo. Si Steam Input no tiene layout live, el juego cae a `GamepadBackend`/XInput automáticamente.

| Paso | Qué hace |
|------|----------|
| `SetInputActionManifestFilePath` | Carga `Steam/steam_input_manifest.vdf` (fallback: raíz del exe) **antes** de Init |
| `SteamInput.Init(true)` | Tras `SteamAPI.Init()`, RunFrame explícito |
| `RunFrame()` | Cada frame, **antes** de `InputManager.Update()` |
| `IsSlotLive` | Solo reclama el slot si acciones reportan `bActive` |
| `GetControllerType` / `GetControllerLabel` | F3 debug: tipo real del pad (PS4, PS5, Xbox…) |

Archivos copiados al build: `Steam/steam_input_manifest.vdf`, `Steam/controller_gamepad.vdf`.

### Steam Partner (requerido para release)

Shipping `Steam/*.vdf` in the depot is **not enough**. Without Partner registration, many players soft-claim (handle present, Jump/Move `bActive=0`) and fall back to XInput — Xbox often works, DualShock/DualSense often do not.

**Checklist (do this in Steamworks Partner, then Publish):**

1. App Admin → **Steam Input** → enable.
2. Enable controller families you support: Xbox, Generic, PS4, PS5, Switch Pro, Steam Deck.
3. Template: **Custom Configuration (Bundled with game)**.
4. Path (relative to game install root): `Steam/steam_input_manifest.vdf`.
5. **Publish** Partner changes (depot upload alone does not register official layout).
6. Verify on a clean tester account: Steam → Color Blocks → Controller → **Official** / “Mando oficial”.
7. In-game F3: slot `live=yes`, not stuck on `SOFT CLAIM`. Options → Controls shows Steam note; use **Steam Controller Configuration** if soft-claim.

### Official Recommended vs Your Layouts (critical)

Steam shows two different configs:

| Tab | Who gets it | Notes |
|-----|-------------|--------|
| **RECOMMENDED → Official Gamepad** | New players (default) | Comes from depot `Steam/controller_gamepad.vdf` (must be revision **5+** with stick `analog` → Move) |
| **YOUR LAYOUTS** | Only accounts that edited/saved a layout | Overrides Official. Dev “Molleja” layout works for you only — friends never get it |

**Why friends pad dead while yours works:** you use Your Layouts; they use empty/old Official.

**Ship checklist (every Input fix):**

1. Confirm repo `Steam/controller_gamepad.vdf` has `"revision" "5"` (or higher) and Left Stick / D-Pad use `"analog"` (not `"edge"` / `"click"` for Move).
2. `publish.bat` → ContentBuilder `\content\Steam\*.vdf` → `steamcmd run_app_build` → Steamworks **Set Live**.
3. Partner → Steam Input → Bundled path `Steam\steam_input_manifest.vdf` → **Save + Publish**.
4. Verify on **clean account** (no Your Layouts) or delete your personal Color Blocks layout first.
5. Friends: Update → Controller → **Recommended → Official Gamepad** (apply). Keep Steam Input Enabled.

**Friend pad dead, other Steam games OK (temporary):** Properties → Controller → Steam Input = **Disabled**. Prefer fixing Official via checklist above.

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
4. **Rich presence** — subir tokens localizados (`#StatusInParty`, etc.) desde `Steam/rich_presence_english.txt` al backend Steam Partner (código ya publica `connect` + `steam_display`).
5. **Achievements / cloud saves** (opcional v1).
6. **SteamPipe** — depots, build release x64, quitar `steam_appid.txt` de build publicada.
