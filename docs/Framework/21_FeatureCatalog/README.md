# 21 — Feature Catalog

## Mandatory Features

Deben existir (o infra lista) según [20_GameStandards](../20_GameStandards/README.md):

| Feature | Por qué |
|---------|---------|
| Settings | Sin options no hay ship serio |
| Input (actions + rebind) | Accesibilidad + pads |
| Logging | QA remoto |
| Developer Mode | Iteración segura |
| Build Version | Repro de bugs |
| Steam Ready (fail-soft) | Si el destino es Steam |
| Save System | Progreso |
| UserData | Persistencia correcta |
| Migration | No romper jugadores |
| Localization ready | Evita reescritura UI |
| Controller Support | Estándar moderno |
| Menus (Main/Pause/Options) | Baseline UX |
| Debug Overlay | Diagnóstico |

## Features to Evaluate

Decidir por juego (roadmap). No implementar “porque sí”.

| Feature | Pregunta de decisión |
|---------|----------------------|
| Achievements | ¿Loop de meta largo? |
| Workshop / UGC | ¿Herramientas de creación? |
| Leaderboards | ¿Competitivo / speedrun? |
| Cloud Save | ¿Multi-dispositivo? |
| Replay System | ¿Marketing / share / debug? |
| Ghosts | ¿Racing / WR? |
| Dedicated Servers | ¿Escala > P2P? |
| Crossplay | ¿Multi-store? |
| Photo Mode | ¿Fantasy visual fuerte? |
| Accessibility (full) | ¿Alcance (paletas, remapping UI, subtitles)? |
| Difficulty Modes | ¿Audiencia amplia? |
| Analytics | ¿Privacidad / utilidad? |
| Modding | ¿Comunidad técnica? |
| Split Screen | ¿Couch coop? |
| Procedural Generation | ¿Replayability core? |
| HDR | ¿Target visual high-end? |
| Steam Deck verify | ¿Ship Steam? → casi mandatory eval |
| Rich Presence | ¿Social discovery? |
| Benchmark suite | ¿Sistemas frágiles? |
| Console Commands | ¿Dev speed vs abuse? |
| Rollback netcode | ¿Fighting / precision PvP? |

Marcá en el roadmap del producto: Adopt / Defer / Reject + razón de una línea.
