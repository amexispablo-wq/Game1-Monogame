# 11 — Multiplayer

## Recomendación v1

**Host-authoritative:** el host simula; clientes envían input y aplican snapshots.

Predicción / rollback = fase 2, cuando v1 sea jugable.

## Capas

```
Social (Lobby / Party / Invites)     → Steam u otro
Session (roles, peers, level id)
Transport (packets)
Simulation (tick, ownership)
Presentation (interp visual)
```

No mezclar lobby chat con gameplay packets sin framing claro.

## Lobby / Party

- Roster: quién está, slots locales (couch), leader.
- Start signal versionado (mismo build/content).
- Kick / Leave reglas claras (ver Steam lobby guide).

## Ownership / Authority

- Cada entidad replicada: `NetworkId`, `OwnerId`, flags host-controlled.
- Host simula todo en v1.
- Input: frames por tick con id de jugador.

## Snapshots / Replication

- Snapshot: estado necesario para reconstruir vista/sim cliente.
- Seq numbers; descartar viejos.
- Compresión después de correctness.

## Prediction (después)

- Cliente predice local; reconcilia con snapshot.
- Diseñá estado serializable limpio desde v1 para no reescribir entidades.

## Diagnostics

- Overlay: rol, seq, packet loss, last error.
- Logs con peer id + build version.
- Repro: grabar inputs (opcional) para soak tests.

## Version validation

- Mismo protocol version + content hash/build guid.
- Reject join con mensaje claro.

## Checklist v1

- [ ] 2 peers misma sesión
- [ ] Leave/disconnect seguro
- [ ] Mismatch de versión detectado
- [ ] Host + client documentados en roadmap
- [ ] No predicción hasta que snapshots sean estables

Ver [10_Steam](../10_Steam/README.md), [16_Roadmaps/Multiplayer.md](../16_Roadmaps/Multiplayer.md).
