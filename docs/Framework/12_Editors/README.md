# 12 — Editors

## Tipos

| Tipo | Cuándo |
|------|--------|
| In-game editor | Iteración rápida, mismos runtime rules |
| External tool | Pipelines complejos, artistas no-programadores |
| Data-only (JSON/YAML) | Prototipo / mods |

## Principios

1. **Mismo formato** que carga el runtime (o export determinista al formato runtime).
2. **Validación** al guardar (spawns, metas, budgets).
3. **Version** en archivos editables + migración.
4. No acoplar el editor a un solo género: pensá “documentos de contenido” (niveles, quests, items).

## In-game

- Dirty flag + confirm al salir.
- Undo/redo si el scope lo permite.
- Playtest desde el editor (bootstrapping sesión local).
- Dev-only para editar official; user content siempre editable.

## External

- CLI import/export.
- Thumbnails/previews generados por el mismo código de runtime si es posible.

## Workshop / mods

- Publish path solo desde user content validado.
- Read-only sub → “Create local copy” para editar.

## Anti-patrones

- Formato de editor distinto e indocumentado del runtime.
- Guardar en Content/ del bin.
- Validación solo en UI (el load path también debe validar).

Ver [06_AssetPipeline](../06_AssetPipeline/README.md), [05_SaveSystem](../05_SaveSystem/README.md).
