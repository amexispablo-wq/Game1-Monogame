# 05 — Integración con Steam

## Estado

| Feature | Estado |
|---------|--------|
| Init/shutdown Steamworks | ✅ `SteamManager` |
| Tolerancia a fallos (correr sin Steam) | ✅ |
| Callbacks por frame | ✅ `SteamCallbackManager` |
| Lobby friends-only (crear/unirse/salir) | ✅ `SteamLobbyService` |
| Invitaciones overlay + join desde amigos | ✅ `SteamInviteManager` |
| Prompt in-game Accept/Decline (`LobbyInvite`) | ✅ `SteamInviteManager` + popup en `ColorBlocksGame` (oculto en `GameScene`) |
| Roster de party en lobby data | ✅ `SteamPartyService` + `PartyRosterCodec` |
| Sync inicio de nivel (líder → todos) | ✅ `BroadcastLevelStart` |
| Rich presence (connect string) | ✅ Parcial (`connect` + `#StatusInParty`; tokens Partner pendientes) |
| Kick vía lobby chat / Leave guest | ✅ Host Kick; guest Leave en `PartyScene` |
| **Steam Input API** | ✅ `SteamInputManager` (alias histórico `SteamInputService`) |
| **Gameplay networking** | 🟡 Ver [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md) |
| Leaderboards globales | ✅ `SteamLeaderboardService` + `LeaderboardScene` (QA/Partner boards) |
| Steam Workshop / UGC niveles | ✅ `SteamWorkshopService` + `LevelLibrary` Workshop source |
| Replay share / Ghost WR | ✅ `SteamReplayService` + `SteamGhostService` — ver [`10-REPLAY-Y-GHOSTS.md`](10-REPLAY-Y-GHOSTS.md) |
| Achievements / cloud saves | ❌ |
| SteamPipe / store ship | 🟡 Scripts en repo; proceso Partner pendiente — ver [`08-ROADMAP.md`](08-ROADMAP.md) |

## SteamManager — `Steam/SteamManager.cs`

`IDisposable`, instanciado en `ColorBlocksGame` (`_steam`), expuesto vía `game.Steam`.

| Miembro | Descripción |
|---------|-------------|
| `Initialize()` | `SteamAPI.Init()`; setea `IsInitialized`, refresca info de usuario |
| `RunCallbacks()` | `SteamAPI.RunCallbacks()` cada `Update` + refresca info |
| `Shutdown()` / `Dispose()` | `SteamAPI.Shutdown()` |
| `IsInitialized` | si Steam arrancó OK |
| `Username` / `SteamId` | persona + id |
| `IsOverlayEnabled` | overlay Steam |
| `Status` | texto debug |

### Tolerancia a fallos

Excepciones recuperables (`DllNotFoundException`, etc.) → juego sigue con `IsInitialized = false`.

## Steam Lobby — `Steam/SteamLobbyService.cs`

- Lobby friends-only: create/join/leave (leave-before-join + cancel create si hay join pendiente).
- Overlay invite diferido (`InviteFriends` → overlay al `LobbyReady`).
- Lobby data: nivel, `RopeGameplayMode`, lava rise, host-in-gameplay flag.
- `BroadcastLevelStart` / `LevelStartReceived`.
- Kick vía chat prefix; eventos `LobbyStateChanged`, `LobbyReady`, `MemberLeft`, `ErrorOccurred`.

## Steam Invites — `Steam/SteamInviteManager.cs`

Dueño único de invites + join externo:

- Overlay in-game (`OpenInviteOverlay`).
- Rich Presence: `connect` = `lobby:<id>`, `steam_display` = `#StatusInParty`, player group keys.
- **`LobbyInvite_t`** → pending invite; `ColorBlocksGame` muestra `Popup` Accept/Decline si **no** está en `GameScene` (durante nivel se encola).
- Friends **Invite to Game** Accept → `GameLobbyJoinRequested` → `AcceptLobbyInvite` (limpia pending).
- **Join Game** / launch `+connect_lobby` → `HandleJoinRequest`.
- Guest join → `OnSteamLobbyReadyForExternalJoin` → `PartyScene` si no está ya en Party/Game.

Tokens Partner: [`Steam/rich_presence_english.txt`](../Steam/rich_presence_english.txt).

## Steam Party — `Steam/SteamPartyService.cs`

- Roster ↔ lobby member data (`PartyRosterCodec`).
- `IsLeader` en roster = **primer slot local** del lobby owner (keyboard o gamepad).
- `Party.BindSteamServices` desde `ColorBlocksGame`.

## Leaderboards — `Steam/SteamLeaderboardService.cs`

- Boards Official + Workshop: `"{levelId}_p{playerCount}_f4"` (stable; version lives in details). Local never uploads.
- Score = seconds × 10000 (menor = mejor).
- Workshop uploads only if the level is in the **top 2480 by unique subscriptions** (Steam UGC client query + disk cache). See [`12-LEADERBOARD-ADMIN.md`](12-LEADERBOARD-ADMIN.md).
- Browse / WR peek: Find only (no FindOrCreate).
- Details + attach UGC replay (`ReplayUgcHandle` / ghost id) on **every** KeepBest PB (not WR-only).
- UI: `Scenes/LeaderboardScene.cs` — 1–4P boards, per-row **Replay** (download entry UGC), sticky local PB footer → Around You.
- `DownloadLocalEntry` — AroundUser(1) for sticky when Global top excludes the player.
- Missing Replay on a row = no UGC (`ShareReplayFile` failed and/or Attach only when `m_bScoreChanged==1`).
- Reset/delete of Steam boards: local tool [`Tools/LeaderboardMaintenance`](../Tools/LeaderboardMaintenance/) (publisher key on your PC; schedule or run manually). No 24/7 game backend.

## Workshop — `Steam/SteamWorkshopService.cs`

- Publish: solo niveles **Local** → UGC. Con `WorkshopId` válido → **Update** del mismo item; sin id → `CreateItem`.
- UI: overlay Uploading… con Cancel (soft-cancel; Steam no aborta SubmitItemUpdate ya enviado).
- Preview: PNG canónico regenerado al publicar; si el item Steam fue borrado, se limpia `WorkshopId` y se crea de nuevo.
- Tras Update: LB reset **no** es automático en el cliente — corre `LeaderboardMaintenance` (detecta `time_updated`).
- Sync subs → `%LocalAppData%/Color Blocks/Workshop/{id}/level.json` (lista `LevelLibrary` Workshop).
- Edición: Duplicate → Local (estilo Portal 2).

## Replay / Ghost Steam

Ver [`10-REPLAY-Y-GHOSTS.md`](10-REPLAY-Y-GHOSTS.md):

- `SteamReplayService` — share/download `.replay` vía Remote Storage UGC.
- `SteamGhostService` — cache WR desde top LB + replay UGC.

## Ciclo de vida (en `ColorBlocksGame`)

```csharp
// Initialize
_steam.Initialize();
_steamCallbacks.Register();
Party.BindSteamServices(_steamLobby, _steamParty);
_steamWorkshop.Initialize();
_steamWorkshop.SyncSubscribedItems();
_steamInvites.TryConsumeLaunchJoin(Environment.GetCommandLineArgs());

// Update (cada frame)
_steam.RunCallbacks();
_steamInput.RunFrame();
// … SyncPartyInvitePopup / UpdatePartyInvitePopup …

// Dispose
_steamInvites.ClearPresence();
_steam.Shutdown();
```

## Steam Input — `Steam/SteamInputManager.cs`

Layout gameplay = Xbox (`GamePad`). Steam Input traduce DS4/DualSense/Xbox/etc. Fallback `GamepadBackend`/XInput si no hay layout live.

| Paso | Qué hace |
|------|----------|
| `SetInputActionManifestFilePath` | `Steam/steam_input_manifest.vdf` antes de Init |
| `SteamInput.Init(true)` | Tras `SteamAPI.Init()` |
| `RunFrame()` | Cada frame, **antes** de `InputManager.Update()` |
| `IsSlotLive` | Solo reclama slot si acciones `bActive` |
| Glyphs | `SteamInputGlyphProvider` |

Archivos: `Steam/steam_input_manifest.vdf`, `Steam/controller_gamepad.vdf`.

### Steam Partner (plug-and-play)

Shipping VDFs en depot **no basta**. Checklist corto:

1. App Admin → Steam Input ON + familias de pads.
2. Template **Gamepad** (Valve) → **Save + Publish**.
3. Verificar cuenta limpia (sin Your Layouts): Recommended → Gamepad auto.
4. Official bundled (glyphs) = paso opcional posterior.

Detalle: [`STEAM_INPUT_OFFICIAL_SHIP.md`](STEAM_INPUT_OFFICIAL_SHIP.md).

## Configuración / archivos

- **`steam_appid.txt`** (raíz): App ID `4796400` (verificar producción).
- **`Steam/Native/Windows-x64/steam_api64.dll`** → output como `steam_api64.dll`.
- **Steamworks.NET** 2024.8.0 NuGet.
- **`app.manifest`**: DPI awareness.

## Pendientes para release

Ver [`08-ROADMAP.md`](08-ROADMAP.md):

1. Coop online — predicción + interpolación + QA 2-client.
2. Partner — boards LB, Workshop flags, Rich Presence tokens, Steam Input Publish.
3. SteamPipe — depots, quitar `steam_appid.txt` de build publicada.
4. Achievements / cloud (opcional v1).
