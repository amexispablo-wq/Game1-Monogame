# Checklist — nuevo juego MonoGame + Steam

Orden sugerido. Tachá al completar.

## Día 0 — repo

- [ ] Repo + `.gitignore` (`bin/`, `obj/`, `.vs/`, user secrets)
- [ ] Proyecto MonoGame DesktopGL compila
- [ ] `docs/` con README + link a este playbook (o copia del playbook)
- [ ] Namespace + `#nullable enable` convention

## Semana 1 — loop jugable

- [ ] `IScene` + Menu + una escena de juego
- [ ] Input KB (+ pad básico)
- [ ] Settings resolución / fullscreen
- [ ] Tick fijo si hay sim

## Steam mínimo

- [ ] `SteamManager` fail-soft + `RunCallbacks`
- [ ] `steam_appid.txt` + native DLL copy
- [ ] Overlay smoke test (Shift+Tab)

## Social

- [ ] Lobby create/join/leave
- [ ] Invite overlay
- [ ] Invite prompt in-game + suppress in gameplay
- [ ] Rich Presence connect + Partner tokens
- [ ] Kick host / Leave guest

## Input ship-ready

- [ ] Manifest VDF + RunFrame order
- [ ] XInput fallback
- [ ] Partner Template **Gamepad** → Publish
- [ ] Test cuenta limpia

## Online (si aplica)

- [ ] Host-auth v1 (input + snapshot)
- [ ] 2-client QA documentado
- [ ] Predicción = fase posterior

## Meta (si aplica)

- [ ] Leaderboards upload/download
- [ ] Workshop publish/subscribe paths separados de official
- [ ] (Opcional) Replay/Ghost UGC

## Release

- [ ] Roadmap actualizado (código vs Partner)
- [ ] SteamPipe depot sin appid txt
- [ ] Beta branch
- [ ] Store checklist ([`09-steampipe-release.md`](09-steampipe-release.md))

## Referencias rápidas

| Paso | Doc |
|------|-----|
| Bootstrap | [`01-bootstrap-monogame.md`](01-bootstrap-monogame.md) |
| Roadmap | [`02-roadmap-steam.md`](02-roadmap-steam.md) |
| Steam core | [`03-steam-core.md`](03-steam-core.md) |
| Lobby | [`04-steam-party-lobby.md`](04-steam-party-lobby.md) |
| Input | [`05-steam-input.md`](05-steam-input.md) |
| Net | [`06-networking-hostauth.md`](06-networking-hostauth.md) |
| UI | [`07-ui-focus-nav.md`](07-ui-focus-nav.md) |
| Dev | [`08-dev-tooling.md`](08-dev-tooling.md) |
| Ship | [`09-steampipe-release.md`](09-steampipe-release.md) |
