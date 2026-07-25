# 15 — Release Pipeline

## Configuraciones

| Build | Uso |
|-------|-----|
| Debug | Dev diario, overlays |
| Release | Perf + testing interno |
| Steam/Publish | Depot upload; developerMode off |

## Versionado

- Version visible en menú (`1.2.3` + build id / git sha corto).
- Incrementar protocol/content versions cuando rompas compat net/saves.
- Changelog interno por build.

## Git

- `main` estable; features en branches.
- Tags de release (`v1.0.0`).
- No commitear `bin/obj` ni secrets.

## Steam upload

1. Build Release x64
2. Empaquetar depot **sin** `steam_appid.txt`
3. Incluir native Steam + Content + VDFs Input
4. Subir a branch `beta`
5. QA checklist ([17](../17_Checklists/BeforeSteamReview.md))
6. Promote a `default`

## Developer mode removal

- Flag default false
- Strip o ignore en configuración Publish
- Verificar que no queden botones sandbox en Main Menu

## Build reports

Guardar: commit, fecha, config, tamaño depot, smoke test pass/fail.

## Consolas (futuro)

- Cert guidelines aparte; input/TRC; builds firmadas.
- Ver plantilla [16_Roadmaps/ConsoleRelease.md](../16_Roadmaps/ConsoleRelease.md).

## Checklist rápido

- [ ] Version bump
- [ ] Release build corre
- [ ] Dev mode off
- [ ] Steam beta smoke (2 accounts si MP)
- [ ] Store assets actualizados

Ver [10_Steam](../10_Steam/README.md), [08_DeveloperMode](../08_DeveloperMode/README.md).
