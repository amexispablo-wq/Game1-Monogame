# 10 — Steam

Guía de integración **genérica**. No asume un género.

## Principios

1. **Fail-soft:** el juego corre sin cliente Steam.
2. **Un wrapper por dominio** (Manager/Service); gameplay no llama Steamworks crudo.
3. **Partner ≠ depot:** Input templates y Rich Presence tokens requieren Publish en Partner.
4. **Callbacks cada frame** vía `SteamAPI.RunCallbacks` (o manager equivalente).

## Piezas core

| Pieza | Rol |
|-------|-----|
| Steam API Init/Shutdown | Lifecycle |
| CallbackManager | Eventos tipados |
| `steam_appid.txt` | Solo **dev**; no en depot final |
| Native DLL | `steam_api64.dll` (Windows) |
| Overlay | Shift+Tab; no pelear el foco sin razón |

## Steam Input

- Manifest VDF + action sets (Menu/Game).
- `RunFrame` antes de leer input de juego.
- Fallback XInput si Steam Input no live.
- **Ship:** Partner → Template **Gamepad** → Save + Publish.
- Probar en cuenta **sin Your Layouts**.

Pitfall: el layout del developer no es el de los amigos.

## Overlay

- Detectar overlay activo si necesitás pausar input.
- Invites/achievements UX pueden abrir overlay; tené path in-game cuando importe.

## Networking

- Preferí Steam Networking Messages/Sockets para peers Steam.
- Version/build check antes de simular.
- Ver [11_Multiplayer](../11_Multiplayer/README.md).

## Lobby + Invites

| Flujo | Notas |
|-------|-------|
| Create/Join/Leave lobby | Friends-only o public según diseño |
| Overlay invite | `ActivateGameOverlayInviteDialog` |
| In-game prompt | `LobbyInvite` → Accept/Decline UI propia |
| Join accepted | `GameLobbyJoinRequested` |
| Cold start | `+connect_lobby` / connect string |
| Rich Presence | `connect` + tokens Partner |

Reglas UX: no prompt mid-gameplay crítico (encolar); un dueño de join; guest Leave; host no kick self.

## Workshop

- Publish solo content **user**; official shipped aparte.
- Sync subs a UserData/`Workshop/{id}/`.
- Versioná formatos UGC; validá al cargar.
- Legal agreement flow UX.

## Leaderboards

- Nombres de boards estables y documentados.
- Score meaning claro (menor tiempo, mayor puntos…).
- Offline: fallback a local.
- Anti-cheat: honestidad v1; host-auth si online.

## Achievements / Stats

- Definir en Partner; desbloquear vía wrapper.
- Idempotente; loguear fails.
- No spamear API cada frame.

## Cloud Save

- Mapear UserData relevante; excluir caches/logs.
- Conflictos: documentar “last write” o UI de resolución.

## Rich Presence

- Tokens localizados en Partner.
- Clear al salir de estados.
- Connect string parseable y versionado.

## SteamPipe / branches / review

Ver detalle en [15_ReleasePipeline](../15_ReleasePipeline/README.md):

- Depots sin `steam_appid.txt`
- Branch `beta` antes de default
- Store page + capturas
- Steam Deck: controls + resolución + performance

## Testing

- [ ] Sin Steam cliente
- [ ] Con Steam, 2 accounts lobby/invite
- [ ] Pad cuenta limpia
- [ ] Workshop publish→subscribe
- [ ] LB upload/download
- [ ] Deck o proto Deck verification

## Pitfalls comunes

- Doble subscribe de callbacks
- Gameplay → Steam directo
- UGC mezclado con Content oficial
- Assumir Overlay invite = in-game UX
- Subir build Debug con developerMode on

Worked example (producto): `docs/05-STEAM.md` en el repo Color Blocks.
