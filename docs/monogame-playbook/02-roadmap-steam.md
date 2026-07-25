# 02 — Plantilla roadmap Steam

Copiar tabla a `docs/ROADMAP.md` del juego nuevo. Marcar ✅ / 🟡 / ❌ con honestidad (código ≠ Partner listo).

## Visión (una frase)

> [Juego] en Steam: [local/online], [meta social: LB/Workshop/Achievements].

## Estado actual

| Área | Estado | Notas |
|------|--------|-------|
| Gameplay single / local coop | | |
| UI + input (KB/pad) | | |
| Steam init fail-soft | | |
| Lobby / invites | | |
| Online gameplay | | |
| Leaderboards | | |
| Workshop / UGC | | |
| Steam Input Partner Publish | | |
| SteamPipe / store | | |

## Fases (plantilla)

| Fase | Objetivo | Exit criteria |
|------|----------|---------------|
| **0 Feel/QA** | Mecánica core estable | Benchmark o checklist manual verde |
| **1 Pulido local** | Options, audio, resoluciones, bindings | Build jugable 30–60 min sin Steam features |
| **2 Steam social** | Init + lobby + invite + presence | 2 cuentas: invite → misma party UI |
| **3 Online gameplay** | Host-auth v1 | 2 clients misma sesión; documentar lag |
| **4 Meta Steam** | LB y/o Workshop | Upload/download verificado en Partner |
| **5 Ship** | SteamPipe + store + Input Publish | Beta branch + cuenta limpia pad OK |

## Orden sugerido

```
Feel → Local polish → Lobby/invites → Host-auth net → LB/Workshop → SteamPipe
```

No abrir Workshop/LB marketing hasta lobby estable. Predicción/interp **después** de host-auth jugable.

## Anti-patrones

- Marcar “❌ no implementado” cuando el servicio ya existe (usar “🟡 QA/Partner”).
- Depender de Your Layouts Steam Input del dev (amigos pad muerto).
- Meter UGC en la misma carpeta que content oficial.

**Referencia CB:** [`../08-ROADMAP.md`](../08-ROADMAP.md).
