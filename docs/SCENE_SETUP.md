# Scene Setup Checklist

End-to-end steps for the Phase 1 slice's full scene flow:
**Loading → MainMenu → BrawlerSelect → GameModeSelect → Match → Results.**

The runtime controllers (`SceneFlow`, `LoadingScreen`, `MainMenuScreen`, `BrawlerSelectScreen`, `GameModeSelectScreen`, `ResultsScreen`) live under `Assets/Scripts/Core/Infrastructure/Flow/`. Editor-only `[MenuItem]` builders that auto-construct each scene's hierarchy live under `Assets/Editor/SceneFlow/`.

---

## One-time setup

### 1. Add scenes to Build Settings

Run **MOBA → Scene Flow → Setup Build Settings**. Adds the 6 scene paths to Build Settings (entries that don't exist on disk yet are still added — they get populated as you run each scene builder).

### 2. Auto-build the menu scenes

Run each of these once. They overwrite any existing scene at the same path, so be careful if you've already hand-edited one.

- **MOBA → Scene Flow → Build Loading Scene**
- **MOBA → Scene Flow → Build MainMenu Scene**
- **MOBA → Scene Flow → Build BrawlerSelect Scene**
- **MOBA → Scene Flow → Build GameModeSelect Scene**
- **MOBA → Scene Flow → Build Results Scene**

Each builder creates: a Canvas (Screen Space - Overlay, ScaleWithScreenSize 1920x1080), an EventSystem, a colored background, placeholder text and buttons, and the screen-controller MonoBehaviour with field references already wired.

The **Match scene** does NOT have a builder — it's your existing gameplay scene. Don't run a builder for it.

---

## Manual wiring still needed (per scene)

### Loading
Nothing — works out of the box. Auto-advances after `_holdSeconds` (default 2s).

### MainMenu
Nothing — works out of the box.

### BrawlerSelect
- **Assign `_availableBrawlers` array** on the Canvas's `BrawlerSelectScreen` component. Drag in your 4 BrawlerDefinition assets (Colt, Byron, Jessie, Barley).
- **Create a card prefab** and assign it to `_cardPrefab`. Minimum prefab content:
  - Root GameObject with `Button` + `Image` + `LayoutElement` (preferred size ~200×240)
  - Child GameObject with `Text` (or TMP_Text) — the screen sets `.text = brawler.name`
  - Save as `Assets/Prefabs/UI/BrawlerCard.prefab`

### GameModeSelect
Nothing — works out of the box. (When you add more modes, drop more buttons in the scene + extend `GameModeSelectScreen` with one more `_someModeButton` + `OnSomeMode()` handler + a new `GameModeId` enum value.)

### Match
This is your gameplay scene. Three integration points to wire so flow works end-to-end:

1. **Spawn the player's chosen brawler.** Read `SceneSelection.SelectedBrawler` somewhere in your match-init code (probably in `SpawnManager` or wherever player spawning happens) and use it instead of a hardcoded brawler.

2. **Capture results on match end.** `MatchManager.OnStateChanged` fires when state goes to `Ended`. Subscribe and call:
   ```csharp
   MatchResultBoard.Capture(winnerTeam, blueScore, redScore);
   SceneFlow.Instance.LoadScene(SceneId.Results);
   ```
   Where to put it: a new small MonoBehaviour in the Match scene (e.g. `MatchEndRouter`), OR extend `MatchManager.EndMatch` directly.

3. **Drop a `MatchHUD` GameObject** + (per-brawler) `BrawlerHealthBarView` and `BrawlerCarrierBadgeView` widgets if you haven't already (separate from this scene-flow work).

### Results
Nothing — works out of the box. Reads `MatchResultBoard` (set by the Match scene before transitioning here).

---

## Polish pass (when you're ready)

Stuff the builders deliberately leave default so you can replace with your own art:

- **Button background sprites** — currently white default. Drop your sprites onto each Button's child Image.
- **Custom font** — currently `LegacyRuntime.ttf`. If you imported TMP fonts, swap each Text with TMP_Text and assign your font asset; the runtime controllers support both via `_*Tmp` and `_*Legacy` SerializeField pairs.
- **Background images / gradients** — the builders use flat colors. Replace each scene's Background Image with a gradient/photo if you want.
- **Brawler card art** — portraits, archetype icons, ability previews on the card prefab.
- **Audio** — title music in MainMenu, button click SFX, victory fanfare on Results, etc. Any place a button click happens is a natural hook.

---

## Architecture notes

- **`SceneFlow` lives only in the Loading scene.** It marks itself `DontDestroyOnLoad` on Awake, so it carries through every subsequent scene transition. If you start the game from any other scene (e.g. for testing), `SceneFlow.Instance` will be null — drop a SceneFlow GameObject into that scene's editor session manually.
- **`SceneSelection` is `static`,** so it survives scene transitions automatically. Reset on returning to MainMenu (`MainMenuScreen.OnEnable`) so stale picks don't leak.
- **`MatchResultBoard` is also `static`** with the same shape. Reset whenever a fresh match starts (e.g. in `MatchManager.StartMatchFlow`).
- **Scene names are mapped centrally** in `SceneFlow._sceneNames` (parallel to the `SceneId` enum). Renaming a scene file? Update the mapping array; gameplay code stays unchanged.

---

## Future flow extensions (not in scope)

- Lobby / matchmaking screen between MainMenu and BrawlerSelect (Phase 3+ multiplayer)
- Settings screen (audio/video/control rebinding)
- Tutorial / first-time UX
- Victory animation overlay before transitioning to Results
- Brawler trophy / progression display on results
