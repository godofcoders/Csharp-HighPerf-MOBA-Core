# MOBA Gameplay Roadmap

Updated: 2026-06-15

This roadmap is the forward source of truth for the next gameplay, UI, content, and polish passes. The rule for every item is the same as the AI work: build real systems, not temporary patches. Each milestone should finish with a short manual QA checklist and a focused commit.

## Status Legend

- `[ ]` Not started
- `[~]` In progress / needs playtest
- `[x]` Done

## Current Validation Queue

Started: 2026-06-10

These items are active now. We validate and fix the most disruptive gameplay-feel issues first, before stacking more behavior on top.

- `[~]` Held aim and auto-aim behavior
  - Tap should fire auto-aim without showing preview.
  - Hold should show aim preview after a short delay.
  - Releasing held aim while moving should fire in the held aim direction, not movement direction.
  - Super aim preview should use a distinct color.
  - `[~]` Aim preview should be stable while holding aim, with no visible jitter. Held preview now falls back to stable aim direction, smooths direction/origin/range, and uses physics obstacle clipping before grid fallback; needs Unity feel check.
  - `[~]` Aim preview should not show a sphere/end marker unless the ability is actually point-targeted. Directional marker removed in code; needs Unity visual confirmation.

- `[~]` AI movement realism
  - Bots should move less twitchily.
  - Dodges should feel readable and fair, not instant-perfect.
  - Player movement should remain unaffected.

- `[~]` Projectile/shooting readability
  - Projectile prefabs should be realistically sized for their attack width.
  - Projectile visuals should have trails/impact effects where appropriate.
  - `[~]` Colt-style multi-shot attacks should keep separate bullet lanes instead of collapsing into one local-looking shot. Straight multi-projectile logic now supports aim-aligned forward offset, parallel lanes, and per-shot cadence; Colt asset has explicit lane/burst tuning; needs Unity playtest.
  - Player movement should not bend or offset bullet direction after firing.

- `[~]` Gem readability
  - Gem prefab should visually read as a gem, not a placeholder.
  - Multiple dropped gems should scatter on the ground with readable spacing.

## Milestone G-01: Hypercharge Completion

Goal: make hypercharge feel like a complete, readable combat system rather than only a stat/runtime hook.

- `[~]` Verify charge gain from all intended sources. Hypercharge now gains from the same brawler-level super-charge grant path using per-hypercharge charge multipliers; needs Unity balance QA.
- `[~]` Verify activation rules: available, blocked, consumed, interrupted, death/reset behavior. Missing-definition blocks now report correctly; reset/expiry cleanup remains on existing runtime path.
- `[~]` Finish per-brawler hypercharge tuning. Colt, Barley, Jessie, and Byron have first-pass hypercharge assets plus enhanced-super links.
- `[~]` Verify enhanced-super swap behavior for each brawler. Enhanced super assets are data-wired for the current four brawlers; needs Unity activation/super QA.
- `[~]` Add HUD state: charge meter, ready state, activation state, duration countdown. Active hypercharge now displays remaining seconds and fill-down progress.
- `[~]` Add activation feedback: VFX, SFX hook, camera impulse, character highlight. Runtime character highlight/aura is wired; SFX/camera impulse still future polish.
- `[~]` Add end feedback: cleanup VFX, state return, no lingering modifiers. Runtime highlight cleans up on end/death/disable; manual expiry/death QA still open.
- `[ ]` Add debug/validation hooks for charge, activation, expiry, enhanced-super usage.
- `[ ]` Manual QA: activate, die during activation, respawn, use enhanced super, verify no duplicate modifiers.

## Milestone G-02: Core Combat And Aiming Polish

Goal: make player combat feel reliable and readable.

- `[~]` Thick Brawl Stars-style directional aim preview.
- `[~]` Held aim vs tap auto-aim behavior.
- `[~]` Wall-aware aim preview clipping when blockers are in front. Directional preview now resolves obstacle masks from inspector, map generator, or the authored `Obstacles` layer, handles near-cover overlap, then uses grid fallback only when no physics mask exists; needs Unity playtest.
- `[~]` Auto-aim should prefer valid targets and avoid bad wall angles. Direct projectile auto-aim now requires line-of-sight; needs Unity playtest around walls.
- `[~]` Projectile visuals should always spawn and travel consistently.
- `[~]` Projectile spawn direction should be independent of player movement after cast. Straight projectile spawn origin now derives from aim direction, and brawlers hold cast-facing through short attack/burst windows.
- `[~]` Multi-projectile attacks should spawn in distinct lanes/spread positions when authored that way.
- `[~]` Aim preview should be smoothed enough to avoid camera/mouse jitter without feeling delayed.
- `[~]` Remove end marker/sphere from directional preview; keep endpoint markers only for throwable/placement abilities.
- `[~]` Improve projectile hit feedback and lifetime cleanup. Projectile impact/expiry events now drive pooled visual pulses, and pooled projectile visuals reset on despawn.
- `[~]` Tune action buffering and attack lock feel while moving. Action buffering is now tick-based, preserves near-ready commands through short locks/cooldowns, and executes before movement so lock flags apply on the cast-start tick.
- `[ ]` Manual QA: shoot while moving, shoot around walls, tap auto-aim, hold aim, super aim, projectile visibility.

## Milestone G-03: Grass / Stealth System

Goal: make hiding in grass a real gameplay mechanic with simulation, AI, and presentation support.

- `[~]` Verify or rebuild bush/grass volume detection. Visibility now ticks from baked `MapData.BushGrid` each simulation tick; needs Unity validation against authored grass volumes.
- `[x]` Define stealth rules: hidden while inside grass, revealed by attack, revealed by damage, and status reveal. Close-range enemy proximity reveal is supported by config but disabled by default.
- `[x]` Add team visibility rules: self/allies visible, enemies hidden unless revealed.
- `[x]` Integrate stealth with AI perception and target memory.
- `[~]` Add visual feedback: brawler visuals now fade through runtime transparent material clones, enemies hide from the local player's team perspective, and friendly/self hidden state shows a subtle ground indicator; needs Unity readability QA.
- `[x]` Add debug visibility for hidden/revealed state.
- `[ ]` Manual QA: enter grass, shoot from grass, take damage in grass, enemy bot loses/reacquires target correctly.

## Milestone G-04: Breakable Objects / Destructible Cover

Goal: support destructible map objects with clear simulation rules and visual feedback.

- `[x]` Add breakable object definition: health, collision, team neutrality, destruction rules.
- `[x]` Add breakable object controller lifecycle and registry integration.
- `[x]` Define which attacks can damage breakables.
- `[~]` Make Colt super destroy eligible breakable objects. Breakables can require super damage and an optional exact ability asset; Colt super asset wiring needs Unity authoring.
- `[x]` Update projectile collision and area/super interactions.
- `[x]` Update navigation/grid/pathing when cover is destroyed.
- `[~]` Add visual feedback: hit flash, critical-health tint, destroyed visual prefab hook; cracks/debris/audio remain future polish.
- `[ ]` Manual QA: damage object, destroy object, verify bots and projectiles route through destroyed cover.

## Milestone G-05: Solo Showdown Mode

Goal: add a free-for-all/survival game mode as a second major gameplay showcase.

- `[x]` Add Solo Showdown mode definition and match rules.
- `[x]` Add free-for-all team/targeting behavior or temporary per-player team mapping.
- `[x]` Add spawn logic for multiple solo contestants.
- `[x]` Add win condition: last brawler alive.
- `[~]` Add poison/shrinking danger zone system. Runtime poison-zone component is available on the Solo Showdown mode prefab; needs Unity radius/damage tuning.
- `[ ]` Add power cube or pickup progression if scoped in.
- `[~]` Add Solo Showdown AI macro behavior: survive, third-party, retreat from poison, value pickups. Survival/final-duel/poison macro is implemented; pickup value waits on power cubes.
- `[~]` Add scoreboard/result screen handling for placement. Winner and internal placements are tracked; richer placement/result UI remains future polish.
- `[ ]` Manual QA: match starts, bots fight each other, poison forces movement, match ends cleanly.

## Milestone G-06: HUD / Match Feedback

Goal: make the match readable for a player and for project review/demo.

- `[~]` Health bars above friendly/enemy brawlers.
- `[ ]` Verify health bars update immediately on damage, heal, death, respawn.
- `[ ]` Show team gem counts and carrier gem counts clearly.
- `[ ]` Show match timer and countdown state.
- `[ ]` Show opening countdown: 3, 2, 1, start.
- `[ ]` Add kill feed / combat log presentation.
- `[ ]` Show ammo/reload state.
- `[ ]` Show super and hypercharge readiness.
- `[ ]` Gem pickup/drop visuals should clearly show gem count and ownership.
- `[ ]` Manual QA: damage, heal, death, gem pickup/drop, countdown, kill log, respawn.

## Milestone G-07: Game Feel Pass

Goal: add the feedback layer that makes the systems feel alive.

- `[ ]` Damage numbers.
- `[ ]` Hit flash / hurt flash.
- `[ ]` Projectile trails and impact effects.
- `[ ]` Projectile prefab scale pass so bullets match gameplay width and character scale.
- `[ ]` Hit stop for heavy/super hits.
- `[ ]` Camera shake/impulse for impactful actions.
- `[ ]` Gem pickup/drop feedback with scattered ground placement for multi-gem drops.
- `[ ]` Hypercharge activation feedback.
- `[ ]` Basic audio hooks for attack, hit, death, pickup, super, hypercharge.
- `[ ]` Manual QA: effects are readable but not noisy, no missing references, no frame spikes from VFX.

## Milestone G-08: Brawler Kit Completion

Goal: make each authored brawler feel distinct and complete.

- `[ ]` Colt: line pressure, super wall/object destruction, bullet trail clarity.
- `[ ]` Jessie: bounce readability, turret lifecycle, turret target feedback.
- `[ ]` Byron: heal-vs-damage clarity, ally targeting, projectile feedback.
- `[ ]` Barley: area denial visuals, puddle ownership clarity, throw arc readability.
- `[ ]` Verify 2 gadgets, 2 star powers, and 1 hypercharge per brawler in real play.
- `[ ]` Manual QA: one match per brawler using each ability/build path.

## Milestone G-09: Map And Mode Content Polish

Goal: make maps support gameplay, AI, and future modes cleanly.

- `[ ]` Author grass zones.
- `[ ]` Author breakable cover.
- `[ ]` Replace placeholder gem visuals with a readable gem prefab.
- `[ ]` Add gem drop scatter rules for deaths with multiple carried gems.
- `[ ]` Author lanes/chokes/cover semantics where needed.
- `[ ]` Add mode-specific spawn sets: Gem Grab teams, Solo Showdown solo spawns.
- `[ ]` Add map validation tooling for invalid cells, unreachable spawns, bad objective placement.
- `[ ]` Manual QA: no stuck spawns, no unreachable objectives, no boundary-stalling hotspots.

## Milestone G-10: Brawler Select Rebuild

Goal: replace the current broken/messy screen with a usable first-pass product screen.

- `[ ]` Rebuild card layout with stable responsive grid.
- `[ ]` Show brawler portrait/model preview, name, archetype, role, and locked/available state.
- `[ ]` Show selected brawler detail panel: health, damage style, movement, attack, super, gadget/star power/hypercharge summary.
- `[ ]` Add clear selected/hover/focused states.
- `[ ]` Persist selection into match flow reliably.
- `[ ]` Add random/fallback selection safety if no brawler is selected.
- `[ ]` Add support for keyboard/controller navigation later if needed.
- `[ ]` Manual QA: all brawlers display, selection survives scene transition, no overlapping text on common resolutions.

## Milestone G-11: Production QA / Showcase Readiness

Goal: stabilize the vertical slice for university/demo presentation.

- `[ ]` End-to-end scene flow validation: menu -> brawler select -> mode select -> match -> results.
- `[ ]` Run AI validation checklist from `docs/AI_VALIDATION.md`.
- `[ ]` Validate performance with bots, projectiles, VFX, UI, and breakables active.
- `[ ]` Gate debug logs behind toggles.
- `[ ]` Remove or hide debug-only visuals in normal play.
- `[ ]` Record a 90-second gameplay demo.
- `[ ]` Prepare screenshots/clips for interim/final report and presentation.

## Recommended Execution Order

1. Start the validation queue now, beginning with held aim, projectile readability, AI movement realism, and gem readability.
2. Finish `G-01 Hypercharge Completion`.
3. Polish `G-02 Core Combat And Aiming Polish`, especially aim jitter, projectile lanes, wall-aware aim, and projectile visuals.
4. Implement gameplay systems through `G-03 Grass / Stealth`, `G-04 Breakable Objects / Destructible Cover`, and `G-05 Solo Showdown Mode`.
5. Improve readable match feedback through `G-06 HUD / Match Feedback` and `G-07 Game Feel Pass`.
6. Complete playable content through `G-08 Brawler Kit Completion` and `G-09 Map And Mode Content Polish`.
7. Rebuild `G-10 Brawler Select` once core gameplay feel is stable.
8. Stabilize the vertical slice through `G-11 Production QA / Showcase Readiness`.

## Done Definition

An item is done only when:

- The system has clear ownership and no duplicated one-off logic.
- Runtime behavior has a manual QA note.
- Debug/logging is gated or intentionally visible.
- Unity playtest confirms no obvious broken references, missing visuals, or severe frame spikes.
- The commit message describes the user-facing/system outcome.
