# 05 — Integración con Steam

## Estado

- **Implementado:** init/shutdown de Steamworks, persona name, SteamID, estado de overlay, callbacks por frame. Tolerante a fallos (corre sin Steam, ej. en dev sin cliente).
- **Falta:** todo lo de **multijugador online** (lobbies, networking sockets, invitaciones), achievements, stats/leaderboards, rich presence. Ver [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md).

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

`Initialize`/`RunCallbacks` atrapan excepciones "recuperables" (`DllNotFoundException`, `BadImageFormatException`, `EntryPointNotFoundException`, `SEHException`, `TypeInitializationException`) → el juego sigue corriendo con `IsInitialized = false` y status "Steam unavailable". Esto permite desarrollar sin el cliente de Steam abierto.

El estado de Steam se ve en el **debug HUD** (`F3`) dentro de `GameScene`.

## Ciclo de vida (en `ColorBlocksGame`)

```79:96:Core/ColorBlocksGame.cs
protected override void Initialize()
{
    _steam.Initialize();
    ...
}
```

- `Initialize()` → `_steam.Initialize()`.
- `Update()` → `_steam.RunCallbacks()` (primero, cada frame).
- `Dispose(disposing)` → `_steam.Shutdown()`.

## Configuración / archivos

- **`steam_appid.txt`** (raíz): contiene el App ID. Se copia al output (`CopyToOutputDirectory`). En dev suele ser `480` (Spacewar) hasta tener el App ID real de la tienda.
- **`Steam/Native/Windows-x64/steam_api64.dll`**: DLL nativa de Steamworks, copiada al output como `steam_api64.dll` (`CopyToOutputDirectory=Always`).
- **`Steamworks.NET` 2024.8.0** vía NuGet (`Color Blocks.csproj`).
- **`app.manifest`**: manifiesto de la app (DPI awareness, etc.), referenciado en el csproj.

## Pendientes para release en Steam

1. Reemplazar el App ID de dev por el **App ID real** en `steam_appid.txt`.
2. Implementar el **coop online** sobre Steam Networking + lobbies (ver doc 03).
3. (Opcional) Achievements, stats y leaderboards (encaja con `BestTimeStorage`).
4. (Opcional) Rich presence e invitaciones por overlay.
5. Empaquetado/depot via SteamPipe, build de release (`net9.0`, x64), incluir `steam_api64.dll`.
6. Quitar/ocultar el `steam_appid.txt` de dev en la build publicada (Steam lo provee en runtime real).
