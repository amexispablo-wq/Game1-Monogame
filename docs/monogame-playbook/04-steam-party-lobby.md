# 04 — Party / Lobby / Invites

## Modelo

- **Lobby Steam** = sala social (quién está, metadata, start signal).
- **Party local** = slots en esta máquina (KB + pads).
- **Roster sync** = serializar slots → lobby member/lobby data → rebuild en peers.

## Flujos a cubrir

| Flujo | Callback / API |
|-------|----------------|
| Create friends-only lobby | `CreateLobby` |
| Invite overlay | `ActivateGameOverlayInviteDialog` |
| Invite recibido in-game | `LobbyInvite_t` → **UI propia** Accept/Decline |
| Accept overlay Steam | `GameLobbyJoinRequested` → Join |
| Join Game / cold start | `GameRichPresenceJoinRequested` / `+connect_lobby` |
| Kick remoto | chat message o lobby API según diseño |
| Leave guest | LeaveLobby + UI “Leave” en slot propio |

## Reglas UX (probadas)

1. **Un dueño** de invite/join (`InviteManager`) — evita double-join.
2. Prompt in-game para `LobbyInvite`; **no mostrar mid-gameplay** (encolar).
3. Host **no** Kick self; guest ve **Leave** en su slot (cualquier input local).
4. Leader flag en roster = asiento **primario** del owner (no “solo keyboard”).
5. Tras join guest → navegar a escena Party/lobby si no está ya ahí.

## Lobby data útil

- Level id / mode flags / “host in gameplay”.
- Member data: slots locales serializados (K/G + controller id).
- Version/build id para reject mismatch.

## Checklist

- [ ] 2 cuentas: overlay invite → join
- [ ] 2 cuentas: prompt in-game Accept/Decline
- [ ] Guest Leave vuelve a menú / disuelve local
- [ ] Host Kick saca peer
- [ ] Invite durante partida no rompe run (queue)
- [ ] Rich Presence Join Game con juego cerrado (launch args)

**Referencia CB:** `SteamInviteManager`, `SteamLobbyService`, `SteamPartyService`, `PartyScene`, [`../05-STEAM.md`](../05-STEAM.md).
