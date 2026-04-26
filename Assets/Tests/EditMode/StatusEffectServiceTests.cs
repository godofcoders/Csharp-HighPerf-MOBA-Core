using System;
using System.Collections.Generic;
using NUnit.Framework;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for StatusEffectService — the orchestrator that decides
    // "do I refresh an existing effect or apply a fresh one?" when an
    // incoming StatusEffectContext arrives.
    //
    // SUT:        StatusEffectService.ApplyStatus(in StatusEffectContext)
    // Decision:   For each instance already on target.ActiveStatusEffects,
    //             ask CanMerge(context); the FIRST one that returns true
    //             gets Merge called and we return early. Otherwise create
    //             a fresh instance via the type switch, call Apply on it,
    //             and append to the list.
    // Side-effects we care about: the StatusEffectEventBus.OnStatusEffectApplied
    //             event must fire with Refreshed=false on the apply path
    //             and Refreshed=true on the merge path. The combat log
    //             service must get exactly one entry per call.
    //
    // WHY THIS NEEDS HEAVIER SETUP THAN PRIOR FIXTURES:
    // ApplyStatus reaches into FOUR ambient pieces of infrastructure:
    //   - ServiceProvider.Get<ISimulationClock>()  (global static dictionary)
    //   - ServiceProvider.Get<ICombatLogService>() (same dictionary)
    //   - StatusEffectEventBus.OnStatusEffectApplied (static event)
    //   - The IStatusTarget the context points at (interface, fakeable)
    // Two of those are static singletons that retain state between tests if
    // we don't pump them in [SetUp]/[TearDown]. The discipline below — Clear
    // before, Clear after, subscribe in SetUp / unsubscribe in TearDown —
    // is what keeps tests independent. Skip that and one test's leftover
    // event handler will silently double-count events in the next test.
    //
    // CROWN-JEWEL TESTS in this fixture:
    //   1. Merge path does NOT add a second instance to ActiveStatusEffects.
    //      The event still fires, with Refreshed=true. This is the heart of
    //      the stack/refresh semantics — break this and every status type
    //      either accumulates duplicate entries (tick spam, double cleanup)
    //      or silently replaces existing modifier sources.
    //
    //   2. First-CanMerge-wins. If two existing instances both say they can
    //      merge, only the FIRST one is merged. Documents the implicit
    //      "there's only one of each type at a time" assumption — if that
    //      assumption breaks (e.g. multi-stack status added later), the
    //      service code needs an audit, and this test goes red to flag it.
    //
    //   3. CanReceiveStatusEffects()=false short-circuits without touching
    //      the active list, the event bus, OR the combat log. This is what
    //      protects dead / respawning brawlers from accumulating effects
    //      during the resurrection window.

    public class StatusEffectServiceTests
    {
        // ---------- Test doubles ----------

        private sealed class FakeClock : ISimulationClock
        {
            public uint CurrentTickValue;
            public uint CurrentTick => CurrentTickValue;
            public float TickDelta => 1f / 30f;
        }

        private sealed class FakeStatusTarget : IStatusTarget
        {
            public int EntityID { get; set; } = 1;
            public TeamType Team { get; set; }
            public bool IsDead { get; set; }
            public bool CanReceiveStatusEffectsResult = true;

            public List<IStatusEffectInstance> ActiveStatusEffects { get; }
                = new List<IStatusEffectInstance>();

            // Recording fields kept around so future tests on Apply/Remove
            // semantics of real status effects (e.g. SlowEffect) can read
            // what got pushed without us having to spin up a real BrawlerState.
            public readonly List<MovementModifier> AddedMovementModifiers
                = new List<MovementModifier>();
            public readonly List<object> RemovedMovementSources = new List<object>();

            public bool CanReceiveStatusEffects() => CanReceiveStatusEffectsResult;

            public void AddIncomingMovementModifier(MovementModifier modifier)
                => AddedMovementModifiers.Add(modifier);
            public void RemoveIncomingMovementModifiersFromSource(object source)
                => RemovedMovementSources.Add(source);

            public void AddIncomingDamageModifier(DamageModifier modifier) { }
            public void RemoveIncomingDamageModifiersFromSource(object source) { }

            public bool HasStatus(StatusEffectType type)
            {
                for (int i = 0; i < ActiveStatusEffects.Count; i++)
                {
                    if (ActiveStatusEffects[i].Type == type)
                        return true;
                }
                return false;
            }
        }

        // Fully scriptable IStatusEffectInstance — every hook is recorded so
        // tests can assert on the exact call shape, and CanMergeReturn /
        // IsExpiredReturn let each test pick the merge decision branch.
        private sealed class SpyStatusEffectInstance : IStatusEffectInstance
        {
            public StatusEffectType TypeValue = StatusEffectType.Slow;
            public StatusEffectType Type => TypeValue;

            public uint StartTick { get; set; }
            public uint EndTick { get; set; }

            public bool CanMergeReturn = false;
            public bool IsExpiredReturn = false;

            public int ApplyCount;
            public int TickCount;
            public int RemoveCount;
            public int CanMergeCount;
            public int MergeCount;

            public readonly List<uint> MergeTicks = new List<uint>();
            public readonly List<StatusEffectContext> MergeContexts
                = new List<StatusEffectContext>();

            public string Label;

            public void Apply(IStatusTarget target, uint currentTick) { ApplyCount++; }
            public void Tick(IStatusTarget target, uint currentTick) { TickCount++; }
            public void Remove(IStatusTarget target, uint currentTick) { RemoveCount++; }

            public bool CanMerge(StatusEffectContext context)
            {
                CanMergeCount++;
                return CanMergeReturn;
            }

            public void Merge(StatusEffectContext context, uint currentTick)
            {
                MergeCount++;
                MergeContexts.Add(context);
                MergeTicks.Add(currentTick);
            }

            public bool IsExpired(uint currentTick) => IsExpiredReturn;
        }

        // ---------- Per-test infrastructure setup ----------

        private FakeClock _clock;
        private CombatLogService _combatLog;
        private List<StatusEffectResult> _capturedApplied;
        private Action<StatusEffectResult> _appliedHandler;
        private StatusEffectService _service;

        [SetUp]
        public void SetUp()
        {
            // 1. Clean ServiceProvider — any leftover registrations from
            // prior test runs would silently satisfy Get<T>() with stale
            // doubles. Clear-then-Register guarantees a known starting state.
            ServiceProvider.Clear();

            _clock = new FakeClock { CurrentTickValue = 100 };
            _combatLog = new CombatLogService();
            ServiceProvider.Register<ISimulationClock>(_clock);
            ServiceProvider.Register<ICombatLogService>(_combatLog);

            // 2. Subscribe a capturing handler to the static event bus.
            // We MUST keep a strong reference to the delegate so we can
            // unsubscribe the exact same instance in TearDown — otherwise
            // C# event removal would silently no-op and we'd leak the handler
            // into the next test.
            _capturedApplied = new List<StatusEffectResult>();
            _appliedHandler = result => _capturedApplied.Add(result);
            StatusEffectEventBus.OnStatusEffectApplied += _appliedHandler;

            _service = new StatusEffectService();
        }

        [TearDown]
        public void TearDown()
        {
            // Unsubscribe FIRST — if a previous test left registrations, the
            // Clear below would still work, but the static event would still
            // fire into our captured handler if a future test indirectly
            // invokes the bus during its own SetUp. Always pair sub/unsub.
            if (_appliedHandler != null)
            {
                StatusEffectEventBus.OnStatusEffectApplied -= _appliedHandler;
                _appliedHandler = null;
            }

            ServiceProvider.Clear();
        }

        // ---------- Builder helpers ----------

        private static StatusEffectContext MakeContext(
            IStatusTarget target,
            StatusEffectType type = StatusEffectType.Slow,
            float duration = 1f,
            float magnitude = 0.5f,
            object sourceToken = null)
        {
            return new StatusEffectContext
            {
                Source = null,
                Target = target,
                Type = type,
                Duration = duration,
                Magnitude = magnitude,
                Origin = UnityEngine.Vector3.zero,
                SourceToken = sourceToken ?? new object()
            };
        }

        // ---------- A. Apply path (no existing effect) ----------

        [Test]
        public void ApplyStatus_AddsNewInstance_WhenNoExistingEffectMatches()
        {
            // Arrange
            var target = new FakeStatusTarget();
            var ctx = MakeContext(target, StatusEffectType.Slow, duration: 1f);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, target.ActiveStatusEffects.Count);
            Assert.AreEqual(StatusEffectType.Slow, target.ActiveStatusEffects[0].Type);
        }

        [Test]
        public void ApplyStatus_CallsApplyOnNewInstance_OncePerCall()
        {
            // Arrange — use a Slow context so SlowEffect is created; we read
            // the side-effect (movement modifier added) as proxy for "Apply
            // was invoked". Direct spy isn't possible here because the
            // service constructs the instance internally.
            var target = new FakeStatusTarget();
            var ctx = MakeContext(target, StatusEffectType.Slow, duration: 1f);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, target.AddedMovementModifiers.Count,
                "SlowEffect.Apply should have pushed exactly one movement modifier.");
        }

        [Test]
        public void ApplyStatus_RaisesAppliedEvent_WithRefreshedFalse_OnApplyPath()
        {
            // Arrange
            var target = new FakeStatusTarget();
            var ctx = MakeContext(target, StatusEffectType.Slow);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, _capturedApplied.Count);
            Assert.IsTrue(_capturedApplied[0].Applied);
            Assert.IsFalse(_capturedApplied[0].Refreshed,
                "First-time apply must report Refreshed=false; only merges " +
                "should fire as Refreshed=true.");
        }

        [Test]
        public void ApplyStatus_AddsCombatLogEntry_OnApplyPath()
        {
            // Arrange
            var target = new FakeStatusTarget();
            var ctx = MakeContext(target, StatusEffectType.Slow);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, _combatLog.GetRecentEntries().Count);
        }

        // ---------- B. Merge path (existing effect says CanMerge=true) ----------

        // CROWN JEWEL #1
        [Test]
        public void ApplyStatus_DoesNotAddNewInstance_WhenExistingEffectCanMerge()
        {
            // Arrange — pre-seed the active list with a spy that says
            // "yes, I can merge this incoming context".
            var target = new FakeStatusTarget();
            var existing = new SpyStatusEffectInstance
            {
                TypeValue = StatusEffectType.Slow,
                CanMergeReturn = true
            };
            target.ActiveStatusEffects.Add(existing);

            var ctx = MakeContext(target, StatusEffectType.Slow);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, target.ActiveStatusEffects.Count,
                "Merge path must NOT append — the existing instance is " +
                "the one that gets refreshed in place.");
            Assert.AreSame(existing, target.ActiveStatusEffects[0],
                "The same existing instance should remain in the list.");
        }

        [Test]
        public void ApplyStatus_CallsMergeOnExisting_AndDoesNotCallApplyAgain()
        {
            // Arrange
            var target = new FakeStatusTarget();
            var existing = new SpyStatusEffectInstance
            {
                TypeValue = StatusEffectType.Slow,
                CanMergeReturn = true
            };
            target.ActiveStatusEffects.Add(existing);

            var ctx = MakeContext(target, StatusEffectType.Slow);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, existing.MergeCount, "Merge should have fired once.");
            Assert.AreEqual(0, existing.ApplyCount,
                "Apply must NOT be re-called on a merge — Apply is install-time " +
                "side-effect (e.g. registering a movement modifier); calling it " +
                "twice would double-register.");
        }

        [Test]
        public void ApplyStatus_PassesCurrentTickIntoMerge()
        {
            // Arrange
            _clock.CurrentTickValue = 7777;
            var target = new FakeStatusTarget();
            var existing = new SpyStatusEffectInstance
            {
                TypeValue = StatusEffectType.Slow,
                CanMergeReturn = true
            };
            target.ActiveStatusEffects.Add(existing);

            var ctx = MakeContext(target);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert — merge needs the clock's view of "now" to compute the
            // refreshed end tick. If the service ever pulled time from the
            // wrong source, this catches it.
            Assert.AreEqual(1, existing.MergeTicks.Count);
            Assert.AreEqual(7777u, existing.MergeTicks[0]);
        }

        [Test]
        public void ApplyStatus_RaisesAppliedEvent_WithRefreshedTrue_OnMergePath()
        {
            // Arrange
            var target = new FakeStatusTarget();
            target.ActiveStatusEffects.Add(new SpyStatusEffectInstance
            {
                TypeValue = StatusEffectType.Slow,
                CanMergeReturn = true
            });
            var ctx = MakeContext(target);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, _capturedApplied.Count);
            Assert.IsTrue(_capturedApplied[0].Applied);
            Assert.IsTrue(_capturedApplied[0].Refreshed,
                "Merge path must report Refreshed=true so subscribers (UI, " +
                "audio) can distinguish 'new debuff hit me' from 'existing " +
                "debuff was extended'.");
        }

        [Test]
        public void ApplyStatus_AddsCombatLogEntry_OnMergePath()
        {
            // Arrange
            var target = new FakeStatusTarget();
            target.ActiveStatusEffects.Add(new SpyStatusEffectInstance
            {
                TypeValue = StatusEffectType.Slow,
                CanMergeReturn = true
            });
            var ctx = MakeContext(target);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, _combatLog.GetRecentEntries().Count);
        }

        // ---------- C. Decision branch — existing effect says CanMerge=false ----------

        [Test]
        public void ApplyStatus_AppliesNewInstance_WhenExistingEffectCannotMerge()
        {
            // Arrange — pre-seed an existing spy that REFUSES to merge.
            // The service should fall through to the apply branch and
            // append a second instance.
            var target = new FakeStatusTarget();
            target.ActiveStatusEffects.Add(new SpyStatusEffectInstance
            {
                TypeValue = StatusEffectType.Stun,
                CanMergeReturn = false
            });

            var ctx = MakeContext(target, StatusEffectType.Slow);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(2, target.ActiveStatusEffects.Count,
                "When no existing effect can merge, a fresh instance is appended.");
            Assert.AreEqual(StatusEffectType.Slow, target.ActiveStatusEffects[1].Type);
        }

        // ---------- D. First-CanMerge-wins ----------

        // CROWN JEWEL #2
        [Test]
        public void ApplyStatus_OnlyMergesFirstMatchingEffect_WhenMultipleSayCanMerge()
        {
            // Arrange — two spies, both saying CanMerge=true. Only the first
            // should actually have Merge called; the second should be untouched.
            // Documents the "only one of each type lives in the list" invariant
            // that the service implicitly relies on.
            var target = new FakeStatusTarget();
            var first = new SpyStatusEffectInstance
            {
                TypeValue = StatusEffectType.Slow,
                CanMergeReturn = true,
                Label = "first"
            };
            var second = new SpyStatusEffectInstance
            {
                TypeValue = StatusEffectType.Slow,
                CanMergeReturn = true,
                Label = "second"
            };
            target.ActiveStatusEffects.Add(first);
            target.ActiveStatusEffects.Add(second);

            var ctx = MakeContext(target, StatusEffectType.Slow);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(1, first.MergeCount, "First match should be merged.");
            Assert.AreEqual(0, second.MergeCount,
                "Second match should NOT be merged — service returns after " +
                "the first successful merge.");
            Assert.AreEqual(1, first.CanMergeCount, "First was asked.");
            Assert.AreEqual(0, second.CanMergeCount,
                "Second should never even be asked CanMerge — the loop " +
                "short-circuits on first true.");
        }

        // ---------- E. Early-return guards ----------

        [Test]
        public void ApplyStatus_DoesNothing_WhenContextTargetIsNull()
        {
            // Arrange
            var ctx = new StatusEffectContext
            {
                Target = null,
                Type = StatusEffectType.Slow,
                Duration = 1f,
                Magnitude = 0.5f,
                SourceToken = new object()
            };

            // Act + Assert — no NRE, and no events fired.
            Assert.DoesNotThrow(() => _service.ApplyStatus(in ctx));
            Assert.AreEqual(0, _capturedApplied.Count);
            Assert.AreEqual(0, _combatLog.GetRecentEntries().Count);
        }

        // CROWN JEWEL #3
        [Test]
        public void ApplyStatus_DoesNothing_WhenTargetCannotReceiveStatusEffects()
        {
            // Arrange — target is alive but rejects new effects (the dead /
            // respawning brawler case).
            var target = new FakeStatusTarget { CanReceiveStatusEffectsResult = false };
            var ctx = MakeContext(target, StatusEffectType.Slow);

            // Act
            _service.ApplyStatus(in ctx);

            // Assert
            Assert.AreEqual(0, target.ActiveStatusEffects.Count,
                "A target that refuses status must not accumulate effects, " +
                "even partially. This is what protects the resurrection window.");
            Assert.AreEqual(0, _capturedApplied.Count,
                "No event should fire — subscribers shouldn't see a phantom apply.");
            Assert.AreEqual(0, _combatLog.GetRecentEntries().Count,
                "No combat log entry — the rejection is invisible.");
        }

        // ---------- F. Per-instance Merge contract pinned via real SlowEffect ----------
        //
        // The orchestration above asserts "service called Merge". These two
        // tests pin "Merge does the right thing inside" — using the real
        // SlowEffect instance that production actually instantiates. Together
        // they cover the full path: service-level routing + value-level
        // semantics, with no real BrawlerState in sight.

        [Test]
        public void SlowEffect_Merge_ExtendsEndTick_WhenNewDurationIsLonger()
        {
            // Arrange — first slow at tick 100 with 1.0s = 30 ticks => EndTick=130.
            var target = new FakeStatusTarget();
            _clock.CurrentTickValue = 100;
            var ctx1 = MakeContext(target, StatusEffectType.Slow, duration: 1f);
            _service.ApplyStatus(in ctx1);

            uint endTickAfterFirst = target.ActiveStatusEffects[0].EndTick;
            Assert.AreEqual(130u, endTickAfterFirst, "sanity: 1s slow at tick 100 ends at 130");

            // Act — at tick 110, apply a 2.0s slow. New end should be
            // 110 + 60 = 170 (longer than 130, so it wins).
            _clock.CurrentTickValue = 110;
            var ctx2 = MakeContext(target, StatusEffectType.Slow, duration: 2f);
            _service.ApplyStatus(in ctx2);

            // Assert
            Assert.AreEqual(1, target.ActiveStatusEffects.Count, "merge, not append");
            Assert.AreEqual(170u, target.ActiveStatusEffects[0].EndTick,
                "Longer incoming duration should extend EndTick.");
        }

        [Test]
        public void SlowEffect_Merge_DoesNotShrinkEndTick_WhenNewDurationIsShorter()
        {
            // Arrange — first slow at tick 100 with 2.0s = 60 ticks => EndTick=160.
            var target = new FakeStatusTarget();
            _clock.CurrentTickValue = 100;
            var ctx1 = MakeContext(target, StatusEffectType.Slow, duration: 2f);
            _service.ApplyStatus(in ctx1);

            uint endTickAfterFirst = target.ActiveStatusEffects[0].EndTick;
            Assert.AreEqual(160u, endTickAfterFirst, "sanity: 2s slow at tick 100 ends at 160");

            // Act — at tick 110, apply a 0.5s slow. Naive "always overwrite"
            // would set EndTick to 110 + 15 = 125, SHRINKING the slow.
            _clock.CurrentTickValue = 110;
            var ctx2 = MakeContext(target, StatusEffectType.Slow, duration: 0.5f);
            _service.ApplyStatus(in ctx2);

            // Assert
            Assert.AreEqual(160u, target.ActiveStatusEffects[0].EndTick,
                "Shorter incoming duration must NOT cut a longer existing slow " +
                "short — that would let attackers grief their teammates' hard CC " +
                "by spamming weak slows. The merge is max(existing, incoming).");
        }
    }
}
