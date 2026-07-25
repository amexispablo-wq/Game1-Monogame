# 09 — SteamPipe / release

## Antes de upload

- [ ] App ID producción (no sandbox mezclado)
- [ ] Build Release x64
- [ ] **Sin** `steam_appid.txt` en depot (Steam lo inyecta)
- [ ] `steam_api64.dll` + VDFs Input en paths esperados
- [ ] Content oficial completo; user paths no hardcodeados a máquina dev

## Depots (típico Windows)

1. Depot exe + DLLs + Content.
2. (Opcional) depot demos / dedicated.
3. Branches: `default`, `beta`.

## Partner checklist paralelo

| Item | Dónde |
|------|-------|
| Steam Input Template Publish | App Admin → Steam Input |
| Rich Presence tokens | Localization / presence |
| Leaderboards creadas | Stat/LB admin (nombres = código) |
| Workshop enabled | Workshop settings + legal |
| Store page | Caps, trailers, categories coop |
| Achievements | Opcional v1 |
| Cloud | Opcional; definir qué se sincroniza |

## Beta

1. Branch `beta` con build interna.
2. QA: 2 accounts online + Workshop subscribe + pad cuenta limpia.
3. Promote a default.

## Post-ship

- Crash / diagnostics path documentado.
- Hotfix process (mismo SteamPipe).

**Referencia CB:** [`../08-ROADMAP.md`](../08-ROADMAP.md) Fase 5, `SteamBuild/` / `Publish/` si existen en el repo.
