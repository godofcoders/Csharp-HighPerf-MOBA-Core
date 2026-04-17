# MOBA Core — Session Log

> **Continuity protocol:** At the start of each session, Claude reads this file top to bottom to recover context. At the end of each session, Claude appends a new entry. The "Current State" block at the top is kept up to date.

---

## Project Context (stable)

- **What:** C# High-Performance MOBA Core — hobby/learning project modeled after AAA MOBA architecture
- **Repo:** `/Users/vapronix/Documents/GitHub/MOBA/Csharp-HighPerf-MOBA-Core`
- **Engine:** Unity, C# .NET
- **Current focus:** Phase 1 — offline vertical slice, gameplay systems only
- **Planned multiplayer stack (Phase 3+):** Unity Netcode for GameObjects (free)
- **Owner:** Akash
- **Plan doc:** `docs/PHASE_1_PLAN.md`

## Current State

- **Phase:** 1
- **Active session:** 2 (complete)
- **Last completed session:** 2
- **Next session target:** Session 3 — State decomposition + first unit tests
- **Blockers:** none

## Key Decisions Ledger

| # | Decision | Choice | Locked in session |
|---|----------|--------|-------------------|
| 1 | Phase 1 game mode | **Gem Grab** | 1 |
| 2 | Second brawler authoring | **Not needed** — four brawlers already scaffolded. Replaced with Session 4 differentiation pass. | 1 |
| 3 | Starting sequence | **Architecture warmup first** (S2–S3), then brawler loadouts (S4–S10), then game mode | 1 |
| 4 | Loadout scope | **Full loadout** — 2 gadgets + 2 star powers + 1 hypercharge per brawler (20 assets total) | 1 |
| 5 | Networking stack (future) | Unity Netcode for GameObjects (Phase 3+) | 1 |
| 6 | Gear | **Deferred** out of Phase 1 | 1 |

## Glossary (concepts we've covered)

| Term | Where introduced | One-line meaning |
|------|------------------|------------------|
| Tick phase | S2 | A named stage inside a single simulation tick that runs to completion before the next stage starts; phases have an explicit ordering so system-to-system ordering stops being accidental. |
| Insertion-order iteration | S2 | Iterating a list in the order things were added. Fragile because behavior depends on wiring-up order rather than intent. |
| Deterministic simulation | S2 | Given the same inputs and same tick sequence, the simulation produces exactly the same outputs. Required for rollback netcode and replays. |
| Phase-bucketed registry | S2 | Registry that stores tickables in per-phase lists, so each tick walks phases in declared order instead of walking a single flat list. |
| Reverse iteration during tick | S2 | Iterating `list[i]` from `Count-1` down to `0` so that if an entity self-unregisters during its Tick, we don't skip the next element. |

## Sessions

### Session 1 — 2026-04-17 — Phase 1 kickoff

**Goals**
- Align on Phase 1 scope (offline, gameplay-systems-focused)
- Set up session continuity (this file + plan doc)
- Lock in 3 starting decisions

**Work done**
- Full architecture review of ~200 gameplay C# files under `Assets/Scripts/Core`
- Created `docs/PHASE_1_PLAN.md` (Phase 1 plan with session-by-session outline)
- Created `docs/SESSIONS.md` (this file)
- Produced a strengths/gaps assessment: strong at gameplay simulation patterns (tick loop, strategy-pattern abilities, utility AI, modifier pipeline, spatial grid). Weak at determinism, tests, docs, and Unity decoupling. All aligned with the learning goals.

**Decisions made**
- Phase 1 is strictly offline; networking pushed to Phase 3
- Will target Unity Netcode for GameObjects when networking arrives
- Strict-determinism work (fixed-point) deferred to Phase 2

**Decisions locked in**
- D1: Gem Grab as Phase 1 game mode
- D2: No new brawler authoring — four already scaffolded (Colt, Byron, Jessie, Barley). Replaced with Session 4 fix pass + S5–S9 loadout authoring.
- D3: Architecture warmup first (S2–S3), then brawler loadouts (S4–S10), then game mode
- D4: Full loadout per brawler — 2 gadgets + 2 star powers + 1 hypercharge each (20 loadout assets)
- D5: Unity Netcode for GameObjects is the target multiplayer stack for Phase 3+
- D6: Gear deferred out of Phase 1

**Brawler-content audit finding**
- All four brawlers have: definition asset, model prefab, base stats, main attack ability
- Colt/Byron/Jessie have supers; **Barley's super is missing** (SuperAbility fileID=0)
- **No brawler has an AIProfile assigned** — all currently use default utility weights (1/1/1), so archetypes don't behave differently
- No gadgets, star powers, hypercharges, gear, or builds wired on any brawler
- Archetype enum values: Colt/Byron/Jessie=0, Barley=3 — likely uninitialized defaults
- Byron has a dedicated `HybridAoEAbilityLogic.cs`; Jessie has `Scrappy_DeployableDefinition`; Barley has `Barley_puddle_hazard` — so the custom ability logic exists per archetype

**System infrastructure audit finding** (good news — these exist, no rebuild needed)
- **Gadgets:** `GadgetDefinition` base with `MaxCharges=3`; concrete logics `DashGadgetLogic`, `HealBurstGadgetLogic`, `AllyHealPulseGadgetLogic`; `GadgetLockStatusEffect` for silencing
- **Hypercharge:** `HyperchargeDefinition` (duration + speed/damage/shield buffs + optional EnhancedSuper swap); `HyperchargeTracker` (AddCharge/Activate/Tick)
- **Star Powers / Passives:** `StarPowerDefinition`, `PassiveDefinition`, `PassiveFamilyDefinition`, `PassiveLoadoutRules`, `PassiveLoadoutValidationResult`, `IPassiveRuntime` — full passive lifecycle system exists
- Build option resolution via `BrawlerBuildResolver` exists

**Known smells logged**
- `HyperchargeTracker.Activate()` hardcodes `30f` (TPS) on line 31 — should read from sim clock. Fix opportunistically in Session 2 or 3.
- `Assets/Scripts/Data/Scriptables/Colt.asset` + root-level `Blaze_*.asset` look archival; canonical Colt is at `Assets/Scriptables/colt/Colt_Definition.asset`

**Learnings flagged for next session**
- Explicit tick phases (why insertion-order iteration is fragile)
- Cohesion vs coupling in state objects
- `Script` guid referenced by brawler definitions: `3571b305cae1f47d289f6b4a50e50d85` (will be useful when editing assets programmatically)

**Open questions / notes for future Claude**
- When reopening, first action: read this file, then read `docs/PHASE_1_PLAN.md`, then check the Key Decisions Ledger for anything still TBD
- Do not touch `Library/`, `Temp/`, `obj/` — Unity-generated
- Brawler scriptables live under `Assets/Scriptables/<name>/` — note some use lowercase (`colt`, `byron`) and some uppercase (`Jesse`, `Barley`); don't "fix" casing on existing assets without user confirmation — Unity may be referencing exact paths
- Older/deprecated-looking scriptables exist at `Assets/Scripts/Data/Scriptables/Colt.asset` and root-level `Blaze_*.asset` — treat as archive, don't use as canonical

**Next session goal**
- Session 2: Tick-phase refactor. Before writing any code, explain the *why* (determinism, debuggability, ordering bugs). Introduce `TickPhase` enum, phased registration API, and migrate existing `ITickable` usage. Keep changes behavior-preserving; no gameplay changes this session.

---

<!-- New session entries go below this line, newest at the bottom. Follow the template above. -->

### Session 2 — 2026-04-17 — Tick-phase refactor

**Goals**
- Land the `TickPhase` enum and phase-bucketed `SimulationRegistry`
- Migrate every call site off the old `Register(ITickable)` API
- Integrate `ProjectileManager` into the registry (was driven out-of-band)
- Bonus: clean up the `HyperchargeTracker` hardcoded 30 TPS smell

**Work done**
- Created `Assets/Scripts/Core/Simulation/TickPhase.cs` — 9-phase enum with values spaced by 10 (PreTick=0 through PostTick=80) so new phases can slot in without renumbering, which matters once we have save-game / replay compatibility to worry about.
- Rewrote `Assets/Scripts/Core/Simulation/SimulationRegistry.cs` from a single flat `List<ITickable>` to `Dictionary<TickPhase, List<ITickable>>`:
  - Buckets are pre-populated in the constructor so `Register()` never allocates during gameplay
  - `Register(ITickable, TickPhase)` / `Unregister(ITickable, TickPhase)` / `TickAll(uint)` is the new API
  - Ordered phase list cached once from `Enum.GetValues` — ascending value → intended phase order
  - Within a phase, reverse iteration is used so that self-unregistering tickables don't cause skip bugs
- Added `protected virtual TickPhase Phase => TickPhase.Movement;` to `SimulationEntity`. Base OnEnable/OnDisable now pass `Phase` to the registry.
- `BrawlerAIController` overrides `Phase => TickPhase.InputApply`. This replaces "AI ticks before brawler because Unity happens to call the components in this order" with an explicit phase-ordering guarantee.
- `DeployableController`: removed the redundant `SimulationClock.Registry?.Register(this)` / `Unregister(this)` calls in `Initialize` and `Despawn`. The base-class OnEnable/OnDisable already handles this — explicit calls were a harmless-but-dead duplicate.
- `ProjectileManager` now implements `ITickable`:
  - Registered in `Start()` (not `Awake()`) so `SimulationClock.Awake` has run first and `Registry` exists
  - Registered in **Collision phase** (preserves old "projectiles tick after brawlers" ordering)
  - Unregisters in `OnDestroy`
  - `ManualTick` renamed to `Tick`; removed from `IProjectileService` interface
- `SimulationClock.Update` now makes exactly one call: `_registry.TickAll(tickCount)`. The separate `projectileService.ManualTick(tickCount)` call is gone (including its silent `try/catch`).
- Fixed `HyperchargeTracker.Activate()` hardcoded `30f`: now uses `SimulationClock.TickDeltaTime` as the source of truth. `durationSeconds / TickDeltaTime` gives the correct tick count for any TPS.

**Files touched**
- New: `Assets/Scripts/Core/Simulation/TickPhase.cs`
- Modified: `Assets/Scripts/Core/Simulation/SimulationRegistry.cs`
- Modified: `Assets/Scripts/Core/Infrastructure/SimulationEntity.cs`
- Modified: `Assets/Scripts/Core/Infrastructure/BrawlerAIController.cs`
- Modified: `Assets/Scripts/Core/Simulation/Deployable/DeployableController.cs`
- Modified: `Assets/Scripts/Core/Infrastructure/ProjectileManager.cs`
- Modified: `Assets/Scripts/Core/Simulation/IProjectileService.cs`
- Modified: `Assets/Scripts/Core/Infrastructure/Services/SimulationClock.cs`
- Modified: `Assets/Scripts/Core/Simulation/Progression/HyperchargeTracker.cs`

**Decisions made**
- Chose single-phase-per-entity over multi-phase registration. Today every `SimulationEntity` subclass has one monolithic `Tick()`; there's nothing to slice yet. When we later want multi-phase (Session 3+), we can promote `Phase` to `Phases[]` with zero call-site changes outside the registry.
- `ProjectileManager` lands in a single phase (Collision) for Session 2. The full split into `TickMovement` (→ Movement) + `TickCollision` (→ Collision) is deferred to Session 3, where unit tests will make the split verifiable.
- `BrawlerController` stays in Movement phase even though its `Tick` also does action-state updates, command consumption, and ability ticks. This is acknowledged debt to be paid down during state decomposition (Session 3).

**Learnings covered**
- **Why explicit phases beat insertion-order iteration:** ordering becomes a property of the *system design* rather than a property of *object spawn order*. A bug from "brawler 3 was spawned before the projectile manager, so they tick in the wrong order" becomes impossible.
- **Reverse iteration during iteration-over-list-that-mutates:** safer than forward iteration when elements may self-remove. Also covered: forward iteration with `List.Count` re-read each step is correct only if removals happen at or after the current index.
- **Phase values spaced by 10:** makes future insertions renumber-free, which matters when phase numbers end up serialized (save games, replays).
- **Pre-populated dictionary buckets:** avoids allocations in the hot path. Tiny detail, big difference on mobile.
- **Determinism as a through-line:** every decision this session (explicit phases, fixed order within phase, no try/catch to swallow errors) pushes toward "same inputs → same outputs" even though we're not at fixed-point math yet. Phase 2 will be easier because of this.
- **Kill-credit attribution as the within-phase ordering motivator:** two projectiles hitting the same target on the same tick need stable, replayable tie-break rules; insertion order is fragile, deterministic tiebreakers (entity IDs) are durable.

**Open questions / notes for future Claude**
- `BrawlerController.Tick` is still a god-method — 80 lines spanning several phases worth of work. Session 3 state decomposition should split it.
- `ProjectileManager.Tick` single-pass still mixes Movement + Collision + Damage + Cleanup. Split in Session 3 once unit tests exist.
- `DeployableController.OnDisable` and `DeployableController.Despawn` partially duplicate work (both unregister from grid / combat registry). Harmless, but worth tidying when deployable content work starts in Session 5.
- `SimulationClock.TickDeltaTime` is a `public const float = 1f / 30f`. If we ever want runtime-configurable TPS we'll need to turn this into a property fed by the instance's `_ticksPerSecond`. The `HyperchargeTracker` fix already reads through this constant, so promoting it is a one-touch change.

**Next session goal**
- Session 3: State decomposition + first tests. Split `BrawlerState` into `BrawlerStats` / `BrawlerCooldowns` / `BrawlerActionState` / `BrawlerLoadout`. Add a Unity test assembly. Write the first unit tests for damage math and the stat-modifier pipeline. As a warmup, split `ProjectileManager.Tick` into `TickMovement` and `TickCollision` and register for both phases — the tests we add this session will keep that refactor honest.

---
