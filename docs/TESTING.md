# Testing Notes — One-Page Reference

The durable reference for how we write tests in this project. Original one-pager
on top; additions accumulated through Session 5's testing curriculum below.

---

## Original one-pager

### Test naming rule

`Subject_Action_Expectation`

- **Subject** — what's being tested (method, class, or feature)
- **Action** — what you're doing to it
- **Expectation** — what should be true after

Examples: `ApplyDamage_ReturnsTrue_OnKillingBlow`, `TickAll_RunsPhasesInDeclaredOrder`, `Consume_FailsAtomically_WhenInsufficient`.

If you can't fit the test into this shape, the test is doing too much. Split it.

### Methodology

1. **Find a cheap surface** — POCOs you can `new` up, no Unity boot.
2. **Name the contract** — `Subject_Action_Expectation`, written before the body.
3. **Arrange / Act / Assert** — three blocks, blank lines between, one Act per test.
4. **Test invariants, not just happy paths** — boundaries, failure modes, idempotency, order independence.
5. **Hand-roll fakes for collaborators.** Skip Moq.

### Test doubles vocabulary

- **Dummy** — satisfies type, never used
- **Stub** — returns canned answers
- **Spy** — records calls so you can assert on them
- **Fake** — simplified working implementation

### Fake return strategies

- Constant when test doesn't care
- Constructor-injected when tests need different values
- Recording (spy) when verifying calls happened
- Real-enough logic when behavior matters
- For methods you don't need: `default` / `null` / `false` / `0`

### Interfaces vs base classes

- **Interfaces:** clean, implement everything from scratch
- **Base classes:** only override `virtual`/`abstract`, must call base constructor
- **Unity types:** `ScriptableObject.CreateInstance<T>()` or `AddComponent<T>()`, heavier

### Two anti-patterns to avoid

1. **No clear assertion** — running a method without distinguishing right from wrong behavior. Bad: `Assert.IsTrue(health > 0)` when it was already true. Good: assert exact expected value.
2. **Asserting on internal fields** — reflection into private state, public-for-test fields, test-only getters. Couples tests to implementation details. Always assert through the same public surface production code uses.

### Mental model

Test the class like a vending machine. Coins in (Arrange), press button (Act), soda out (Assert). **Don't open the machine.**

**Diagnostic:** if a property you want to assert on shouldn't exist for production callers, it shouldn't exist for tests either.

---

## Additions

### Vocabulary

- **SUT — Subject Under Test.** The class/method the fixture exists to verify, distinguished from its *collaborators* (which get fakes/stubs/spies). When you write "the SUT does X," it's the single thing being tested. Use this term explicitly in fixture doc-comments — it makes "what's verified vs. what's faked" unambiguous.

### The cheap-surface ladder

A practical refinement of "find a cheap surface" — when you have a choice, climb to the lowest rung:

1. **POCO with method-parameter inputs** — no statics, no Unity, no DI. `new SUT()` is the entire setup; `sut.Method(currentTick: 100)` is the entire act. Examples: `BrawlerCooldowns`, `BrawlerActionStateMachine`, `BrawlerStealth`.
2. **POCO with constructor-injected collaborators (DI)** — hand it fakes, done. Add `[SetUp]` only if you reuse the same wiring across many tests. Example: `StatusEffectService` (after handing it fakes via `ServiceProvider`).
3. **POCO that consumes static singletons** — needs the static-state isolation discipline below. Example: anything reading `ServiceProvider.Get<T>()` or subscribing to a static event bus.
4. **ScriptableObject + MonoBehaviour + scene fixtures** — save these for in-engine smoke tests, not unit tests.

### Test patterns we've actually used (S5 curriculum)

**POCO value testing.** Cheap-surface rung 1. Class has no static state, no Unity types. Setup is `new SUT()`; the test reads as "given these inputs, the published value is this." Used in every fixture where the SUT could be lifted onto rung 1.

**Spy-on-hooks.** When the SUT consumes an abstract base class with virtual hooks (e.g. `PassiveDefinition.Install` / `Uninstall`), the test creates a subclass that overrides each hook to append a tagged string into a *shared chronological* `List<string>`. Asserting on the list verifies WHICH hooks fired AND IN WHAT ORDER across multiple instances. Strictly stronger than per-instance call counters — the latter can't catch "out of order across instances" bugs. Example: `BrawlerLoadoutPassiveLifecycleTests`.

**Static-state isolation discipline.** When the SUT touches static singletons:

- `[SetUp]`: `Clear()` the singleton, register fakes, save event handlers to fields, subscribe.
- `[TearDown]`: unsubscribe (using the *same delegate instance* from the field), `Clear()` again.

Without this, tests leak state across runs and fail intermittently by run-order. Example: `StatusEffectServiceTests`.

**Same-delegate-instance gotcha (C# events).** `someEvent -= handler` silently no-ops if `handler` is a different delegate instance than the one originally subscribed. So this fails to unsubscribe:

```csharp
[SetUp] public void SetUp() { Bus.OnX += result => _captured.Add(result); }
[TearDown] public void TearDown() { Bus.OnX -= result => _captured.Add(result); } // different instance!
```

The fix: store the handler in a field and reuse it.

```csharp
private Action<Result> _handler;
[SetUp] public void SetUp() {
    _handler = result => _captured.Add(result);
    Bus.OnX += _handler;
}
[TearDown] public void TearDown() {
    Bus.OnX -= _handler; // same instance
    _handler = null;
}
```

**State-machine table testing.** For a state machine with N states × M transitions, walk every (state, transition, condition) cell — including cells that should *not* transition. Use NUnit `[TestCase]` to fan one method out across all enum values; new states get one new `[TestCase]` row, not a copy-pasted method. Example: `BrawlerActionStateMachineTests` walks all 8 `BrawlerActionStateType` values × 4 transition methods.

**Truth-table testing.** For a predicate that's a logical AND/OR of K booleans, `[TestCase]` one method through all `2^K` combinations. Every row pins one input combo. If someone flips the AND to OR or drops a check, multiple rows fail simultaneously — a much louder regression than per-case methods. Example: `BrawlerStealthTests.IsHidden_RequiresAllThreeConditions_TruthTable` (8 rows for `inBush AND !revealed AND !recentlyAttacked`).

**Boundary-pair test.** For any strict inequality in production (`<`, `>`), assert *both sides* in one method. `LockUntilTick - 1` (active) and `LockUntilTick` (expired) adjacent in the same test body. Catches `<` vs `<=` typos that single-side tests can't. Two-line pattern:

```csharp
sm.Enter(..., currentTick: 100, durationTicks: 30); // LockUntilTick = 130
Assert.IsTrue(sm.IsActive(currentTick: 129));   // boundary - 1
Assert.IsFalse(sm.IsActive(currentTick: 130));  // boundary
```

**Crown jewel test.** The 1–4 most important tests in a fixture, named explicitly in the fixture's doc-comment with one line on *why* each is load-bearing. Future readers know which tests mean "stop and reconsider" if they break vs. "probably just update the test." If a refactor breaks a crown jewel, you re-examine the refactor; if it breaks a peripheral test, you may just update the test. Every fixture in S5 names its crown jewels.

**Known-gap-via-test.** Found a real bug while writing tests? Don't silently fix in production *and* don't delete the test. Pin current behavior with a comment that names the gap, names plausible fixes, and explains why this is a "decide later, document now" rather than a "tidy up." The test stays loud in the trace until intentionally addressed. Example: `BrawlerStealthTests.IsHidden_FreshBrawlerInBush_AppearsVisible_KnownGapDueToLastAttackTickDefault`.

**Pin-the-constant test.** For tunable constants (`RecentlyAttackedTicks = 60`, `TickRate = 30`), one line: `Assert.AreEqual(60u, BrawlerStealth.RecentlyAttackedTicks)`. Catches silent balance-pass drift — a designer changing 60 → 90 must also update the test, which surfaces it for review instead of slipping in.

**Pin-then-migrate.** Refactor pattern, not a test pattern strictly, but tied to testing: when introducing a new helper, land helper + a focused test fixture proving its contract *first*, then migrate call sites in separate commits so each migration is reviewable in isolation. Avoids "big bang" risk where a helper bug takes down N sites at once. Example: `SimulationClock.SecondsToTicks` + its fixture, then 13 site migrations.

### More on hand-rolled fakes

- **Subclass-style fakes are valid.** Spy-on-hooks is a fake built by extending an abstract base class instead of implementing an interface. Same idea, different inheritance path.
- **A fake can record AND return canned values.** The "stub vs spy vs fake" categories overlap in practice. Don't agonize over the label — make the fake do what the test needs.
- **Defaulting unused methods loudly.** For methods on a fake's interface that no test should reach, `throw new NotImplementedException()` is also valid. It surfaces "the SUT did something I didn't expect" instead of silently returning `null`.
- **Fakes go in the same file as the fixture.** As private nested classes inside the test class. Co-located = grep-friendly; one-file diff per fixture.

### Additional anti-patterns

3. **Testing the fake instead of the SUT.** If your assertions are mostly about *the fake's recorded calls*, ask whether the SUT actually does anything observable. Sometimes the answer is "yes, it orchestrates" (legitimate spy use). Often the SUT is a thin wrapper and the test is just verifying that your fake works.
4. **Over-fixturing in `[SetUp]`.** A common SetUp building a "complete" SUT for all tests creates hidden coupling — tests pass for reasons unrelated to what they assert. If a test needs a different setup, write a different test method or a small builder helper. Don't bloat SetUp.
5. **Wall-clock coupling.** Tests that read `DateTime.Now`, use `Thread.Sleep`, or depend on `Time.deltaTime`. Always pass the tick as a method parameter or use a `FakeClock`. Wall-clock tests are flaky and hide ordering bugs.
6. **Asserting on log strings.** Asserting that a log line contains a substring couples the test to copy. The log is a side-effect for humans, not a contract. Assert on the public API; let the log line drift.
7. **Testing "current implementation" instead of "published contract."** If a refactor that preserves behavior breaks your test, the test was coupled to internals. Rewrite or delete it.

### Same-instance discipline checklist (events, modifiers, source tokens)

This pattern shows up in three places in the codebase — same shape every time:

- **C# events:** `bus -= handler` requires the *same delegate instance* to actually unsubscribe.
- **Movement/stat modifiers:** `RemoveModifier(sourceToken)` requires the *same source token instance* (or value-equal, depending on the API) used at `AddModifier`.
- **Passive uninstall:** `UninstallAll` walks runtimes paired with the *same context object* used at install — verified by the spy-on-hooks fixture.

If you see "thing was added but never seems to remove cleanly," check same-instance discipline first.

### Round-to-nearest tick conversion

Seconds → ticks should be `(uint)(seconds * TickRate + 0.5f)`, not `(uint)(seconds * TickRate)`. The latter truncates: 0.5s × 30Hz = 15.0 → 15, but 0.5s × 31Hz = 15.5 → 15 (off by one). The `+ 0.5f` rounds. The bug is invisible in playtest (one-tick errors don't show up) but caught by a single arithmetic test against the expected published value. Centralized in this project as `SimulationClock.SecondsToTicks(float seconds)`.

### When to break the rules

- **One Act per test:** boundary-pair tests deliberately have two Acts (one for each side of the boundary). The two Acts together pin a single contract — it would be weaker as two separate methods because the relationship between the two sides is the point.
- **Don't open the machine:** known-gap-via-test sometimes requires asserting on a *current* behavior that wouldn't survive a "test the contract" purity test. The comment explains why; the test stays.
- **Skip Moq:** if you ever face a collaborator with 10+ methods or complex stateful setup, Moq starts paying off. We haven't hit that yet. When in doubt, hand-roll first; reach for Moq when the hand-roll is bigger than the SUT.

---

## Quick decision tree

When sitting down to write a fixture:

1. Can the SUT be `new`-ed up with no statics and no Unity? → POCO value testing. Done.
2. Does it take collaborators in its constructor? → Hand-rolled fakes. Done.
3. Does it touch static singletons? → Add SetUp/TearDown discipline. Save event handlers in fields.
4. Is it a state machine? → `[TestCase]`-parameterized table walk + boundary pairs.
5. Is it a logical predicate of K booleans? → Truth-table test, `2^K` rows.
6. Does it orchestrate a pipeline over multiple instances? → Spy-on-hooks with a shared chronological log.
7. Did writing the fixture surface a bug? → Pin the gap with a comment; don't silently fix.
8. Is the SUT a new helper? → Pin-then-migrate: land helper + fixture first, migrate sites in separate commits.

Then name the 1–4 crown jewels at the top of the fixture so the next person knows which tests are load-bearing.
