# 08 — Developer Mode

## Qué es

Flag persistente (`developer_settings.json` u equivalente) o compilación `#if DEBUG` que habilita:

- Overlays y teclas debug
- Escenas sandbox
- Tuning en vivo
- Benchmarks CLI
- Cheats / spawn tools

## Reglas de ship

1. **Default OFF** en builds que suben a Steam.
2. No depender de developerMode para features de jugador.
3. Si el flag puede quedar en true por error: build Release ignora o strippea.
4. Sandboxes no reemplazan QA en contenido real.

## Sandboxes

Escena aislada por mecánica (combate, inventario, pathfinding, net soak).  
Inputs y UI mínimos; reset rápido.

## Tuning en vivo

- Panel que edita floats/bools de un objeto `Tuning` serializable.
- Botón “reset defaults”.
- Opcional: dump a JSON para promover a defaults de código.

## Benchmarks CLI

```
dotnet run -- --benchmark <suite>
```

- Headless o ventana mínima.
- Exit code ≠ 0 si FAIL.
- Correr en CI o pre-merge para sistemas frágiles (física, pathfinding).

## Checklist

- [ ] Flag documentado
- [ ] Ship build no muestra botones sandbox
- [ ] Overlay F3 gated
- [ ] Al menos un benchmark o soak test documentado

Ver [07_DebugTools](../07_DebugTools/README.md), [09_Performance](../09_Performance/README.md).
