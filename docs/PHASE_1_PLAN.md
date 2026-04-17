# Phase 1 — Offline Vertical Slice

Status: active
Owner: Akash
Started: 2026-04-17

---

## Framing

The project is a hobby/learning codebase modeled after AAA MOBA architecture. Phase 1 is strictly **offline** and focuses on **gameplay systems**. Multiplayer, determinism-grade fixed-point math, and meta/progression systems are explicitly deferred to later phases.

## Goal

Produce an offline-playable slice that demonstrates the core gameplay systems working end-to-end:

- Two distinct brawlers with contrasting ability archetypes
- One complete game mode with objectives and scoring
- A clean match lifecycle (lobby → countdown → play → end)
- A minimal, readable HUD
- A light architecture pass that cashes in on AAA-style patterns the codebase is already reaching toward

## Out of scope (deferred)

- Multiplayer networking → Phase 3 (target: Netcode for GameObjects, free)
- Fixed-point math / strict determinism → Phase 2 (required prerequisite for networking)
- Meta game (progression, matchmaking, shop, battle pass) → Phase 4
- Content scale (30 brawlers, 20+ maps) → Phase 5
- Mobile touch controls → optional late in Phase 1

## Exit criteria

A recordable 90-second gameplay video showing:

1. Full match lifecycle: lobby → countdown → play → end
2. Two brawlers with distinct ability archetypes, player vs bots
3. Game mode objective being scored
4. HUD showing health, ammo/reload, super charge, cooldowns, score, match timer
5. AI that plays the mode, not just fights (prioritizes objectives)
6. No egregious frame spikes on desktop

## Session-by-session outline

Revised after Session 1 brawler-content audit and loadout-scope decision. Four brawlers already scaffolded; Phase 1 now includes a **full loadout authoring pass** (2 gadgets + 2 star powers + 1 hypercharge per brawler = 20 loadout assets) before the game-mode work.

| # | Theme | What happens | Primary teaching |
|---|-------|--------------|------------------|
| 1 | Kickoff | Plan, session log, key decisions, brawler + system audits | How to run a focused learning project |
| 2 | Tick phases | Refactor `SimulationRegistry` into explicit phases: PreTick → InputApply → AbilityCast → Movement → Collision → DamageResolution → StatusEffectTick → Cleanup → PostTick. Add `TickPhase` enum and phased registration API. Also fix `HyperchargeTracker` hardcoded 30 TPS. | Why ordered simulation phases beat insertion-order iteration |
| 3 | State decomposition + first tests | Split `BrawlerState` into `BrawlerStats`, `BrawlerCooldowns`, `BrawlerActionState`, `BrawlerLoadout`. Add Unity test assembly. Write first unit tests for damage math and modifier ordering. | Cohesion vs coupling; testing pure functions |
| 4 | Brawler fix pass | Author Barley's super. Design + author 4 distinct `BrawlerAIProfile` assets (ranged-skirmisher, hybrid-support, summoner-zoner, area-zoner) and wire each brawler. Fix `Archetype` enums. Verify `BrawlerBuildResolver` option-unlock logic. | Utility AI tuning via data |
| 5 | Gadgets pt. 1 (core authoring) | Verify `GadgetChargeState` (per-gadget charge tracking). Author 4 primary gadgets — one per brawler. Reuse `DashGadgetLogic` / `HealBurstGadgetLogic` where they fit; author new logics only where needed. Wire to `GadgetOptions` on each brawler. | Gadget system end-to-end |
| 6 | Gadgets pt. 2 (variety + options) | Author the second gadget per brawler (total 8). Validate `GadgetOptions` list shows up in `BrawlerBuildResolver`. Playtest choice swap. | Option-based loadout validation |
| 7 | Star Powers pt. 1 | Audit `PassiveDefinition` / `StarPowerDefinition` / `IPassiveRuntime`. Author 4 star powers (one per brawler, passive stat/behavior tweaks). Wire to `StarPowerOptions`. | Passive-runtime install/uninstall lifecycle |
| 8 | Star Powers pt. 2 | Author second star power per brawler (total 8). Verify `PassiveLoadoutRules` validation rejects conflicting picks. | Loadout validation rules |
| 9 | Hypercharges | Author 1 hypercharge per brawler (total 4). Each with enhanced-super swap where meaningful (Byron: bigger heal radius, Jessie: turret gets shield, Colt: pierce, Barley: larger puddle). Tune stat buffs. | Hypercharge activation + enhanced-super swap |
| 10 | Full-brawler integration playtest | Play 1v1 and 1v3 bot matches with every brawler. Do they feel distinct? Do AI profiles behave differently? Tune. | Playtesting as a refinement loop |
| 11–12 | Gem Grab: objective scaffolding | Gem-spawner entity, gem pickups, carrier state on brawler, cashout logic. Extend `MatchManager` with Gem Grab-specific state and scoring. | Match-lifecycle state machine; objective entities |
| 13–14 | Gem Grab: objective-aware AI + win condition | Teach AI to prioritize gems and protect the carrier. Use `AITeamBlackboard` for "protect carrier" / "deny gems" signals. 60-second win-condition timer. | How objective AI differs from combat AI |
| 15–16 | Camera + input polish | Top-down follow camera with smoothing and dead zones. Keyboard/mouse control scheme. Input rebinding. | Critically-damped springs; input buffering |
| 17–18 | HUD | Health bars (world-space), super-charge ring, ammo indicators, cooldowns, gem-carrier count, scoreboard, match timer, gadget/hypercharge UI. | UI data bindings; decoupling presentation via event buses |
| 19–20 | Game feel | Hit stop, damage numbers, screen shake, projectile trails, hit sparks, gem-collect feedback, hypercharge-activation VFX. | Why "juice" sells the slice |
| 21 | Slice review | Record 90 s video, assess vs exit criteria, list Phase 2 entry criteria. | Honest self-evaluation |

**Projected Phase 1 length:** ~21 sessions. Assumes ~1.5–2 hours each.

## Where we start

Recommendation: **architecture warmup first (Sessions 2–3), then feature work (Sessions 4+).**

Rationale: Sessions 2–3 are short, mechanical, and unblock everything downstream. Explicit tick phases change how every new feature gets wired in. Splitting `BrawlerState` makes the second brawler easier to author. Adding a test harness now means every subsequent change is verifiable. Doing this after building features would require revisiting all that code.

## Key decisions

| # | Decision | Choice | Locked in session |
|---|----------|--------|-------------------|
| 1 | Phase 1 game mode | **Gem Grab** | 1 |
| 2 | Second brawler (authoring) | **Not needed** — four brawlers already scaffolded (Colt, Byron, Jessie, Barley). Replaced with differentiation pass in Session 4. | 1 |
| 3 | Starting sequence | **Architecture warmup first** (Sessions 2–3), then brawler loadouts (Sessions 4–10), then game mode | 1 |
| 4 | Loadout scope | **Full loadout** — 2 gadgets + 2 star powers + 1 hypercharge per brawler (20 loadout assets total) | 1 |
| 5 | Networking stack (future) | Unity Netcode for GameObjects (Phase 3+) | 1 |

## Brawler-content audit (Session 1 snapshot)

| Brawler | Main | Super | Gadget | StarPower | Hyper | AIProfile | Archetype | Notes |
|---------|------|-------|--------|-----------|-------|-----------|-----------|-------|
| Colt | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | 0 (unset) | Ranged linear |
| Byron | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | 0 (unset) | Hybrid — dedicated `HybridAoEAbilityLogic.cs` |
| Jessie | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | 0 (unset) | Summoner — Scrappy deployable |
| Barley | ✅ | ❌ **missing** | ❌ | ❌ | ❌ | ❌ | 3 | Area zoner — puddle hazard effect present |

**System infrastructure audit (exists, no work needed):** `GadgetDefinition` (with `MaxCharges`), `DashGadgetLogic`, `HealBurstGadgetLogic`, `AllyHealPulseGadgetLogic`, `GadgetLockStatusEffect`, `HyperchargeDefinition` (with enhanced-super swap), `HyperchargeTracker`, `StarPowerDefinition`, `PassiveDefinition` + family + loadout rules + validation, `IPassiveRuntime`.

**Known smells to clean up opportunistically:**
- `HyperchargeTracker.Activate()` hardcodes `30f` (TPS) at line 31 — should read from simulation clock
- Deprecated `Assets/Scripts/Data/Scriptables/Colt.asset` + root-level `Blaze_*.asset` look archival; don't use as canonical

**Gear deferred** out of Phase 1; it's meta-game-adjacent and not required for the slice.

## Learnings agenda

Topics we will hit over Phase 1, in roughly this order:

- Why explicit tick phases beat insertion-order iteration
- Stat modifier math: additive vs multiplicative, order of operations, source tracking
- Strategy pattern for abilities, and where it stops scaling
- Utility AI vs FSM vs behavior trees; why twin-stick arena games lean utility
- Object pooling for projectiles/VFX and why allocations wreck mobile
- Event buses vs direct coupling, when each is correct
- Camera smoothing math (critically damped springs)
- Game feel micro-mechanics: hit stop, camera kick, screen shake, damage numbers
- Match lifecycle as an explicit state machine
- Writing a deterministic-friendly simulation even before going fixed-point
