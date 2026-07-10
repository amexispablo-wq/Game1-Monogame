# 08 — Roadmap hacia Steam

Visión del producto: lanzamiento en **Steam** con coop **local y online**, **highscores globales** por nivel, y **Steam Workshop** para niveles de la comunidad.

Este documento resume estado actual vs. pasos sugeridos. No es un compromiso de fechas.

---

## Estado actual (resumen)

| Área | Estado |
|------|--------|
| Gameplay local (1–4 jugadores) | ✅ Funcional |
| Física de soga (rewrite Verlet 2026) | ✅ Funcional; regresiones en benchmark |
| Dev: Rope Sandbox + tuning F6 + benchmarks | ✅ Funcional (requiere `developerMode`) |
| Editor de niveles | ✅ Funcional (local) |
| Mejores tiempos locales | ✅ `best_times.json` |
| UI / navegación gamepad+teclado+mouse | ✅ Grafo explícito, debug F8/F9 |
| Rebinding teclado + gamepad | ✅ Options; verificar persistencia gamepad en `SettingsManager` |
| Coop local (teclado + gamepads) | ✅ `PartyManager`, `PartyScene` |
| Steam init + callbacks | ✅ `SteamManager` |
| Steam lobby + invitaciones + roster | ✅ `SteamLobbyService`, `SteamPartyService` |
| Sincronizar inicio de nivel vía lobby | ✅ Líder elige nivel, todos cargan |
| Coop online (simulación sincronizada) | 🟡 v1 host-authoritative; falta predicción + QA |
| Highscores globales (Steam Leaderboards) | ❌ No implementado |
| Steam Workshop (UGC) | ❌ No implementado |
| Achievements / cloud saves | ❌ No implementado |
| Empaquetado SteamPipe / depots | ❌ Fuera del repo |

---

## Fase 0 — Rope + QA dev (ahora)

Objetivo: soga estable en niveles reales antes de más features Steam.

| Item | Estado |
|------|--------|
| Rewrite Verlet + stretch-only constraints | ✅ |
| Pull → acortar rest length + tensión al otro PJ | ✅ |
| Colored collision + color mix extremos (R+G=amarillo) | ✅ |
| Fix input sandbox (`GameplayInputBlocked`) | ✅ |
| Benchmark suite rope (14 mecánicas × 2 modos) | ✅ |
| Feel tuning en niveles diseñados (no solo sandbox) | 🟡 Manual QA pendiente |
| Sandbox con selector `ColoredPhysics` | ❌ Opcional |
| Fuzz testing estable en CI | ❌ Opcional |

**Comando regresión:** `dotnet run -- --benchmark rope` (ver [`09-HERRAMIENTAS-DEV.md`](09-HERRAMIENTAS-DEV.md)).

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

## Fase 2 — Coop online (en progreso)

**v1 scaffold listo.** Lobby + transporte + loop host-authoritative. Falta QA 2-client, predicción, interpolación.

### 2.1 Transporte — hecho

- `SteamGameNetworkService` — `ISteamNetworkingMessages`, canales 0/1.
- `NetworkPacketCodec` — binario `InputFrame` + `GameSnapshot`.

### 2.2 Sesión — parcial

- `GameSession.CreateOnline` en lobby (Host/Client).
- `OwnerId` + `NetworkPlayerId` vía roster + `AssignNetworkPlayerIds`.
- Input remoto en **host** vía `GameNetworkCoordinator` → `NetworkInputBuffer` (no `InputManager`).

### 2.3 Loop de red — hecho (v1)

```
Host:  PumpIncoming → Advance → BroadcastSnapshot
Client: SendLocalInput → TryConsumeClientSnapshot → ApplySnapshot
```

### 2.4 Gameplay online — pendiente

- Spawn roster: `SpawnFromParty` (OK). `SpawnRemotePlayer` mid-game: sin cablear.
- Interpolación entre snapshots.
- Desconexión parcial (`MemberLeft` → PartyScene).
- F3 debug: rol NET + snapshot seq.

### 2.5 QA online — pendiente

- 2 Steam clients, latencia, host migration (opcional v1).

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
[Fase 0 rope QA] → [Fase 1 pulido] → [Fase 2 online QA] → [Fase 3 leaderboards] → [Fase 4 Workshop] → [Fase 5 release]
        ↑                    ↑
   ahora (feel + niveles)   deuda settings/lava/gitignore
```

### Qué hacer ahora (prioridad)

1. **QA manual rope** — jugar 2–3 niveles oficiales en `ColoredPhysics`: pull, esquinas, cambio de color mid-air, 3–4 jugadores.
2. **Benchmark en cada cambio de rope** — `--benchmark rope`; no mergear si FAIL.
3. **Fase 1 rápida** — gamepad bindings persist, `.gitignore` bin/obj, niveles versionados en source tree.
4. **Fase 2 QA** — 2 Steam clients, latencia 50–150 ms, verificar snapshots + desconexión.
5. Después: leaderboards (Fase 3) o Workshop (Fase 4) según prioridad de marketing.

Workshop y leaderboards pueden paralelizarse parcialmente después de online estable.

Leaderboards **sin** online son posibles antes (solo single-player trusted) — útil para marketing pre-release.

---

## Referencias en código (ampliado)

| Tema | Archivos clave |
|------|----------------|
| Rope / física | `Entities/Rope.cs`, `Entities/RopeConstraint.cs`, `Managers/PhysicsWorld.cs`, `Gameplay/GameplayTuning.cs` |
| Dev / benchmarks | `Developer/GameplayBenchmark/`, `Scenes/RopeSandboxScene.cs`, `docs/09-HERRAMIENTAS-DEV.md` |
| Red / sesión | `Networking/GameSession.cs`, `Networking/Packets/`, `Networking/Replication/` |
| Steam lobby | `Steam/SteamLobbyService.cs`, `Steam/SteamPartyService.cs` |
| Party | `Party/PartyManager.cs`, `Scenes/PartyScene.cs` |
| Tiempos locales | `Managers/BestTimeStorage.cs`, `Core/GameSimulation.cs` |
| Niveles | `LevelSystem/LevelManager.cs`, `Scenes/EditorScene.cs` |
| UI | `docs/07-UI-NAVEGACION.md` |
