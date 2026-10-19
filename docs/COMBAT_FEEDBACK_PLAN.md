# Combat feedback delivery

1. `feat(presentation): add pooled combat numbers`
   - Actual health lost/restored, including regeneration; no overheal or overkill inflation.
   - Bounded, prewarmed UI pool; combine rapid ticks per brawler and feedback kind.
   - Camera projection, fade/rise animation, visibility filtering, scene cleanup.
2. `feat(feedback): confirm hits and shield absorption`
   - Confirm resolved local-player hits, including fully absorbed shield hits.
   - Distinct shield indicator and sound; throttle rapid-hit audio.
   - Saved effects-volume control, separate from background music.
3. Ability-readiness cues
   - Distinct super/hypercharge ready transitions and restrained sounds.
4. Presentation and accessibility polish
   - In-game tuning across brawlers and screen sizes; adjustable number/effect intensity.

Each implementation commit includes focused tests. Sound and visual feel also need an in-game playtest.
