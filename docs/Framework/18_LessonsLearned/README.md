# 18 — Lessons Learned

Lecciones **universales** extraídas de construir un juego MonoGame+Steam real. Aplican a cualquier género.

## Datos y archivos

1. **Nunca** guardar progreso de usuario solo dentro de `Content/` / output de build.
2. Usá `%LocalAppData%/<Game>/` (o equivalente) como root de UserData.
3. **Versioná** saves, settings, niveles, replays, protocolos de red.
4. **Migraciones** explícitas; backup antes de migrate.
5. IDs estables > nombres display como claves.

## Arquitectura

6. Separá gameplay de rendering.
7. Tick fijo si hay net, física sensible o replays.
8. Gameplay **nunca** llama Steam/filesystem APIs directo — pasa por servicios.
9. Un dueño por flujo (invites, saves, lobby).
10. Diseñá ownership de entidades aunque el v1 sea single-player.

## Input / UI

11. Acciones, no teclas hardcodeadas.
12. Teclado **y** control desde el prototipo.
13. Suppress confirm al cambiar escena (click-through).
14. Modales bloquean input de la escena detrás.
15. Steam Input: Partner Publish ≠ copiar VDF al depot.

## Steam / net

16. Fail-soft sin Steam.
17. Prompt in-game de invites; no asumir solo overlay.
18. No mostrar prompts sociales mid-run crítico — encolar.
19. Host-auth v1 antes de predicción.
20. Validá versión/build entre peers.
21. Workshop/UGC en carpeta separada del content oficial.
22. Rich Presence tokens viven en Partner, no solo en el cliente.

## Producto / proceso

23. Mostrá **build version** en menú u overlay.
24. DeveloperMode default off en ship.
25. Cada sistema importante loguea errores.
26. Benchmarks/CI para sistemas frágiles.
27. Documentá estado real (código listo ≠ Partner/QA listo).
28. Pensá Workshop/cloud al diseñar formatos — cambiar después duele.
29. Leader flag / roles: no asumas “solo keyboard = leader”.
30. Probá en cuenta limpia (pads, first-run), no solo en la del developer.

## Proceso

31. Roadmaps con exit criteria, no solo wishlists.
32. Checklists de hitos antes de “casi ship”.
33. Framework docs &gt; copiar mecánicas de un juego a otro.

Cuando descubras una lección nueva: **añadila aquí** con número y una línea de por qué.
