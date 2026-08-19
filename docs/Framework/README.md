# MonoGame Game Development Framework

**Kit canónico (portable):** [`C:\Users\amexi\Desktop\MonoGame-Game-Framework\`](file:///C:/Users/amexi/Desktop/MonoGame-Game-Framework/)

Usá ese directorio como fuente de verdad para juegos nuevos. Ver `USAGE.md` allí.

Esta carpeta (`docs/Framework/` dentro de Color Blocks) es una **copia de lectura** histórica. Puede desactualizarse; no edites estándares universales solo acá — subilos al kit del Desktop.

---

Copia local (índice de referencia; puede laggear respecto al canónico):

## Cómo empezar un juego nuevo

1. Leer [`20_GameStandards`](20_GameStandards/README.md) (mínimo obligatorio).
2. Seguir [`00_GettingStarted`](00_GettingStarted/README.md) + checklist en [`17_Checklists/BeforeFirstPrototype.md`](17_Checklists/BeforeFirstPrototype.md).
3. Copiar plantilla de roadmap desde [`16_Roadmaps`](16_Roadmaps/README.md).
4. Usar prompts de [`19_PromptLibrary`](19_PromptLibrary/README.md) en Cursor.
5. Consultar [`18_LessonsLearned`](18_LessonsLearned/README.md) antes de decidir formatos de save/Steam/red.

## Índice

| # | Área | Contenido |
|---|------|-----------|
| 00 | [GettingStarted](00_GettingStarted/README.md) | Bootstrap, carpetas, paquetes, convenciones |
| 01 | [ProjectArchitecture](01_ProjectArchitecture/README.md) | Loop, tick fijo, escenas, servicios, acoplamiento |
| 02 | [Rendering](02_Rendering/README.md) | Separar draw/sim, cámaras, resolución |
| 03 | [Input](03_Input/README.md) | Acciones, rebind, pad, Steam Input-ready |
| 04 | [UI](04_UI/README.md) | Foco, menús, pause/options |
| 05 | [SaveSystem](05_SaveSystem/README.md) | UserData, versiones, migración |
| 06 | [AssetPipeline](06_AssetPipeline/README.md) | Content, async load, official vs user |
| 07 | [DebugTools](07_DebugTools/README.md) | Overlays, filosofía debug |
| 08 | [DeveloperMode](08_DeveloperMode/README.md) | Gates de ship, sandboxes, benchmarks |
| 09 | [Performance](09_Performance/README.md) | Alloc, pooling, GC, profiling |
| 10 | [Steam](10_Steam/README.md) | Integración completa Steam |
| 11 | [Multiplayer](11_Multiplayer/README.md) | Lobby, autoridad, snapshots |
| 12 | [Editors](12_Editors/README.md) | Editores in-game / externos |
| 13 | [AI](13_AI/README.md) | Guías genéricas de IA |
| 14 | [ProceduralGeneration](14_ProceduralGeneration/README.md) | Seeds, determinismo |
| 15 | [ReleasePipeline](15_ReleasePipeline/README.md) | Builds, versionado, SteamPipe |
| 16 | [Roadmaps](16_Roadmaps/README.md) | Plantillas de roadmap |
| 17 | [Checklists](17_Checklists/README.md) | Checklists de hitos |
| 18 | [LessonsLearned](18_LessonsLearned/README.md) | Lecciones universales |
| 19 | [PromptLibrary](19_PromptLibrary/README.md) | Prompts Cursor reutilizables |
| 20 | [GameStandards](20_GameStandards/README.md) | Estándar mínimo por proyecto |
| 21 | [FeatureCatalog](21_FeatureCatalog/README.md) | Mandatory vs Evaluate |
| 22 | [GenreGuides](22_GenreGuides/README.md) | Guías por género |

## Relación con documentación de producto

- **Producto CB:** `docs/01-ARQUITECTURA.md` … `docs/10-REPLAY-Y-GHOSTS.md`
- **Framework canónico:** `C:\Users\amexi\Desktop\MonoGame-Game-Framework\`

No dupliques mecánicas de producto en el Framework. Linkeá al doc de producto cuando haga falta un ejemplo real.
