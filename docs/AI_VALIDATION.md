# AI Validation Pass

Use the AI debug overlay while running play mode:

- `AI Perf` shows map/path cost for the current simulation tick.
- `AI Validation` shows whether decisions are healthy at the team level.
- `Team Roles` shows whether AI-15 role coordination is actively changing scores.

## Healthy Baseline

- `invalid=0` during normal combat and objective play.
- `zero=0` once bots are initialized and the match has begun.
- `lowMargin` can spike in close decisions, but should not stay high for every bot.
- `switch` should pulse during changing combat, not climb every tick for every bot.
- `target=X/Y` should reflect combat state: targeted bots during fights, targetless bots during objective/search phases.
- `roleAdj` should be non-zero when multiple allies are choosing similar roles.
- `A/H/R`, `Rt/E/P`, `U/G`, and `S/O/W` should show a spread of actions in real matches, not all bots parked on one action.

## Regression Signals

- `invalid > 0`: an action is being chosen in a context where its scorer should have made it impossible.
- sustained high `zero`: bots are alive but no meaningful action is scoring.
- sustained high `switch`: commitment or team role shaping may be too loose.
- sustained high `lowMargin`: action weights are too flat or competing systems are fighting each other.
- `roleAdj=0` in clustered team fights: AI-15 reservations may not be reporting.
- high `AI Perf` path queries or touched nodes alongside poor FPS: map-aware movement needs another budget pass.

## Suggested Smoke Test

1. Run a 3v3 match for at least 60 seconds.
2. Cycle bots with `F3`.
3. During combat, verify target actions spread across `Approach`, `HoldRange`, `Reposition`, `Peel`, `Retreat`, and `Evade`.
4. During downtime, verify actions move toward `Search`, `Objective`, or `Wander`.
5. Press `F4` if console dump is enabled and scan for non-zero `invalid`.
6. Repeat once with a fragile-heavy team and once with a tank/fighter-heavy team.
