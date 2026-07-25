# 20 — Game Standards

**Mínimo obligatorio** para todo juego nuevo del Framework. Si falta algo de esta lista en alpha, no está “listo para contenido serio”.

## Sistemas / features

| Requisito | Notas |
|-----------|-------|
| Scene system | `IScene` + ChangeScene limpio |
| Main Menu | Entrada clara al loop |
| Pause Menu | En gameplay |
| Options Menu | Gráficos + audio + input |
| Settings persistence | UserData versionado |
| Graphics settings | Resolución |
| Fullscreen / Borderless / Windowed | Tres modos |
| Audio settings | Music + SFX |
| Input rebinding | KB + pad |
| Keyboard + Mouse + Controller | Menús y gameplay básico |
| Steam Input ready | Arquitectura lista; Partner si ship Steam |
| Localization ready | Keys (aunque 1 idioma) |
| Developer mode | Gated; off en ship |
| F3 Debug Overlay | FPS + version + escena mínimo |
| Console / debug commands | O plan explícito “más tarde” con stub |
| Logging | Archivo bajo UserData |
| Crash / error handling | Fail-soft + log |
| Build version display | Menú u overlay |
| UserData folder | LocalAppData root |
| Data migration support | Version field + path |
| Save system | Load/save progreso o equivalente |
| Async loading support | Para cargas no triviales |
| Benchmark mode | CLI o escena; al menos un smoke |
| Screenshot mode | O “Evaluate” documentado si no aplica |

## Calidad de arquitectura

- Gameplay ≠ Steam directo
- Render ≠ mutar sim
- Official content ≠ UserData ≠ UGC

## Definition of Done (alpha)

Cumple esta tabla + [BeforeAlpha](../17_Checklists/BeforeAlpha.md).

Ver también [21_FeatureCatalog](../21_FeatureCatalog/README.md) para lo que es Evaluate.
