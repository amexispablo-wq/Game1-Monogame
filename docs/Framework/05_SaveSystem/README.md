# 05 — Save System

## Por qué

Saves rotos = reviews negativas. Versioná desde el día 1.

## Dónde guardar

| Tipo | Ubicación |
|------|-----------|
| Settings, saves, logs | `%LocalAppData%/<GameName>/` (Windows) o equivalente |
| Content shipped | `Content/` junto al exe |
| UGC / Workshop | Subcarpeta UserData, **no** mezclar con official |

**Nunca** persistir progreso crítico solo bajo `bin/.../Content/`.

## Versionado

```json
{ "version": 3, "data": { } }
```

- Campo `version` (int) en todo formato serializado (save, settings, replay, level pack).
- Migraciones explícitas: `v1 → v2 → v3` en pipeline.
- Si no se puede migrar: backup `.bak` + mensaje al usuario.

## Prácticas

1. Escritura atómica: write temp → flush → rename.
2. Un slot activo documentado; autosave vs manual claros.
3. Backward compatible cuando sea barato; si no, migrar o rechazar con log.
4. No guardar referencias a paths absolutos de la máquina del developer.
5. IDs de contenido estables (no display names).

## Settings vs Save

- **Settings:** gráficos, audio, binds — independientes del slot de campaña.
- **Save:** progreso, inventario, world seed, etc.
- Ambos versionados; migraciones separadas.

## Cloud (evaluar)

Si Steam Cloud: mismos archivos UserData; excluir caches/logs pesados. Ver [10_Steam](../10_Steam/README.md) y [21_FeatureCatalog](../21_FeatureCatalog/README.md).

## Checklist

- [ ] UserData root centralizado
- [ ] version en saves + settings
- [ ] migración + test “save viejo”
- [ ] backup en fallo de migrate
- [ ] logs de load/save errors

Siguiente: [06_AssetPipeline](../06_AssetPipeline/README.md).
