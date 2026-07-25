# 03 — Steam core

## Piezas mínimas

| Pieza | Rol |
|-------|-----|
| `SteamManager` | `SteamAPI.Init` / `RunCallbacks` / `Shutdown` |
| `SteamCallbackManager` | `Callback<T>.Create` → eventos C# |
| `steam_appid.txt` | App ID en raíz output (dev); **no** en build SteamPipe final |
| `steam_api64.dll` | Native, copiada al output |
| NuGet `Steamworks.NET` | Bindings |

## Fail-soft

```
try Init → IsInitialized
catch DllNotFound / BadImage → juego sigue offline
Update: if initialized → RunCallbacks
```

Dev sin Steam cliente = OK. Features Steam no-op o mensaje UI.

## Dueño único

- Un sitio crea managers (clase `Game`).
- Escenas llaman APIs de alto nivel (`game.SteamLobby.InviteFriends`), no `SteamMatchmaking.*` directo.
- Un manager por dominio: Lobby, Invites, Input, Leaderboards, Workshop.

## Rich Presence (mínimo party/join)

- Key `connect` con string parseable (`lobby:<id>` o custom).
- `steam_display` token (`#StatusInParty`) → **subir tokens a Partner**.
- Player group keys si querés cluster en friends list.
- Clear presence al salir de lobby.

## Ciclo de vida

```
Initialize → Init + Register callbacks + (opcional) consume launch args
Update     → RunCallbacks cada frame (antes o con input)
Dispose    → ClearPresence + Shutdown
```

## Checklist

- [ ] Corre con y sin Steam
- [ ] `steam_appid.txt` correcto en dev
- [ ] Callbacks no se registran dos veces
- [ ] Shutdown limpio al cerrar
- [ ] Tokens presence en Partner si usás `steam_display`

**Referencia CB:** [`../05-STEAM.md`](../05-STEAM.md), `Steam/SteamManager.cs`, `SteamCallbackManager.cs`.
