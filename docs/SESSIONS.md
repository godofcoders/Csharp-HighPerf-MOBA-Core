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
- **Active session:** 5 (in progress — testing curriculum kickoff: 159 unit tests across 6 fixtures, two precision bugs caught and fixed, `SimulationClock.SecondsToTicks` helper centralised across 13 production sites; earlier Session 5 work covered Archetype enum extension, BuildLayout wiring, resolver verification, DefaultBuild + AIProfile asset authoring)
- **Last completed session:** 4 (continued)
- **Next session target:** Open — more tier-3 testing (BrawlerStealth, BrawlerLoadout helpers, BrawlerBuildLayoutDefinition.IsSlotUnlocked), deferred deployable migrations (5 sites under pin-then-migrate), or back to gameplay/AI work (AIUtilityScorer Controller/Artillery cases). Akash to pick.
- **Blockers:** Unity smoke tests for status effects (#37) and loadout (#40) still parked. The new test fixtures provide much of what those smoke tests would have validated, but in-engine smoke is its own confidence check.

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
| POCO substate | S3 | "Plain Old C# Object" — a pure data/logic class with no Unity types, no singletons, no event-bus calls, no `Debug.Log`, no stored back-references to the coordinator. Carved out of a god-class so each substate owns one concern. |
| Pass-through property | S3 | A one-liner getter on the coordinator that returns a substate's field, so external callers written against the old API keep compiling without edits. Zero runtime cost for reference types. |
| Coordinator / substate pattern | S3 | The god-class shrinks into a thin coordinator that owns cross-concern orchestration and side effects; each concern lives in its own POCO substate. Two-layer: substates are pure, the coordinator is where Unity services and event buses show up. |
| Pure orchestration | S3 | A coordinator method that only *schedules* work across substates — no branching on substate internals, no raw state reads, no game logic inline. Each line is one verb on one substate. |
| Caller-owned out-buffer | S3 | A method takes a `List<T> outBuffer` and appends to it instead of returning a new list. Caller owns allocation + lifetime + clearing. Zero-alloc in the tick loop. Kent Beck calls the broader idea "Collecting Parameter." |
| Parameter-based context injection | S3 | Instead of a substate storing a reference to the coordinator, operations that need the coordinator take it as a method parameter. Avoids circular object graphs and keeps the substate's shape genuinely "plain." |
| Health-ratio preservation | S3 | When stats change mid-match (power up, new passives), the bracketing pattern is: snapshot `(oldMax, oldHealth)` → do stat work → set new CurrentHealth so the *ratio* is preserved rather than the absolute value. Lives on the coordinator because it touches two substates (Loadout + Stats) and fires the health-changed event. |
| Known-gap TODO | S3 | A latent bug found mid-refactor that is *documented where the trap lives* rather than silently fixed. Policy: refactor sessions are behavior-preserving; gameplay changes are their own decision, made with eyes open. See `BrawlerState.Reset` for the canonical example. |
| Data-driven strategy pattern | S4 | A family of behaviors expressed as ScriptableObject subclasses, each pairing a data definition with a `CreateRuntime()` factory that spawns its runtime counterpart. Mirrors how `PassiveDefinition` / `IPassiveRuntime` work; lets designers author new variants with no code changes to the install pipeline. Applied to `SuperChargeSourceDefinition` / `SuperChargeSourceRuntime` in S4. |
| Per-brawler charge tuning | S4 | Each brawler ships their own `*DamageSuperCharge.asset` rather than sharing one global rate. Fraction-of-meter-per-damage varies by brawler so fast-rate/low-damage heroes don't hit super instantly and slow-throw heroes still charge at a pace that rewards landing shots. |
| Push-based notification | S4 | Coordinator explicitly calls `NotifyDamageDealt` on the attacker after damage resolves, fanning out to every installed super-charge source runtime. Chosen over an event-bus listener because the damage pipeline already runs "direct call" style and every event-bus escape introduces a non-deterministic seam. |
| Hook-override runtime base | S4 | Abstract `SuperChargeSourceRuntime` ships five no-op virtual hooks (`OnInstalled`, `OnUninstalled`, `Tick`, `OnDamageDealt`, `OnHealApplied`); each concrete subclass overrides only the hook it cares about. Lets the install loop ping every hook on every runtime without null checks inside the callers. |
| POCO value testing | S5 | Testing a class that has no Unity dependencies, no static singletons, and no event-bus calls by constructing it directly with `new`, calling its methods, and asserting on its returned values or read-only properties. No fakes, no fixture setup beyond `new SUT()`. The cleanest possible unit test — applies to pure data/logic classes like `BrawlerActionStateMachine` and `BrawlerCooldowns`. |
| Hand-rolled fake | S5 | A small purpose-built class implementing the SUT's collaborator interface, written by hand instead of using a mocking framework like Moq. Records calls into public fields the test can assert on; returns canned values from public setters. Cheaper to maintain than Moq when collaborators are small interfaces, and far easier to reason about than dynamic proxies. Used throughout S5 (FakeClock, FakeStatusTarget, SpyPassive, SpyRuntime, SpyStatusEffectInstance). |
| Spy-on-hooks | S5 | Pattern where the SUT consumes an abstract base class with virtual hook methods (e.g. `PassiveDefinition.Install`/`Uninstall`); the test creates a subclass that overrides each hook to append a tagged string into a shared chronological `List<string>`. Asserting on the list verifies both *which* hooks fired and *in what order across multiple instances* — much stronger than per-instance call counters. Crucial for testing lifecycle pipelines like `BrawlerLoadout.InstallAll`/`UninstallAll`. |
| Static-state isolation discipline | S5 | When the SUT touches static singletons (`ServiceProvider`, `StatusEffectEventBus`), `[SetUp]` must call `Clear()` then re-register fakes, and `[TearDown]` must unsubscribe handlers (saving the delegate instance — C# event removal silently no-ops on a different instance) and `Clear()` again. Without this, tests leak state across runs and fail intermittently depending on run order. The `_appliedHandler` field pattern in `StatusEffectServiceTests` is the canonical shape. |
| State-machine table testing | S5 | For a state machine with N states and M transition methods, the test fixture walks every (state, transition, condition) cell — including the cells that should *not* transition. Built-in `[TestCase]` parameterization fans one method out across all values of the state enum so the table is exhaustive without copy-paste. Used in `BrawlerActionStateMachineTests` to cover all 8 `BrawlerActionStateType` values × 4 transition methods. |
| Boundary-pair test | S5 | For any strict inequality in production (`currentTick < LockUntilTick`), one test method asserts both sides of the boundary in sequence — the inclusive side returns `true`, the exclusive side returns `false`, with `LockUntilTick - 1` and `LockUntilTick` adjacent. Catches `<` vs `<=` typos that single-side tests miss. Two boundary pairs in `BrawlerActionStateMachineTests` (active-vs-expired, in-state-vs-cleared). |
| Crown jewel test | S5 | The 1–4 highest-value tests in a fixture, named explicitly in the fixture's doc-comment so future readers know which ones are load-bearing. Crown jewels are the tests that catch the bugs you actually fear — invariants like "merge path doesn't add a second instance" or "uninstall walks reverse order." If a refactor breaks a crown jewel, you stop and reconsider; if it breaks a peripheral test, you may just update the test. |
| Pin-then-migrate | S5 | Refactor pattern: when introducing a new helper (e.g. `SimulationClock.SecondsToTicks`), first land the helper + a small focused test fixture proving its contract, then migrate call sites in a separate commit (or commits) so each migration is reviewable in isolation. Avoids the "big bang" risk where a helper bug takes down 13 sites at once. Used for the seconds-to-ticks centralisation in S5. |
| Round-to-nearest tick conversion | S5 | The seconds → ticks helper computes `(uint)(seconds * tickRate + 0.5f)` rather than `(uint)(seconds * tickRate)` so 0.5s at 30Hz becomes 15 ticks, not 14. Fixes a class of off-by-one bugs (cooldowns 1 tick short, status effects expiring early) caused by truncation at the cast. The bug is invisible in playtest but caught by a single arithmetic test. |

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

### Session 3 — 2026-04-20 — BrawlerState decomposition (7 substates)

**Goals**
- Carve `BrawlerState` (originally 1,071 lines, nine concerns fused into one class) into focused POCO substates, one surgical move at a time
- Keep every external caller compiling by leaving pass-through properties on the coordinator
- Teach the patterns as we go: POCO, caller-owned out-buffer, pass-through, pure orchestration, parameter-based context injection, health-ratio preservation
- NOT in scope this session: test assembly (#20) and first unit tests (#21) — pushed to a dedicated session because the decomposition itself ran long enough to be the whole session

**Work done** (in the order we did the cuts)
- **Stats** → `BrawlerStats` (#16, #17): `CurrentHealth`, `MaxHealth`, `MoveSpeed`, `Damage`, shield pool, two damage-modifier collections, movement-modifier collection, plus pure helpers `ApplyDamage` / `ApplyHeal` / `ResetHealthToMax` / `ClearAllModifiers` / `SetCurrentHealth` with clamping enforced inside the POCO. Coordinator keeps the side-effectful `TakeDamage` / `Heal` (`Debug.Log`, `BrawlerPresentationEventBus`, `OnHealthChanged`, death transition).
- **Cooldowns** → `BrawlerCooldowns` (#22, #23): the three `AbilityCooldownState` timers (MainAttack / Super / Gadget), plus `IsReady(slot, tick)` / `StartCooldown(slot, tick, seconds)` / `ResetAll`. Kept as fields (not properties) inside the POCO so `struct` mutations persist.
- **Action-state machine** → `BrawlerActionStateMachine` (#25, #26): enter / clear / expire / interrupt transitions. `Current` exposes the active `BrawlerActionStateData`. Coordinator just calls through.
- **Resources** → `BrawlerResources` (#29, #30): ammo `ResourceStorage`, `SuperChargeTracker`, `HyperchargeTracker`, and the gadget-charge count. `Tick(deltaTime)`, `RefillAmmo`, `ResetHypercharge`, `ResetSuperCharge(filled)`, `UseGadgetCharge`, `AddSuperCharge`, `TryConsumeSuper`. The "push gadget-max-charges from RuntimeKit into Resources" seam stays on the coordinator — documented as the one explicit bridge between Loadout and Resources.
- **Stealth** → `BrawlerStealth` (#32, #33): `IsInBush`, `IsRevealed`, `LastAttackTick`, and the pure `IsHidden(currentTick)` query that composes all three. Pass-through is get/set on these three because `VisibilitySystem` / `RevealEffect` / `BrawlerController` write them directly.
- **Status effects** → `BrawlerStatusEffects` (#35, #36): the `Active` list plus every "do I have status X" query (`HasStatus`, `HasSilence`, `HasAttackLock`, `HasGadgetLock`, `HasSuperLock`, `HasMovementLockStatus`). Crucial method: `TickAndCollectExpired(target, tick, removedOut)` — the POCO ticks + reaps expired effects, appends them into a caller-owned buffer, and the coordinator owns the event-bus / combat-log side effects. `BrawlerState` holds one reusable `_tickRemovedBuffer` of capacity 4 so `TickEffects` is allocation-free in the hot path.
- **Loadout** → `BrawlerLoadout` (#38, #39): `CurrentPowerLevel`, `RuntimeBuild`, `RuntimeKit`, `EquippedHypercharge`, `HyperchargeModifierSource` token, `_equippedPassives`, and the private nested `InstalledPassive` struct holding `(Definition, Context, Runtime)` triples. Methods: `SetPowerLevel` (clamps ≥1), `SetEquippedHypercharge`, `SetEquippedPassives`, `InstallAll(target, owner)`, `UninstallAll(target)`, `TickPassives(target, tick)`, `RefreshRuntimeBuildUnlockState(definition)`, `ResetRuntimeState(definition)`, the slot-unlock reads, and the current-ability-definition lookups with `Definition` fallback. `InstallAll`/`UninstallAll`/`TickPassives` take the coordinator as a *method parameter* rather than storing it as a field — parameter-based context injection.
- **Reset() cleanup** (as a finishing pass): rewrote `BrawlerState.Reset` with five numbered sections (progression + passives, pooled resources, transient combat state, runtime-loadout slots, hypercharge-tagged modifiers), added `Loadout.ResetRuntimeState(Definition)` to encapsulate the RuntimeBuild.Clear + RuntimeKit.Clear + unlock-refresh cluster, and documented a latent respawn bug as a `KNOWN GAP (TODO session-4)` doc comment above `Reset`. Audit traced the respawn flow end to end (`Respawn` → `State.Reset` → `SetEquippedHypercharge` → `RefreshGadgetChargesFromRuntimeKit` → register) and confirmed nothing re-applies the resolved build after reset, so `GetCurrentGadgetDefinition` returns null post-respawn until match restart. Documented, not fixed — that's a gameplay decision, out of refactor scope.

**Files touched**
- New:
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerStats.cs`
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerCooldowns.cs`
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerActionStateMachine.cs`
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerResources.cs`
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerStealth.cs`
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerStatusEffects.cs`
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerLoadout.cs`
- Modified:
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerState.cs` (extensively — shrunk from monolith to a coordinator full of pass-throughs and cross-substate orchestration)
  - `Assets/Scripts/Core/Simulation/Progression/HyperchargeTracker.cs` (added `Reset()` so `BrawlerResources.ResetHypercharge` could be an in-place reset rather than a reference reassignment — #28)

**Decisions made**
- **Pass-throughs over call-site rewrites.** Every substate extraction preserved the old public API via one-line getters. The refactor added zero work for anyone calling `state.MaxHealth`, `state.Ammo`, `state.ActiveStatusEffects`, etc. This was the single most important discipline; it let every extraction be a local, reviewable change instead of a cross-file ripple.
- **Parameter-based context injection for Loadout.** Passive install/uninstall/tick need `PassiveInstallContext(State, Controller, SourceToken)`. Rather than storing a coordinator back-reference on `BrawlerLoadout` (which would recreate the exact kind of circular graph we were carving up), the methods take the coordinator as a parameter. Substate stays genuinely plain; circular ownership avoided.
- **Caller-owned out-buffer for status-effect reap.** `TickAndCollectExpired` writes removed effects into a caller-provided `List<IStatusEffectInstance>`. The coordinator owns one such buffer across ticks (`_tickRemovedBuffer`, capacity 4) so the tick loop doesn't allocate. Substate knows nothing about `StatusEffectEventBus` or `ICombatLogService`; those live on the coordinator.
- **Known-gap policy.** When the Reset() audit turned up the latent "RuntimeBuild wiped but not re-applied" bug, we *documented it in place* with `TODO(session-4)` rather than silently fixing it. Behavior-preserving refactors stay behavior-preserving; gameplay changes are their own decision with their own owner.
- **Smoke tests parked, not abandoned.** Status effects (#37) and loadout (#40) can't be exercised in the current gameplay state. Refactor is behavior-preserving so this isn't gating, but the tasks stay open so they're picked up the moment the gameplay integration lands.
- **No Unity test assembly this session.** Originally #20/#21 were planned for Session 3. The decomposition itself ran long and the teaching load was already heavy; better to land tests as their own session with full focus than to bolt them on at the end.

**Learnings covered**
- **POCO vs Unity-flavored class:** why pulling `Debug.Log` / `ServiceProvider` / event buses out of a substate is what makes it testable in a plain NUnit run with no Unity runtime.
- **Coordinator as the "seam" layer:** any method that touches *two* substates (health-ratio preservation, gadget-charges from RuntimeKit, hypercharge override of super) stays on the coordinator. One substate = zero coordinator logic.
- **Pure orchestration:** what `SetPowerLevel`, `SetPassiveLoadout`, `RefreshPassiveLoadout`, `Reset` look like when every line is one verb on one substate. `Reset` especially: nine different concerns, each a single method call, grouped into five numbered sections.
- **Caller-owned out-buffer as a zero-alloc pattern:** why the buffer lives on the coordinator rather than inside the POCO — the coordinator knows the ticking cadence and can size the capacity once.
- **Parameter-based context injection vs stored references:** the "ask for your context" call style keeps substates genuinely plain (no back-pointers to the coordinator) and avoids the circular-ownership smell.
- **Health-ratio preservation as the bracketing pattern:** snapshot `(oldMax, oldHealth)` → mutate stats → recompute `CurrentHealth = newMax * (oldHealth/oldMax)`. The reason this lives on the coordinator and not inside `Stats` is that the trigger is a *Loadout* change (power level, passives) — the bracket spans two substates.
- **Side-channel tour through the GoF catalog** (teaching, not code): plus Repository, CQRS, RAII, copy-on-write, double-buffering — all on the casual-intro level.

**Open questions / notes for future Claude**
- **Respawn gap (the KNOWN GAP):** `BrawlerState.Reset` wipes `RuntimeBuild` + `RuntimeKit` (via `Loadout.ResetRuntimeState`), but nothing in the controller's `Respawn` flow re-applies the resolved build. Result: after death→respawn the brawler has no gadget/starpower/gears until the next match. Two fix options documented in the Reset() doc comment. Decide in Session 4.
- **Remaining cuts inside BrawlerState.cs** (each is clean to extract, none is urgent):
  - *Action-request translator layer* (~150 lines, stateless helper pattern): the fourteen switch statements that translate `BrawlerActionRequestType` → resource / cooldown slot / block reason / etc. Pure functions of the enum. Natural home: a static `BrawlerActionRequestTranslator` or an instance helper injected with the substates it reads.
  - *Block-reason evaluators* (~120 lines): `GetMainAttackBlockReason` / `GetGadgetBlockReason` / `GetSuperBlockReason` / `GetHyperchargeBlockReason` / `GetMovementBlockReason`, plus the `CanUseX` one-liners on top. Pure "read substate state → return enum" query object. Natural home: a `BrawlerActionGate` that takes the brawler and answers yes/no per action type.
  - *Damage/heal pipeline* (~100 lines): `TakeDamage` and `Heal` on the coordinator do `Stats.ApplyX` + `Debug.Log` + `BrawlerPresentationEventBus.Raise` + `OnHealthChanged` + death handling. Could be a `BrawlerDamagePipeline` collaborator that owns the side-effect choreography; payoff is modest relative to risk, because the death-transition path is subtle.
- **BrawlerController.Tick god-method (carried over from Session 2):** still 80+ lines spanning several tick phases. Session 2's next-session note flagged this; state decomposition was the precondition, and it's now done. Ripe whenever we want a short, focused session.
- **ProjectileManager.Tick split (also carried over from Session 2):** single-pass Movement + Collision + Damage + Cleanup. Session 2 said "split once unit tests exist" — still waiting on tests.
- **`SimulationClock.TickDeltaTime` is a compile-time `const`** — if Phase 2 wants runtime TPS, promote to instance property. `HyperchargeTracker` already reads through it, so one touch.
- **Don't re-read the Session 2 projectile smells yet** — they're blocked on the test harness. Keep them parked until #20/#21 land.

**Next session goal**
- Session 4 (per plan doc): Brawler fix pass. Author Barley's missing super, design + author four distinct `BrawlerAIProfile` assets (ranged-skirmisher / hybrid-support / summoner-zoner / area-zoner) and wire each brawler, fix the `Archetype` enums, verify `BrawlerBuildResolver` option-unlock logic. First thing on arrival: decide the respawn-gap fix (preserve-build-through-Reset vs re-apply-after-Respawn). Optional stretch work if time permits and the learner wants more refactor practice: the three remaining BrawlerState cuts listed above, the `BrawlerController.Tick` split, or standing up the Unity test assembly (#20) + first damage-math tests (#21).

---

### Session 4 — 2026-04-20 → 2026-04-21 — Barley super + super-charge pipeline

**Goals** (scope expanded mid-session)
- Author Barley's missing super ability (`SuperAbility` was null on the brawler definition) — done
- Replace the test-only "spawn with full super" + "flat 0.20 per hit" placeholders with a real, extensible super-charge system — done
- Per-brawler tuning, with scaffolding in place for future charge sources (heal, auto-over-time, ally-proximity)
- NOT in scope this session: AIProfile assets, Archetype fixes, BuildResolver verification — pushed to a follow-up once super-charge is verified in-game.

**Work done**

*Barley super (#42, #43)*
- Investigated Barley's existing assets and existing super delivery patterns. Ruled out `VolleyProjectileAbilityDefinition` (no arc), `BurstSequenceProjectile` (no arc), and single-bottle `ThrownHybridAoE` — none fit the "barrage of arcing bottles that leave puddles" design.
- Created a new `ThrownVolleyAoEAbilityDefinition` (Barley-folder scope) combining: multi-shot burst + arc motion + AoE impact + lingering hazard. Mirrors existing definition-plus-logic pattern; `CreateLogic()` returns `ThrownVolleyAoEAbilityLogic`.
- `ThrownVolleyAoEAbilityLogic` runs `brawler.RunTimedBurst` (coroutine pattern from `BurstSequenceProjectileLogic`) distributing N bottles across a configurable spread angle with distance jitter. Each bottle is an arc-motion `ThrownImpactAoE` that spawns the existing `Barley_puddle_hazard` on landing.
- Authored `Barley_Super.asset` ("Last Call") reusing the existing puddle hazard. Tuned: 7u range, arc height 1.75, impact radius 2.25, six bottles at 0.08s cadence, 30° spread, 0.2 jitter, puddles do the damage (bottles don't, avoiding double-counting).
- Wired `Barley_BrawlerDefinition.SuperAbility` → the new asset.

*Super-charge architecture (#44, #45, #46)*
- Investigation of the existing flow turned up three problems: (1) `Resources.ResetSuperCharge(true)` was being called in both constructor and `Reset` — a debug line granting free super on spawn / respawn; (2) `DamageService` had a hardcoded `DefaultSuperChargePerHit = 0.20f` flat grant with no per-brawler or per-damage scaling; (3) `ProjectileSpawnContext.SuperChargeOnHit` is set by several ability logics but *never read* anywhere — dead code. Also noted three bypass paths that skip `DamageService` entirely (two in `BurnEffect`, one in `DeployableController`), meaning burn DoTs and deployable damage currently feed no charge at all — known-gap, not fixing this session.
- Designed a data-driven ScriptableObject strategy pattern matching the existing `PassiveDefinition` shape. Abstract `SuperChargeSourceDefinition : ScriptableObject` with a single `abstract SuperChargeSourceRuntime CreateRuntime()`; abstract `SuperChargeSourceRuntime` with five virtual no-op hooks (`OnInstalled`, `OnUninstalled`, `Tick`, `OnDamageDealt`, `OnHealApplied`). Subclasses override only the hooks they care about.
- Scaffolded all four concrete definition/runtime pairs even though only damage is wired today: `DamageDealtChargeSource`, `HealingDoneChargeSource`, `AutoOverTimeChargeSource`, `AllyProximityChargeSource`. `Ally`'s runtime takes an injected `AllyScanner` callback so a future brawler-registry service can wire it up without touching the runtime itself.
- Added `BrawlerDefinition.SuperChargeSources` array so each brawler lists the sources they feed from in-editor.
- Extended `BrawlerLoadout` with the install/uninstall/tick/notify lifecycle (`InstallSuperChargeSources`, `UninstallAllSuperChargeSources`, `TickSuperChargeSources`, `NotifyDamageDealt`, `NotifyHealApplied`) — mirrors the existing passive install pattern.
- Added coordinator pass-throughs on `BrawlerState` (`TickSuperChargeSources`, `NotifyDamageDealt`, `NotifyHealApplied`) that delegate to `Loadout`. Ticking wired in `BrawlerController.Tick` alongside `TickPassives` so auto-over-time and proximity sources get a delta every sim step.
- Replaced `DamageService`'s hardcoded 0.20f grant with `ctx.Attacker.State.NotifyDamageDealt(workingDamage, victimState)`. Removed the `DefaultSuperChargePerHit` constant entirely.
- Removed both test-only `Resources.ResetSuperCharge(true)` lines (constructor + Reset) — respawn now has an empty meter, charged from configured sources. `BrawlerResources.ResetSuperCharge` default parameter flipped to `false` so accidentally omitting the arg now defaults to the safe production behavior.
- Authored per-brawler `*_DamageSuperCharge.asset` for Colt, Byron, Jessie, Barley, and Blaze. Rates tuned per brawler — Colt 0.00020 (fast cadence), Byron 0.00035 (slow but accurate), Jessie 0.00045 (lower base damage), Barley 0.00029 (slow throw), Blaze 0.00029 — all targeting roughly 7–10 landed hits to fill the meter. Wired each brawler definition's `SuperChargeSources` array to point at their own asset.

**Files touched**
- New code:
  - `Assets/Scripts/Core/Definitions/Brawler/Barley/ThrownVolleyAoEAbilityDefinition.cs`
  - `Assets/Scripts/Core/Simulation/Abilities/Barley/ThrownVolleyAoEAbilityLogic.cs`
  - `Assets/Scripts/Core/Definitions/Brawler/SuperCharge/SuperChargeSourceDefinition.cs`
  - `Assets/Scripts/Core/Definitions/Brawler/SuperCharge/DamageDealtChargeSource.cs`
  - `Assets/Scripts/Core/Definitions/Brawler/SuperCharge/HealingDoneChargeSource.cs`
  - `Assets/Scripts/Core/Definitions/Brawler/SuperCharge/AutoOverTimeChargeSource.cs`
  - `Assets/Scripts/Core/Definitions/Brawler/SuperCharge/AllyProximityChargeSource.cs`
  - `Assets/Scripts/Core/Simulation/SuperCharge/SuperChargeSourceRuntime.cs`
  - `Assets/Scripts/Core/Simulation/SuperCharge/DamageDealtChargeRuntime.cs`
  - `Assets/Scripts/Core/Simulation/SuperCharge/HealingDoneChargeRuntime.cs`
  - `Assets/Scripts/Core/Simulation/SuperCharge/AutoOverTimeChargeRuntime.cs`
  - `Assets/Scripts/Core/Simulation/SuperCharge/AllyProximityChargeRuntime.cs`
- New assets:
  - `Assets/Scriptables/Barley/Barley_Super.asset` (+ meta)
  - `Assets/Scriptables/colt/Colt_DamageSuperCharge.asset` (+ meta)
  - `Assets/Scriptables/byron/Byron_DamageSuperCharge.asset` (+ meta)
  - `Assets/Scriptables/Jesse/Jessie_DamageSuperCharge.asset` (+ meta)
  - `Assets/Scriptables/Barley/Barley_DamageSuperCharge.asset` (+ meta)
  - `Assets/Scriptables/Blaze_DamageSuperCharge.asset` (+ meta)
- Modified:
  - `Assets/Scripts/Core/Definitions/Brawler/BrawlerDefinition.cs` (added `SuperChargeSources` array)
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerLoadout.cs` (install/tick/notify lifecycle)
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerState.cs` (constructor install, Reset re-install, `TickSuperChargeSources`, `NotifyDamageDealt`, `NotifyHealApplied`, removed both `ResetSuperCharge(true)` test lines)
  - `Assets/Scripts/Core/Simulation/Brawler/BrawlerResources.cs` (flipped `ResetSuperCharge` default to `false`, doc updated)
  - `Assets/Scripts/Core/Simulation/Damage/DamageService.cs` (replaced flat 0.20 grant with `NotifyDamageDealt`, removed dead constant)
  - `Assets/Scripts/Core/Infrastructure/Brawler/BrawlerController.cs` (wired `TickSuperChargeSources` into the per-tick loop)
  - `Assets/Scriptables/Barley/Barley_BrawlerDefinition.asset` (SuperAbility + SuperChargeSources)
  - `Assets/Scriptables/colt/Colt_Definition.asset`, `Assets/Scriptables/byron/Byron_definition.asset`, `Assets/Scriptables/Jesse/Jesse_Definition.asset`, `Assets/Scriptables/Blaze_BrawlerDefinition.asset` (SuperChargeSources wired)

**Decisions made**
- **Strategy pattern via ScriptableObject, not interface.** Each charge source is a `ScriptableObject` subclass with a `CreateRuntime()` factory, matching the existing `PassiveDefinition` / `IPassiveRuntime` shape. Keeps the system authoring-friendly (designers create assets in the `Create Asset` menu, no code changes) and lets each source carry its own tuning fields.
- **Abstract runtime base with virtual no-op hooks, not an interface.** An interface would force every concrete runtime to stub every hook; the virtual-no-op base lets a damage-only source leave `Tick` / `OnHealApplied` untouched. Same trick gives the install loop a single "call every hook on every runtime" shape with no null-check scaffolding inside callers.
- **Per-brawler assets, not shared.** Every brawler ships their own `*_DamageSuperCharge.asset`. Explicit user ask. Also lets future balance passes tune one brawler without touching any others.
- **Push notification from coordinator, not event-bus subscription.** `DamageService` already runs direct-call style (passes a `DamageContext`, invokes methods on `ctx.Attacker`). Adding an event-bus listener would introduce a non-deterministic seam in the damage pipeline; push notification keeps determinism intact and matches the existing idiom.
- **Scaffold all four source types up front, even though only damage is wired.** User-approved. Saves a future session having to introduce the definition/runtime pair infrastructure — adding a new source later becomes a one-file-each drop.
- **Dead code removed (`DefaultSuperChargePerHit`), not just replaced.** The constant was the last caller; leaving it would confuse future readers. `SuperChargeOnHit` on `ProjectileSpawnContext` is also dead code — flagged in the Open Questions list below but left alone this session.
- **Known-gap: three TakeDamage bypass paths.** `BurnEffect` (two call sites) and `DeployableController` call `ITakeDamage.TakeDamage` directly, skipping `DamageService` entirely. Under the new system this means burn DoTs and deployable damage don't feed any super charge. Documented here; deciding whether to route them through `DamageService` is its own gameplay call.

**Learnings covered**
- **Data-driven strategy pattern** — how ScriptableObject subclasses with a factory method beat a switch-over-enum approach for extensibility. Designers add an asset; no code change.
- **Virtual-no-op hooks vs interface methods** — why the base class pays off when most concrete subclasses only care about one or two of the hooks.
- **Push vs pull for cross-system notification** — why we chose direct `NotifyDamageDealt` call over `DamageEventBus` subscription (determinism, matches existing style, no ordering risk).
- **Killing test-only debug lines at their source** — the `ResetSuperCharge(true)` default parameter flip means future "forgot to specify" calls default to production-correct behavior.
- **Scaffolding future work when the cost is low** — authoring four def/runtime pairs now (when the install plumbing is fresh) vs coming back later for three of them separately.

**Open questions / notes for future Claude**
- **`ProjectileSpawnContext.SuperChargeOnHit` is dead code.** Five ability logics still *set* it (`ChargedShotProjectileLogic`, `BurstSequenceProjectileLogic`, `VolleyProjectileLogic`, two more); nothing reads it. Decision point: either (a) delete the field + the five set sites, or (b) revive it as an ability-specific multiplier on top of the `DamageDealtChargeSource` grant. Leaning (a).
- **Three bypass paths around `DamageService`.** `BurnEffect` (two call sites), `DeployableController`. These call `ITakeDamage.TakeDamage` directly, so the new `NotifyDamageDealt` never fires. If we want burn-tick and deployable damage to charge super, they need to route through `DamageService` (or manually call `NotifyDamageDealt`). Gameplay decision.
- **AllyProximity runtime is scaffolded but dormant.** Needs a brawler/team registry service before it can do anything. When that service lands, `BrawlerState.InstallSuperChargeSources` (or the `InstallAll` call site) needs to inject the `AllyScanner` callback on each `AllyProximityChargeRuntime` it creates.
- **Heal pipeline doesn't push `NotifyHealApplied`.** `HealingDoneChargeSource` will no-op until the heal pipeline is updated to fan out. Small drop-in when we get there — every heal site calls `healer.State.NotifyHealApplied(appliedAmount, recipient)`.
- **Auto-over-time `PauseInCombat` flag.** Currently authored but unimplemented — needs a "recently took/dealt damage" field on `BrawlerState` first. Tick-gate is one line once that lands.
- **Per-brawler rates are first-pass estimates.** Based on "~7–10 landed main-attack hits to fill" heuristic, but not yet playtested. Expect a balance pass once #37/#40 smoke tests are back online.

**Next session goal**
- Finish Session 4: AIProfile assets (four distinct profiles — ranged-skirmisher / hybrid-support / summoner-zoner / area-zoner), Archetype enum fixes, BrawlerBuildResolver option-unlock verification. Then loadouts work (S5+). First thing to decide on arrival: the respawn-gap fix carried over from Session 3.

---

### Session 4 (continued) — 2026-04-21 — Gap-fix pass

**Goals**
- Close the three known-gap TODOs left at the end of the super-charge pipeline work: `ProjectileSpawnContext.SuperChargeOnHit` dead code, `BurnEffect` `DamageService` bypass, respawn-time loadout wipe. Explicit user ask: "fix these gaps".

**Work done**

*Gap 1 — Dead code removal (`SuperChargeOnHit`)*
- Deleted the `SuperChargeOnHit` field from `ProjectileSpawnContext` and from the internal projectile-data struct in `ProjectileManager`. Removed the copy-over at `ProjectileManager.CreateProjectile`. Removed the six set sites (`ChainProjectileLogic`, `DeployableAbilityUser`, `ThrownVolleyAoEAbilityLogic`, `ThrownHybridAoEAbilityLogic`, `HybridProjectileLogic`, `BrawlerController.FireProjectile`). Chose option (a) from the S4 Open Questions — the field had no readers and "ability-specific multiplier" can live on a new ScriptableObject source type later if it actually becomes a gameplay requirement; no reason to carry dead fields in the meantime.

*Gap 2 — BurnEffect bypass of DamageService*
- `BurnEffect.Tick` now routes every DoT tick through `ServiceProvider.Get<IDamageService>().ApplyDamage` with a constructed `DamageContext` (`Attacker = _source`, `Target = <spatial entity>`, `Damage = _magnitude`, `Type = DamageType.Ability`). Before: two direct `state.TakeDamage(_magnitude)` calls bypassed the pipeline entirely — no incoming modifiers, no shield, no lifesteal, no combat-log entry, no `DamageEventBus` event, and (critically under the new super-charge system) no `NotifyDamageDealt` push to the ignitor's charge sources.
- Needed a way to get from `IStatusTarget` (what `BurnEffect.Tick` receives) to `ISpatialEntity` (what `DamageService.ApplyDamage` needs as `Target`). `BrawlerState.Owner` already held the back-reference to `BrawlerController`, but `DeployableState` had no such back-ref. Added a `DeployableController Controller { get; set; }` property on `DeployableState` and wired it from `DeployableController.Initialize` immediately after state construction — mirrors `BrawlerState.Owner` setup.
- Side-benefit: before this change, a fatal burn tick on a deployable would zero the state's health but *not* despawn the controller (which only happens in `DeployableController.TakeDamage`, not in the state). Routing through `DamageService` → `ctx.Target.TakeDamage(...)` → `DeployableController.TakeDamage(...)` now hits the despawn path too. Two bugs, one fix.

*Gap 3 — Respawn loadout wipe*
- Picked option (b) from the `BrawlerState.Reset` doc comment: have the controller re-apply the resolved build after `State.Reset()`. The alternative (have `Reset` preserve `RuntimeBuild`/`RuntimeKit`) would have re-used stale ability-logic instances across death→respawn; keeping the wipe + re-resolve flow means every life gets fresh logic instances, matching match-start behavior exactly.
- Extracted the build-resolution block from `InternalInitialize` into a private `ResolveAndApplyCurrentBuild()` helper on `BrawlerController` (handles the three cases: resolved build applies, resolve-error falls back to legacy, no-default-build falls back to legacy). `InternalInitialize` now calls the helper in place of the inlined block; `Respawn` calls `State.RuntimeKit.SetMainAttack/SetSuper` and then the same helper after `State.Reset()`. One source of truth for "how do we pick and install the brawler's build".
- Updated doc-comments on `BrawlerState.Reset` and `BrawlerLoadout.ResetRuntimeState` — the "KNOWN GAP" block on Reset and the "see the TODO" pointer on ResetRuntimeState now describe the solved behavior with a Session 4 back-reference.

**Files touched**
- `Assets/Scripts/Core/Simulation/ProjectileSpawnContext.cs` (removed `SuperChargeOnHit`)
- `Assets/Scripts/Core/Infrastructure/ProjectileManager.cs` (removed field + copy site)
- `Assets/Scripts/Core/Infrastructure/Brawler/BrawlerController.cs` (removed one set site; extracted `ResolveAndApplyCurrentBuild`; wired into Respawn)
- `Assets/Scripts/Core/Simulation/HybridProjectileLogic.cs` (removed set site)
- `Assets/Scripts/Core/Simulation/Abilities/ChainProjectileLogic.cs` (removed set site)
- `Assets/Scripts/Core/Simulation/Abilities/DeployableAbilityUser.cs` (removed set site)
- `Assets/Scripts/Core/Simulation/Abilities/Barley/ThrownVolleyAoEAbilityLogic.cs` (removed set site + stale comment)
- `Assets/Scripts/Core/Simulation/Abilities/Byron/ThrownHybridAoEAbilityLogic.cs` (removed set site)
- `Assets/Scripts/Core/Simulation/StatusEffects/BurnEffect.cs` (routed ticks through DamageService)
- `Assets/Scripts/Core/Simulation/Deployable/DeployableState.cs` (added `Controller` back-reference)
- `Assets/Scripts/Core/Simulation/Deployable/DeployableController.cs` (set `Controller` on state post-construction)
- `Assets/Scripts/Core/Simulation/Brawler/BrawlerState.cs` (updated Reset doc comment — KNOWN GAP → "gap closed by S4")
- `Assets/Scripts/Core/Simulation/Brawler/BrawlerLoadout.cs` (updated ResetRuntimeState doc comment)

**Decisions made**
- **Gap 1 went (a), not (b).** `ProjectileSpawnContext.SuperChargeOnHit` is gone. Revive-as-multiplier was considered but there's no caller that would consume it and no pressing design need — the new `SuperChargeSourceDefinition` hierarchy is the hook for future per-ability tuning. Carrying a dead field "just in case" is exactly the trap the refactor policy warns against.
- **Gap 3 went (b), not (a).** Reset continues to wipe RuntimeBuild/RuntimeKit; BrawlerController.Respawn re-applies. Keeping the wipe means installed ability logics (which carry transient per-life state — cooldown timers on the *logic* instance, pending coroutines, etc.) get rebuilt fresh every life. The preserve-through-Reset alternative would have forced us to audit every ability logic for respawn-safe in-place reset semantics; not worth it for a one-line controller change.
- **Extracted a helper rather than duplicating.** Two Respawn implementations would have drifted; the shared `ResolveAndApplyCurrentBuild` means any future change to build-resolution policy (new fallback tier, new resolver, new post-apply step) affects match-start and respawn identically by default.
- **New `DeployableState.Controller` back-ref.** Parallels `BrawlerState.Owner`. The alternative — looking up controller-by-state via a registry in `BurnEffect` — would have added a runtime cost to every burn tick and introduced a registry dependency in a system that otherwise knows nothing about registries.

**Learnings covered**
- **"Known gaps" aren't "accepted bugs".** The S3 refactor policy (document-in-place, don't silently fix) gets paired with a follow-up session that actually closes them. The gaps were documented with enough detail (options (a)/(b), affected call sites, knock-on effects) that the fix session could move directly to decisions without re-investigation. That's the intended cadence.
- **Extract-then-reuse beats copy-paste for multi-caller flows.** The Respawn fix became one method call because `InternalInitialize` was also refactored to call the same helper. Copying the resolution block into Respawn would have "worked" but would have created two drift-prone copies.
- **Fixing one bug sometimes exposes another.** Routing burn through DamageService fixed the super-charge feed *and* the missing-despawn-on-fatal-DoT for deployables. The bypass was hiding two bugs; removing it fixed both.
- **Back-reference pattern symmetry.** When `BrawlerState.Owner` already existed, adding `DeployableState.Controller` needed no new justification beyond "the same problem exists and the same solution fits". Precedent makes the review cheap.

**Open questions / notes for future Claude**
- **Burn tuning check.** BurnEffect now runs through the full damage pipeline, which means incoming damage modifiers apply to burn ticks (they didn't before). If any burn magnitudes were tuned assuming raw damage, expect them to feel different against brawlers with incoming-damage reduction modifiers. Not a bug, but a playtest item.
- **Other `TakeDamage` bypasses.** Grep now returns only the valid sites (`DamageService` pipeline + the state TakeDamage methods + `DeployableController.TakeDamage` — all correctly-routed callers). Any future effect that deals damage should route through `DamageService` rather than `state.TakeDamage`; enforce in review.
- **Deployable-as-attacker is still not wired.** `DeployableController` has no `.NotifyDamageDealt` hookup because deployables don't currently attribute damage to a brawler's super-charge sources. If turrets ever need to charge their owner's super, `DeployableAbilityUser`'s damage-applying sites need `ctx.Attacker = _owner.Owner` set (some already do).

**Next session goal**
- Finish Session 4: AIProfile assets (four distinct profiles — ranged-skirmisher / hybrid-support / summoner-zoner / area-zoner), Archetype enum fixes, BrawlerBuildResolver option-unlock verification. Then loadouts work (S5+).

## Session 5 — Archetype enum + BuildLayout wiring + resolver verification

### Enum extension
- `BrawlerArchetype` gained `Controller = 5` and `Artillery = 6` (append-only; explicit integer values added to prevent silent re-categorisation on reorder).
- Header comment clarifies naming-vs-Brawl-Stars mapping (Sniper == Marksman, Fighter == Damage Dealer).

### Brawler archetype fixes
- Colt: 2 (Sniper) — unchanged
- Byron: 0 → 3 (Support)
- Jessie: 0 → 5 (Controller)
- Barley: 3 → 6 (Artillery)

### BuildLayout wiring
- Discovered `StandardBrawlerBuildLayout.asset` already existed with the intended 5-slot shape (gear_1 PL8, gear_2 PL10, gadget_1 PL7, starpower_1 PL9, hypercharge_1 PL11).
- All 4 brawler definitions (Colt, Byron, Jessie, Barley) now reference this single shared asset — no duplicate BuildLayouts authored.
- Rationale: flyweight at the data layer. Balance passes on unlock thresholds are one-asset edits. Per-brawler option arrays stay per-brawler.

### Resolver verification
- Walked `TryResolveUnlockedOnly(Colt, build, PL)` at PL 1/7/9/11.
- At every PL: empty option arrays → empty build returned, no exceptions.
- Validated: resolver code path + PL-gating logic + layout wiring all functional. Content authoring is now a pure data-layer task.

### Known gaps (unchanged from Session 4)
- `AIUtilityScorer` IsX booleans don't cover Controller/Artillery — Option A: rely on AIProfile assets to override.
- `BrawlerAIProfile` switch doesn't cover Controller/Artillery — same Option A posture.
- #20 test assembly, #21 unit tests, #37 status-effect smoke test, #40 loadout smoke test — pending.

### Next session candidates
- Author 4 distinct AIProfile assets (Colt ranged-skirmisher, Byron hybrid-support, Jessie summoner-zoner, Barley area-zoner).
- Populate GadgetOptions / StarPowerOptions for the 4 brawlers.
- Unity test assembly bootstrap (#20).

---

### Session 5 (continued) — 2026-04-26 — Testing curriculum kickoff (6 fixtures, 159 tests, 2 precision bugs caught)

**Goals**
- Use the new EditMode test assembly (landed earlier in S5) to start a structured testing curriculum, with each fixture chosen to teach a distinct testing technique rather than just "cover code."
- Catch real bugs along the way; treat any precision/contract bug surfaced by a test as a forcing function to fix production rather than work around in test.
- Build a vocabulary of named patterns (POCO value testing, hand-rolled fakes, spy-on-hooks, static-state isolation, state-machine table testing, boundary-pair tests, crown jewels) Akash can apply to future fixtures unaided.

**Work done**

*BrawlerCooldownsTests (POCO value testing, baseline)*
- Tests confirm `BrawlerCooldowns` is a pure data/logic class — `new BrawlerCooldowns()` is the entire setup. No fakes, no fixtures, no static state.
- Covered: zero-tick reset, per-ability isolation (setting main-attack doesn't leak into super), `IsReady` boundary at the exact ready tick, multi-tick decay correctness.
- Crown jewel: per-ability isolation — the regression most likely to break gameplay if someone refactors the underlying storage from one-field-per-ability to a dictionary.

*HyperchargeTrackerTests + the 0.5s-at-30Hz precision bug*
- Wrote tests against `HyperchargeTracker.RemainingSeconds` and `IsActive`.
- A `Activate(0.5f)` test failed because `(uint)(0.5f * 30f)` truncated to 14, not 15. Production was off by one tick everywhere it converted seconds to ticks.
- Fix landed in production: introduced `SimulationClock.SecondsToTicks(float seconds)` returning `(uint)(seconds * TickRate + 0.5f)`, then migrated 13 call sites under pin-then-migrate (helper + focused fixture first, then migrations).
- Lesson surfaced: contract tests against published values *will* find truncation bugs that integration playtests never notice.

*BrawlerLoadoutPassiveLifecycleTests (spy-on-hooks)*
- 16 tests covering `BrawlerLoadout.InstallAll` / `UninstallAll`.
- Built `SpyPassive : PassiveDefinition` and `SpyRuntime : IPassiveRuntime`, both writing tagged events into a shared `CallLog.Events : List<string>`.
- Coverage: install cardinality, install order, runtime callback firing, install context propagation (target/owner threaded through `PassiveInstallContext`), unique source token per passive, uninstall reverse order (LIFO), uninstall reuses the same context object instance, runtime `OnUninstalled` fires *before* definition `Uninstall`, post-uninstall internal state cleared, empty loadout no-ops, dedupe semantics.
- Three crown jewels named in the doc-comment: reverse-order uninstall, same context object preserved across install→uninstall, runtime-before-definition uninstall ordering.
- Compile fix mid-flow: switched `CallLog.Events` from `IReadOnlyList<string>` to public `List<string>` so test assertions could call `IndexOf`.
- Structural luck noted: `BrawlerLoadout.InstallAll`/`UninstallAll` only use target/owner to construct `PassiveInstallContext` (a struct that just passes them through), and the runtime callbacks are null-conditional, so passing `(null, null)` keeps the test isolated from the wide `BrawlerState` graph.

*StatusEffectServiceTests (static-state isolation discipline)*
- 14 tests covering `StatusEffectService.Apply` (apply-vs-merge orchestrator).
- First fixture in the project to exercise the static-state isolation discipline: SUT touches four static surfaces (`ServiceProvider` for clock + combat log, `StatusEffectEventBus.OnStatusEffectApplied`, `IStatusTarget.ActiveStatusEffects`).
- `[SetUp]` clears `ServiceProvider`, registers `FakeClock` + `CombatLogService`, saves the event handler delegate to a field (`_appliedHandler`), subscribes it to the static event, and constructs the service. `[TearDown]` unsubscribes the *same delegate instance* (C# event removal silently no-ops on a different instance) and clears `ServiceProvider` again.
- Coverage: apply path (4 — adds instance, calls Apply on instance, fires event, logs), merge path (5 — invokes Merge on existing, doesn't add second instance, doesn't call Apply on new, doesn't fire event), apply-vs-merge decision branch, first-CanMerge-wins when multiple existing instances could merge, early-return guards (`CanReceiveStatusEffects()` false, `IsDead` true), real `SlowEffect.Merge` value contract (extends `EndTick` only when `newEndTick > EndTick`).
- Three crown jewels: merge path doesn't add second instance (the central invariant of merge semantics); first-CanMerge-wins (deterministic ordering matters under rollback); early-return short-circuits cleanly without touching list/event/log (otherwise dead/immune brawlers would still fire ghost-apply events).
- Skipped: "unknown StatusEffectType returns null" branch — `StatusEffectType` enum has no `None` value, so the branch isn't testable with normal enum values without polluting the enum for tests.

*BrawlerActionStateMachineTests (state-machine table testing + boundary pairs)*
- 19 test methods, 23 cases via `[TestCase]` parameterization. First fixture in the project to use NUnit `[TestCase]`.
- Coverage walks the (state × transition × condition) table: construction (2), Enter (3 — including force-replace over a non-interruptible state), Clear (1), UpdateExpiry (4), IsActive (2 boundary-pair), IsInState (3), TryInterrupt (4 methods × 8 cases via fan-out across the three castable state types).
- Two boundary-pair tests: `IsActive` at `LockUntilTick - 1` (true) vs `LockUntilTick` (false) — the strict `<` boundary; `IsInState` after entering then clearing.
- Four crown jewels: Enter is force-replace even over non-interruptible states (so external systems like death/forced-move can always overwrite); `IsActive` boundary at `LockUntilTick` (strict `<`, not `<=`); `TryInterrupt` is tick-blind (only checks `IsInterruptible`, not whether the state has expired); struct-replace canary (Enter overwrites the whole `BrawlerActionStateData` struct rather than mutating fields, so all flags propagate atomically).
- No fakes needed — `BrawlerActionStateMachine` is pure POCO with method-parameter ticks. This was the cleanest fixture to write and the highest case-density per line of test code.

*Tests summary*
- 6 fixtures, ~159 tests, all green.
- 2 precision bugs caught and fixed in production (the 0.5s tick-truncation; surfaced by the HyperchargeTracker fixture and propagated via the new `SimulationClock.SecondsToTicks` helper).
- 13 production sites migrated to `SimulationClock.SecondsToTicks` under pin-then-migrate.

**Files touched (this continuation)**

Production:
- `Assets/Scripts/Core/Simulation/SimulationClock.cs` — added `public static uint SecondsToTicks(float seconds) => (uint)(seconds * TickRate + 0.5f);`
- 13 call sites migrated to the helper (cooldowns, hypercharge, status-effect duration conversions, etc.)

Tests (new):
- `Assets/Tests/EditMode/BrawlerCooldownsTests.cs` (+ .meta)
- `Assets/Tests/EditMode/HyperchargeTrackerTests.cs` (+ .meta)
- `Assets/Tests/EditMode/SimulationClockTests.cs` (+ .meta) — focused fixture for the new helper
- `Assets/Tests/EditMode/BrawlerLoadoutPassiveLifecycleTests.cs` (+ .meta) — guid 7ef0123456789abcdef01234567890ab
- `Assets/Tests/EditMode/StatusEffectServiceTests.cs` (+ .meta) — guid 8f0123456789abcdef0123456789abcd
- `Assets/Tests/EditMode/BrawlerActionStateMachineTests.cs` (+ .meta) — guid 90123456789abcdef0123456789abcde

**Decisions made**
- *Test against published contracts, not current implementation.* If a value gets recomputed differently internally, tests should still pass as long as the public contract holds. Conversely, if a test would only pass for the current implementation, it's coupled to internals and should be rewritten or deleted.
- *Hand-rolled fakes over Moq.* For the small interfaces in this codebase (`ISimulationClock`, `ICombatLogService`, `IStatusTarget`, `IPassiveRuntime`), a 20-line POCO fake is easier to read and debug than a Moq setup chain. Defer Moq until/unless we hit a collaborator with 10+ methods.
- *Spy-on-hooks over per-instance counters.* When testing pipelines that act on multiple instances in order, a single shared chronological log catches "out of order" bugs that per-instance counters miss.
- *Static-state isolation in SetUp/TearDown is non-negotiable.* Any fixture that touches `ServiceProvider`, `StatusEffectEventBus`, or any other static singleton must clear in SetUp, register fakes, and undo in TearDown — including saving the event-handler delegate to a field so the unsubscribe targets the same instance.
- *Crown jewels named in fixture doc-comments.* Top-of-file comment block lists the 1–4 tests that, if they break, mean "stop and think" rather than "update the test."
- *Pin-then-migrate for new helpers.* Helper + focused fixture lands first; site migrations go in a follow-up commit (or commits) so each is reviewable in isolation.

**Learnings covered**
- POCO value testing as the cheapest, most maintainable form of unit test — preferred whenever the SUT has no static or Unity dependencies.
- The hand-rolled fake idiom and when it's cheaper than a mocking framework.
- Spy-on-hooks: shared chronological log + tagged appends from each overridden hook = strongest possible assertion of pipeline order across multiple instances.
- Static-state isolation discipline, including the same-delegate-instance gotcha for C# events.
- State-machine table testing — exhaustive (state × transition × condition) coverage via `[TestCase]` fan-out instead of copy-paste.
- Boundary-pair tests for strict inequalities — assert both sides in one method so `<` vs `<=` typos can't slip through.
- Crown jewel naming as living documentation of which tests are load-bearing.
- Pin-then-migrate as a refactor pattern for new helpers — the helper proves itself in isolation before site migrations.
- Round-to-nearest tick conversion (`+ 0.5f` before cast) as the standard fix for seconds → ticks truncation bugs.

**Open questions**
- `StatusEffectType` enum has no `None` value, so the "unknown type returns null" branch in `StatusEffectService.Apply` isn't easily testable without polluting the enum. Worth adding `None = 0`? Trade-off: defensive enum hygiene vs. risk that some `default(StatusEffectType)` site silently becomes `None`.
- `BrawlerActionStateMachine` — should `TryInterrupt` actually check whether the state has expired (currently tick-blind)? Documented as a crown jewel observation rather than a bug, but worth a design conversation.
- Some fixtures still want a Unity in-engine smoke pass (#37 status effects, #40 loadout) even though EditMode coverage is now strong. EditMode validates contracts; in-engine validates wiring (ScriptableObject GUIDs, actual subsystem boot order). Don't conflate the two.

**Next session goal**
- Pick from: more tier-3 testing (BrawlerStealth, rest of BrawlerLoadout helpers, `BrawlerBuildLayoutDefinition.IsSlotUnlocked`); deferred deployable migrations under pin-then-migrate (5 sites: DeployableController, DeployableState, BuffZone, Turret, HealingStation); or back to gameplay/AI work (AIUtilityScorer Controller/Artillery cases). Akash to choose.

---
