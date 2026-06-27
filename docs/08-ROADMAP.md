# 08 — Roadmap hacia Steam

Visión del producto: lanzamiento en **Steam** con coop **local y online**, **highscores globales** por nivel, y **Steam Workshop** para niveles de la comunidad.

Este documento resume estado actual vs. pasos sugeridos. No es un compromiso de fechas.

---

## Estado actual (resumen)

| Área | Estado |
|------|--------|
| Gameplay local (1–4 jugadores) | ✅ Funcional |
| Editor de niveles | ✅ Funcional (local) |
| Mejores tiempos locales | ✅ `best_times.json` |
| UI / navegación gamepad+teclado+mouse | ✅ Grafo explícito, debug F8/F9 |
| Rebinding teclado + gamepad | ✅ Options; verificar persistencia gamepad en `SettingsManager` |
| Coop local (teclado + gamepads) | ✅ `PartyManager`, `PartyScene` |
| Steam init + callbacks | ✅ `SteamManager` |
| Steam lobby + invitaciones + roster | ✅ `SteamLobbyService`, `SteamPartyService` |
| Sincronizar inicio de nivel vía lobby | ✅ Líder elige nivel, todos cargan |
| Coop online (simulación sincronizada) | ❌ Sin transporte de red |
| Highscores globales (Steam Leaderboards) | ❌ No implementado |
| Steam Workshop (UGC) | ❌ No implementado |
| Achievements / cloud saves | ❌ No implementado |
| Empaquetado SteamPipe / depots | ❌ Fuera del repo |

---

## Fase 1 — Pulido pre-Steam (corto plazo)

Objetivo: experiencia sólida en single-player y coop local antes de abrir online.

1. **Fix persistencia gamepad bindings** — `SettingsManager` debe copiar/guardar `GamepadBindings` igual que `Keybindings`.
2. **Completar flags de nivel** — `LavaRise`, `Player1..4`, modos de soga: verificar gameplay cableado end-to-end.
3. **`LevelManager.RenameLevel`** — implementar o quitar del UI.
4. **Audio** — confirmar que `MusicVolume` de Options afecta al motor de audio.
5. **QA resoluciones** — Options responsive en 720p–3440×1440; sin overflow de UI.
6. **Niveles versionados** — mover niveles de fábrica al source tree / Content pipeline; evitar perderlos al `dotnet clean`.
7. **`.gitignore`** — excluir `bin/`, `obj/` del repo.
8. **Rich presence** — strings localizados en Steam Partner (`#StatusInParty`, en partida, en editor).

---

## Fase 2 — Coop online (bloqueador principal)

Hoy: lobby Steam sincroniza **quién está en el party** y **qué nivel jugar**, pero cada cliente corre su **propia simulación** (`GameSessionRole.LocalTest`). Input remoto = vacío.

### 2.1 Transporte

- Elegir: **Steam Networking Sockets** (`SteamNetworkingSockets` / `ISteamNetworkingMessages`) vía Steamworks.NET.
- Serialización binaria compacta de `InputFramePacket` y `GameSnapshotPacket` (evitar JSON en runtime).
- Host = autoridad; clientes predicen jugador local.

### 2.2 Sesión

- Implementar `GameSession.CreateHost` / `CreateClient`.
- Registrar peers reales con `OwnerId` y SteamID al unirse al lobby.
- `InputManager`: inyectar input remoto para `PartyMemberType.SteamRemote` (hoy devuelve `Empty`).

### 2.3 Loop de red

```
Host:  recibe InputFrame → NetworkInputBuffer → simula → broadcast GameSnapshot
Client: envía input local → predice → recibe snapshot → ApplySnapshot + reconciliación
```

Andamiaje ya existe en `Networking/` — ver [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md).

### 2.4 Gameplay online

- Spawn remoto: `PlayerManager.SpawnRemotePlayer` (existe, sin cablear).
- Interpolación de entidades remotas entre snapshots.
- Desconexión / reconexión / kick (parcial vía chat lobby).
- Validar con debug HUD F3 (rol, autoridad por entidad).

### 2.5 QA online

- 2–4 jugadores, distintas latencias, host migration (opcional, difícil — evaluar si necesario v1).

---

## Fase 3 — Highscores globales

Objetivo: ranking por nivel en Steam Leaderboards, además del récord local.

### 3.1 Steam Leaderboards API

- Crear leaderboards en Steam Partner: uno por nivel oficial (o leaderboard con metadata `level_id`).
- Score = tiempo en **centisegundos** (entero; menor = mejor) — alineado con `BestTimeStorage.RoundToCentiseconds`.
- `SteamUserStats.UploadLeaderboardScore` al completar nivel (solo si mejor que récord local o siempre según diseño).
- `DownloadLeaderboardEntries` para UI de ranking (top N + posición del jugador).

### 3.2 UX

- Level Select: mostrar récord local + mejor global (o top 3).
- Pantalla post-nivel: "Nuevo récord global #42".
- Modo offline: solo récord local.

### 3.3 Anti-cheat (mínimo viable)

- Host-authoritative en online (tiempo validado por host).
- En single: aceptar riesgo de cheats en leaderboards globales v1, o validación heurística (tiempo mínimo teórico por nivel).
- Opcional futuro: firmar tiempo con sesión online host-validated.

---

## Fase 4 — Steam Workshop

Objetivo: subir, descargar y jugar niveles creados por la comunidad.

### 4.1 Infra Steam

- Habilitar Workshop en App ID.
- `SteamUGC` API: `CreateItem`, `SubmitItemUpdate`, `SubscribeItem`, `DownloadItem`.
- Tags: dificultad, jugadores, soga, lava, etc.

### 4.2 Formato y empaquetado

- Paquete = `level.json` (mismo `LevelData`) + preview PNG + metadata (nombre, autor, versión).
- Versión de formato (`SteamConstants` o header en JSON) para migraciones futuras.
- Thumbnail: reutilizar `LevelPreviewManager`.

### 4.3 Directorios

```
Content/Levels/          → niveles oficiales (shipped)
Content/Workshop/        → niveles suscritos (por PublishedFileId)
```

- `LevelManager` escanea ambos; UI distingue oficial vs. Workshop.
- No mezclar UGC con niveles de fábrica en el mismo folder.

### 4.4 Flujo jugador

1. Editor → "Publicar en Workshop" (solo si nivel válido: meta, spawn, etc.).
2. Level Select → pestaña/filtro Workshop + botón "Explorar Workshop" (overlay Steam o in-game).
3. Al suscribirse: descargar → aparecer en lista → jugar con mismas reglas de timer/highscore.

### 4.5 Highscores en niveles Workshop

- Decidir: leaderboard separado por `PublishedFileId` o solo récord local.
- Steam permite leaderboards dinámicos o metadata — diseñar antes de implementar.

### 4.6 Moderación

- Reportar contenido vía Steam.
- Validación al cargar: rechazar JSON corrupto o niveles imposibles (sin meta).

---

## Fase 5 — Release Steam

1. **App ID producción** en `steam_appid.txt` (hoy `4796400` en repo).
2. **SteamPipe**: depots Windows x64, build automático CI → upload.
3. **Store page**: capturas, descripción coop local/online, Workshop, leaderboards.
4. **Achievements** (opcional v1): completar todos los niveles oficiales, tiempo bajo X, etc.
5. **Cloud saves** (opcional): `settings.json`, progreso — evaluar si récords deben ser solo leaderboards.
6. **Beta branch** en Steam para probar online + Workshop antes de public.
7. **Legal**: EULA, privacidad si hay analytics.

---

## Orden sugerido de implementación

```
[Fase 1 pulido] → [Fase 2 online] → [Fase 3 leaderboards] → [Fase 4 Workshop] → [Fase 5 release]
                      ↑
              bloqueador para coop online real
```

Workshop y leaderboards pueden paralelizarse parcialmente después de online estable.

Leaderboards **sin** online son posibles antes (solo single-player trusted) — útil para marketing pre-release.

---

## Referencias en código

| Tema | Archivos clave |
|------|----------------|
| Red / sesión | `Networking/GameSession.cs`, `Networking/Packets/`, `Networking/Replication/` |
| Steam lobby | `Steam/SteamLobbyService.cs`, `Steam/SteamPartyService.cs` |
| Party | `Party/PartyManager.cs`, `Scenes/PartyScene.cs` |
| Tiempos locales | `Managers/BestTimeStorage.cs`, `Core/GameSimulation.cs` |
| Niveles | `LevelSystem/LevelManager.cs`, `Scenes/EditorScene.cs` |
| UI | `docs/07-UI-NAVEGACION.md` |
