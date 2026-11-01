# Performance plan

Updated: 2026-10-30

The project already has the right high-level shape for a performance-oriented
MOBA core: fixed-tick simulation, phase ordering, pooled projectile state,
non-alloc spatial queries, and data-driven definitions. The next step is to
make the claim measurable and remove the remaining algorithmic hot spots.

## Performance contract

We will judge changes against a repeatable 30 Hz simulation scenario with 6
and 10 active brawlers, AI enabled, projectiles, deployables, breakables, and
the active game-mode systems running.

Track these numbers in a Development build and in the Unity Profiler:

- simulation tick time: average, p95, and worst tick;
- `GC.Alloc` during steady-state simulation;
- spatial queries per tick and candidates examined per query;
- AI decisions and path queries per tick;
- active projectiles and pooled-object misses;
- catch-up ticks after a long frame.

The initial target is zero managed allocations during steady-state simulation
and a stable 30 Hz tick under the 10-brawler scenario. Exact device targets
will be set after the first baseline capture rather than guessed in code.

## Prioritized work

### P0 — measure before tuning

- Add profiler markers around each simulation phase, spatial queries, path
  queries, AI decisions, damage resolution, and projectile collision.
- Add a deterministic performance scenario that can be run repeatedly with the
  same seed and roster size.
- Record a baseline before each structural change.

### P1 — algorithmic hot paths

1. **A* open set — in progress.** Replace the list scan plus `Contains` with a
   reusable binary min-heap and node-local open/closed state. This changes the
   open-set operations from linear scans to O(log n), while keeping
   deterministic insertion-order tie-breaking.
2. **Spatial grid membership — in progress.** Keep a cell's entities in a
   dense list, but maintain an entity-id → cell/index membership table. The
   first slice uses swap-back removal, O(1) duplicate detection, and empty-cell
   cleanup. Point-collision selection now uses a stable membership-order
   tie-breaker, so gameplay does not depend on swap-back iteration order.
3. **Simulation registry.** Replace the phase dictionary and per-register
   `List.Contains` with fixed phase buckets plus membership/index metadata.
   Registration is setup work, but this removes avoidable lookup and removal
   costs from lifecycle churn.
4. **Reusable result buffers.** Audit every query that returns a new `List<T>`
   or copies a collection. Keep caller-owned buffers for simulation paths and
   reserve allocating convenience APIs for editor/UI code.

### P2 — scheduling and memory behavior

- Stagger expensive AI perception, scoring, and path requests across ticks;
  enforce an explicit per-bot decision budget.
- Replace per-frame dictionary scans in bounded systems with indexed arrays or
  compact pools where entity IDs can be mapped to stable slots.
- Pool transient damage, combat-log, hazard, gem, crate, and VFX work objects.
- Add match-scoped reset/cleanup for registries, event buses, telemetry, and
  static scratch state so repeated matches do not retain stale references.

### P3 — Unity boundary costs

- Cache all components used in tick/update paths.
- Replace scene-wide `Find*` calls with an explicit match context and cached
  service references.
- Keep presentation interpolation, logging, HUD refresh, and VFX work out of
  the simulation tick; consume compact presentation events instead.
- Use `MaterialPropertyBlock` and shared materials for runtime visual variants.

### P4 — scale-out only after profiling

- Move large, flat simulation state to contiguous structs where profiling shows
  cache pressure.
- Evaluate Unity Jobs/Burst and Native containers for broad per-entity passes.
- Do not introduce ECS or Native containers into small, branch-heavy systems
  until the measured bottleneck justifies the ownership and safety cost.

## Data-structure decisions

| Workload | Current shape | Direction | Reason |
|---|---|---|---|
| A* frontier | `List<PathNode>` + scan + `Contains` | binary min-heap + node state | O(log n) pop/update; no duplicate membership scan |
| Spatial cell | `Dictionary<Vector2Int, List<TEntity>>` | dense cell list + id/index membership | O(1) movement/removal and no duplicate linear scan |
| Tick phases | dictionary of lists | fixed phase buckets + indexes | fewer lookups and cheaper lifecycle churn |
| Nearby query output | caller-owned `List<TEntity>` in most hot paths | reusable buffers; optional capped query | zero steady-state allocation |
| AI request scheduling | per-system scans/dictionaries | tick buckets or min-heap by next-due tick | bounded work per simulation tick |
| Large entity state | object graph and interface dispatch | contiguous structs only where measured | better locality, but higher migration complexity |

## Current first slice

The first code slice now covers both the A* open-set refactor in `AStarSolver`
and spatial-grid membership/empty-cell cleanup. The existing navigation and
spatial lifecycle tests remain the correctness gate. The next slice is
profiler markers plus the repeatable scenario so later gains are reported with
evidence rather than intuition.
