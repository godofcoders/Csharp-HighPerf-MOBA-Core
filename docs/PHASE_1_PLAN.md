# Phase 1 — Offline Vertical Slice

Status: active
Owner: Akash
Started: 2026-04-17

---

## Progress as of 2026-04-28

The codebase has run ahead of this plan. The original session-by-session outline assumed content authoring (gadgets, star powers, hypercharges) would happen in dedicated sessions 5–9. In practice, the loadout content was authored opportunistically alongside other work, and Sessions 5–9's content-authoring scope is effectively complete.

**What's authored and wired:**
- 4 brawlers, each with: main attack ✅ super ✅ 2 gadgets ✅ 2 star powers ✅ 1 hypercharge ✅ AIProfile ✅ DefaultBuild ✅ Archetype assigned ✅
- Standard 5-slot BuildLayout (gear_1 PL8, gear_2 PL10, gadget_1 PL7, starpower_1 PL9, hypercharge_1 PL11), shared across all 4 brawlers via flyweight
- Architecture warmup (Sessions 2–3) complete: tick-phase refactor, BrawlerState decomposed into 7 substates
- Super-charge pipeline complete (Session 4): extensible `SuperChargeSourceDefinition` / `SuperChargeSourceRuntime` system + 4 source types (DamageDealt, HealingDone, AllyProximity, AutoOverTime); push-based notification from damage/heal pipelines
- Test infrastructure complete (Session 5 across two days): 8 EditMode fixtures, 238 tests, methodology documented in `docs/TESTING.md`
- Two precision bugs found and fixed: `SimulationClock.SecondsToTicks` helper rolled out to 18 production sites total
- AIUtilityScorer extended for Controller/Artillery archetypes (closing the Session 5 gap)

**What's NOT verified (authored but unplaytested):**
- Whether the gadgets / star powers / hypercharges actually fire end-to-end in-engine. EditMode tests cover contracts; in-engine smoke is its own confidence check. Two parked Unity smoke-test tasks (status effects #37, loadout #40) remain.
- Whether the brawler kits feel distinct in playtest. Plan Session 10 (full-brawler integration playtest) is the natural place to find this out.

**Cleanup carried forward:**
- `Blaze_*` archival files (`Blaze_BrawlerDefinition.asset`, `Blaze_Gadget_Dash.asset`, `Blaze_Gadget_HealBurst.asset`, `Blaze_OverdriveHypercharge.asset`) at the root of `Assets/Scriptables/` — flagged for deletion in Session 1, never removed. Safe to delete: not referenced by any current brawler.
- `GadgetChargeState` mentioned in this plan does not exist by that name. Charge tracking lives across `BrawlerCooldowns` / `BrawlerResources` / `BrawlerLoadout` after the Session 3 decomposition. The plan's wording is stale; functionality is fine.

**Remaining Phase 1 work** (rough mapping to original plan rows):
| Theme | Plan rows | Estimated remaining sessions |
|-------|-----------|------------------------------|
| Integration playtest | 10 | 1 (Akash hands-on in editor) |
| Gem Grab game mode | 11–14 | 4 |
| Camera + input polish | 15–16 | 2 |
| HUD | 17–18 | 2 |
| Game feel | 19–20 | 2 |
| Slice review + 90-second video | 21 | 1 |

So ~12 sessions of substantive remaining work, plus the parked Unity smokes. Versus the original projection of 21 total sessions, actual count is tracking ~18 (5 done with 2 continueds = 7 effective sessions; ~12 remaining).

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

| # | Theme | Status | What happens | Primary teaching |
|---|-------|--------|--------------|------------------|
| 1 | Kickoff | ✅ done | Plan, session log, key decisions, brawler + system audits | How to run a focused learning project |
| 2 | Tick phases | ✅ done | Refactor `SimulationRegistry` into explicit phases: PreTick → InputApply → AbilityCast → Movement → Collision → DamageResolution → StatusEffectTick → Cleanup → PostTick. Add `TickPhase` enum and phased registration API. Also fix `HyperchargeTracker` hardcoded 30 TPS. | Why ordered simulation phases beat insertion-order iteration |
| 3 | State decomposition + first tests | ✅ done (expanded to 7 substates, not 4) | Split `BrawlerState` into `BrawlerStats`, `BrawlerCooldowns`, `BrawlerActionStateMachine`, `BrawlerLoadout`, `BrawlerResources`, `BrawlerStealth`, `BrawlerStatusEffects`. Add Unity test assembly. Write first unit tests. | Cohesion vs coupling; testing pure functions |
| 4 | Brawler fix pass | ✅ done | Author Barley's super. Design + author 4 distinct `BrawlerAIProfile` assets and wire each brawler. Fix `Archetype` enums. Verify `BrawlerBuildResolver` option-unlock logic. Plus: super-charge pipeline (out-of-plan, Session 4 continued). | Utility AI tuning via data |
| 5 | Gadgets pt. 1 | ✅ done out-of-order | Gadget assets authored opportunistically. All 4 primary gadgets exist: Colt Speedloader (AmmoRefill), Byron Booster Shots (AllyHealPulse), Jessie Power Surge (SuperCharge), Barley Last Drop (SuperCharge). | Gadget system end-to-end |
| 6 | Gadgets pt. 2 | ✅ done out-of-order | Second gadgets exist: Colt Quick Step (Dash), Byron Shot in the Arm (HealBurst), Jessie Energize (AllyHealPulse), Barley Herbal Tonic (HealBurst). All 8 wired into GadgetOptions. | Option-based loadout validation |
| 7 | Star Powers pt. 1 | ✅ done out-of-order | First star power authored per brawler. Filenames use `_SP_` prefix: Colt SlickBoots, Byron Malaise, Jessie Shocky, Barley ExtraNoxious. | Passive-runtime install/uninstall lifecycle |
| 8 | Star Powers pt. 2 | ✅ done out-of-order | Second star power per brawler: Colt MagnumSpecial, Byron Injection, Jessie Reconstruction, Barley MedicalUse. All 8 wired into StarPowerOptions. | Loadout validation rules |
| 9 | Hypercharges | ✅ done out-of-order | All 4 hypercharge assets authored: Colt, Byron, Jessie, Barley. Each wired into HyperchargeOptions. Enhanced-super swap behaviour authored per brawler. | Hypercharge activation + enhanced-super swap |
| 10 | Full-brawler integration playtest | ⏳ next | Play 1v1 and 1v3 bot matches with every brawler. Do they feel distinct? Do AI profiles behave differently? Do gadgets / star powers / hypercharges actually fire? Tune. | Playtesting as a refinement loop |
| 11–12 | Gem Grab: objective scaffolding | ⏳ pending | Gem-spawner entity, gem pickups, carrier state on brawler, cashout logic. Extend `MatchManager` with Gem Grab-specific state and scoring. | Match-lifecycle state machine; objective entities |
| 13–14 | Gem Grab: objective-aware AI + win condition | ⏳ pending | Teach AI to prioritize gems and protect the carrier. Use `AITeamBlackboard` for "protect carrier" / "deny gems" signals. 60-second win-condition timer. | How objective AI differs from combat AI |
| 15–16 | Camera + input polish | ⏳ pending | Top-down follow camera with smoothing and dead zones. Keyboard/mouse control scheme. Input rebinding. | Critically-damped springs; input buffering |
| 17–18 | HUD | ⏳ pending | Health bars (world-space), super-charge ring, ammo indicators, cooldowns, gem-carrier count, scoreboard, match timer, gadget/hypercharge UI. | UI data bindings; decoupling presentation via event buses |
| 19–20 | Game feel | ⏳ pending | Hit stop, damage numbers, screen shake, projectile trails, hit sparks, gem-collect feedback, hypercharge-activation VFX. | Why "juice" sells the slice |
| 21 | Slice review | ⏳ pending | Record 90 s video, assess vs exit criteria, list Phase 2 entry criteria. | Honest self-evaluation |

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

## Brawler-content audit (Session 1 snapshot — preserved for reference)

| Brawler | Main | Super | Gadget | StarPower | Hyper | AIProfile | Archetype | Notes |
|---------|------|-------|--------|-----------|-------|-----------|-----------|-------|
| Colt | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | 0 (unset) | Ranged linear |
| Byron | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | 0 (unset) | Hybrid — dedicated `HybridAoEAbilityLogic.cs` |
| Jessie | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | 0 (unset) | Summoner — Scrappy deployable |
| Barley | ✅ | ❌ **missing** | ❌ | ❌ | ❌ | ❌ | 3 | Area zoner — puddle hazard effect present |

## Brawler-content audit (2026-04-28 snapshot — current reality)

| Brawler | Main | Super | Gadget 1 | Gadget 2 | StarPower 1 | StarPower 2 | Hyper | AIProfile | Archetype | DefaultBuild |
|---------|------|-------|----------|----------|-------------|-------------|-------|-----------|-----------|--------------|
| Colt | ✅ | ✅ | Speedloader (AmmoRefill, full reload) | Quick Step (Dash, force 4.0) | SlickBoots | MagnumSpecial | ✅ | ✅ ranged-skirmisher | 2 (Sniper) | ✅ |
| Byron | ✅ | ✅ | Booster Shots (AllyHealPulse, 500/4r) | Shot in the Arm (HealBurst self 600) | Malaise | Injection | ✅ | ✅ hybrid-support | 3 (Support) | ✅ |
| Jessie | ✅ | ✅ | Power Surge (SuperCharge +25%) | Energize (AllyHealPulse, 400/4r) | Shocky | Reconstruction | ✅ | ✅ summoner-zoner | 5 (Controller) | ✅ |
| Barley | ✅ | ✅ | Last Drop (SuperCharge +20%) | Herbal Tonic (HealBurst self 700) | ExtraNoxious | MedicalUse | ✅ | ✅ area-zoner | 6 (Artillery) | ✅ |

All four brawlers carry the standard `StandardBrawlerBuildLayout` (gear_1 PL8, gear_2 PL10, gadget_1 PL7, starpower_1 PL9, hypercharge_1 PL11) via flyweight.

**System infrastructure audit (originally noted, all present):** `GadgetDefinition` family (`AmmoRefillGadgetDefinition`, `DashGadgetDefinition`, `HealBurstGadgetDefinition`, `AllyHealPulseGadgetDefinition`, `SuperChargeGadgetDefinition`); corresponding `GadgetLogic` classes; `GadgetLockStatusEffect`; `HyperchargeDefinition` + `HyperchargeTracker`; `StarPowerDefinition`; `PassiveDefinition` + family + loadout rules + validation; `IPassiveRuntime`. Plus added since: extensible `SuperChargeSourceDefinition` system + 4 source types.

**Known smells originally noted, status update:**
- ✅ Resolved: `HyperchargeTracker.Activate()` hardcoded `30f` — fixed in Session 2; `SimulationClock.SecondsToTicks` helper introduced in Session 5 day 1; rolled out to 18 production sites total across S5 days 1–2.
- ⏳ Outstanding: `Blaze_*` archival files at the root of `Assets/Scriptables/` (`Blaze_BrawlerDefinition.asset`, `Blaze_Gadget_Dash.asset`, `Blaze_Gadget_HealBurst.asset`, `Blaze_OverdriveHypercharge.asset`). Safe to delete — confirmed not referenced by any current brawler. Recommended cleanup in Session 10 or anytime convenient.
- ⏳ Outstanding: Two parked Unity in-engine smoke tests (#37 status effects, #40 loadout). EditMode coverage is now strong but in-engine smoke validates a different layer.

**Gear deferred** out of Phase 1; it's meta-game-adjacent and not required for the slice. Slot infrastructure (gear_1, gear_2) exists in `StandardBrawlerBuildLayout` for future hookup.

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
