# 07 — Debug Tools

## Filosofía

1. **Todo sistema importante loguea** (Steam, save, net, load).
2. **Overlays baratos** en DeveloperMode; apagados en ship.
3. **Una tecla canónica** (sugerido **F3**) para el overlay principal.
4. La info debe servir para **reproducir bugs**, no solo lucir números.

## Overlay sugerido (F3)

| Bloque | Contenido |
|--------|-----------|
| Perf | FPS, frame ms, GC gen0/1/2 counts si posible |
| Memory | Managed heap approx / content loaded |
| Sim | Tick index, accumulator, timescale |
| Scene | Nombre escena actual |
| Input | Device activo, action samples |
| Steam | Init, user, lobby id |
| Net | Rol Host/Client, seq snapshot, RTT si hay |
| Version | Build id / commit / configuración |
| Player | Posición, estado (si aplica) |

No hace falta implementar todo el día 1; reservá el layout.

## Otros atajos (política)

| Tecla | Uso típico |
|-------|------------|
| F3 | Toggle debug overlay |
| F8/F9 | UI focus debug |
| F10/F11 | Benchmark / replay tools |
| ~ o ` | Console de comandos (si existe) |

Documentá la tabla en el README del juego. Conflictos con rebind: debug keys fuera del action map de jugador o gated por developerMode.

## Console / commands

Comandos útiles genéricos: `god`, `timescale`, `load`, `give`, `netgraph`, `clear_save`.  
Solo en DeveloperMode o cheats explícitos de build.

## Crash / logging

- Unlogger a archivo rotativo bajo UserData/Logs.
- En crash: flush + versión + escena + última acción.
- No spamear disco en hot path (rate-limit).

Ver [08_DeveloperMode](../08_DeveloperMode/README.md).
