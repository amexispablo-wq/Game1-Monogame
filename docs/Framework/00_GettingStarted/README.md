# 00 — Getting Started

Cómo debe nacer **todo** proyecto MonoGame nuevo.

## Stack recomendado

| Pieza | Recomendación |
|-------|----------------|
| Runtime | .NET 8 o 9, `x64` |
| Motor | `MonoGame.Framework.DesktopGL` (+ Content Builder Task) |
| Steam (si aplica) | `Steamworks.NET` + native `steam_api64.dll` |
| Nullability | `#nullable enable` en código nuevo |
| IDE | Cursor / VS / Rider |

## Estructura de carpetas sugerida

```
MyGame/
  Core/           Program, Game host, Presentation, Simulation
  Scenes/         IScene + menús + gameplay scenes
  UI/             Widgets + Navigation/focus
  Input/          Action map, backends, rebind
  Services/       Settings, Save, Audio, Logging (o Managers/)
  Content/        Content.mgcb + assets shipped
  Steam/          (si aplica) wrappers; nunca desde Entities/
  Networking/     (si aplica)
  Diagnostics/    Logs, overlays, version
  docs/           Producto + copy/link a Framework
  steam_appid.txt (solo dev)
```

Adaptá nombres; mantené la **separación**: gameplay ≠ render ≠ Steam ≠ UI.

## Checklist de creación

- [ ] `dotnet new` / plantilla MonoGame DesktopGL
- [ ] `.gitignore`: `bin/`, `obj/`, `.vs/`, user secrets
- [ ] Namespace único del juego
- [ ] `IScene` + MenuScene mínima
- [ ] Settings persistidos (resolución / audio stub)
- [ ] UserData path (`%LocalAppData%/GameName/`)
- [ ] Build version visible en menú o overlay
- [ ] Logging a archivo bajo UserData
- [ ] `developerMode` gated (ver [08](../08_DeveloperMode/README.md))
- [ ] Docs: README producto + link a Framework / GameStandards

## Content Pipeline

- Assets **shipped** → `Content/` + MGCB o copy-to-output documentado.
- Datos de usuario / saves / UGC → **nunca** solo dentro de `Content/` en el output (se pierden al clean/update).
- Ver [06_AssetPipeline](../06_AssetPipeline/README.md).

## Convenciones de código

- Un namespace (o pocos, estables).
- Servicios dueños en el `Game` host; escenas piden APIs de alto nivel.
- Gameplay determinista en **tick fijo** si hay red o replays.
- IDs estables (string/guid/int versionados), no “nombres display” como clave.
- Errores Steam/IO: fail-soft + log; no crash al fallar overlay.

## Naming

| Tipo | Ejemplo |
|------|---------|
| Escenas | `MenuScene`, `GameScene` |
| Servicios | `SettingsService`, `SteamLobbyService` |
| Datos | `SaveData`, `SettingsData` + `Version` |
| Acciones input | `Jump`, `Confirm`, `Cancel` (no `Space`, `A`) |

## Configuración inicial

- `app.manifest` DPI awareness (Windows).
- Resolución default + aplicar al boot desde settings.
- Steam: fail-soft Init (ver [10_Steam](../10_Steam/README.md)).

## Siguiente paso

→ [01_ProjectArchitecture](../01_ProjectArchitecture/README.md) y [20_GameStandards](../20_GameStandards/README.md).
