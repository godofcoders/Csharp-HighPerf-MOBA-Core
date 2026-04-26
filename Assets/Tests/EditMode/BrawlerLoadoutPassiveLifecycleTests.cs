using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Spy-on-hooks tests for BrawlerLoadout's passive install/uninstall lifecycle.
    //
    // SUT:   BrawlerLoadout.InstallAll / UninstallAll
    // SPY:   SpyPassive (a test-only PassiveDefinition subclass that records
    //        every call into Install / CreateRuntime / Uninstall) plus
    //        SpyRuntime (a hand-rolled IPassiveRuntime that records its own
    //        lifecycle calls).
    //
    // WHY THE SPY PATTERN, NOT VALUE TESTING:
    // The concrete passives (DamageStarPower, HealthGear, MoveSpeedGear, ...)
    // each *do* something different inside Install. Testing each one in
    // isolation = leaf-behavior testing. But every concrete passive relies on
    // an orchestration contract that lives on BrawlerLoadout:
    //
    //   "Install is called once per equipped passive, with a context that
    //    carries my target / owner / unique SourceToken; later Uninstall is
    //    called with the SAME context (same SourceToken!), in reverse order;
    //    runtime.OnInstalled fires after definition.Install, and runtime
    //    .OnUninstalled fires BEFORE definition.Uninstall."
    //
    // If that contract breaks — for example a refactor re-fabricates the
    // context at uninstall time — every passive's modifier-cleanup goes silent
    // (the source token never matches what was registered). A spy that records
    // calls is the right tool for pinning this kind of lifecycle/ordering
    // contract; value tests on individual passives would catch *symptoms* of
    // the break (modifier leak) but not the root cause.
    //
    // STRUCTURAL LUCK:
    // BrawlerLoadout.InstallAll only uses `target`/`owner` to construct a
    // PassiveInstallContext (a struct that just stores them). The two
    // runtime-callback lines are null-conditional, and our spy returns null
    // from CreateRuntime by default. So we can pass (null, null) for the wide
    // concrete deps — the spy isolates us from the BrawlerState dependency
    // graph completely. When a runtime IS needed for a test (the runtime-
    // callback subset), we hand-roll a SpyRuntime whose OnInstalled/
    // OnUninstalled bodies don't dereference the BrawlerState arg either.
    //
    // CROWN-JEWEL TESTS in this fixture:
    //   1. UninstallAll_CallsUninstall_InReverseInstallOrder
    //      Catches any refactor that swaps the LIFO loop for FIFO. Reverse
    //      order matters because passives can have install-order
    //      dependencies (e.g. a gear that buffs damage has to come down
    //      before the star power that multiplied it, or the multiplier sits
    //      on a stale base).
    //
    //   2. UninstallAll_PassesSameContextObjectUsedAtInstall
    //      The SourceToken in PassiveInstallContext is what every modifier
    //      registered against. If BrawlerLoadout re-built the context at
    //      uninstall time it would mint a fresh SourceToken, and the base
    //      Uninstall's RemoveAllStatModifiersFromSource calls would no-op
    //      silently. This test pins "the loadout stored my context, it didn't
    //      throw it away".
    //
    //   3. UninstallAll_FiresRuntimeOnUninstalled_BeforeDefinitionUninstall
    //      The runtime gets first crack at teardown (it might rely on stat
    //      modifiers the definition is about to remove). Easy to flip in a
    //      refactor; the per-spy CallLog catches the order.

    public class BrawlerLoadoutPassiveLifecycleTests
    {
        // ---------- Test doubles ----------

        // Shared chronological log across spies, so a single test can assert
        // on inter-spy ordering ("Install A, Install B, Uninstall B, Uninstall A").
        private sealed class CallLog
        {
            private readonly List<string> _events = new List<string>();
            public IReadOnlyList<string> Events => _events;
            public void Append(string e) => _events.Add(e);
        }

        // SpyPassive deliberately does NOT call base.Uninstall — base reaches
        // into context.State.RemoveAllStatModifiersFromSource(...) which would
        // NRE on our null target. The whole point of the spy is to record
        // calls, not to do any of the real Uninstall work.
        private sealed class SpyPassive : PassiveDefinition
        {
            public string Label;

            public int InstallCallCount;
            public int CreateRuntimeCallCount;
            public int UninstallCallCount;

            public readonly List<PassiveInstallContext> InstallContexts =
                new List<PassiveInstallContext>();
            public readonly List<PassiveInstallContext> UninstallContexts =
                new List<PassiveInstallContext>();

            // Optional — set to a SpyRuntime when a test needs to verify the
            // runtime callback subset; null by default exercises the
            // common "definition without a runtime object" code path.
            public IPassiveRuntime RuntimeToReturn;

            public CallLog SharedLog;

            public override void Install(PassiveInstallContext context)
            {
                InstallCallCount++;
                InstallContexts.Add(context);
                SharedLog?.Append("Install:" + Label);
            }

            public override IPassiveRuntime CreateRuntime(PassiveInstallContext context)
            {
                CreateRuntimeCallCount++;
                SharedLog?.Append("CreateRuntime:" + Label);
                return RuntimeToReturn;
            }

            public override void Uninstall(PassiveInstallContext context)
            {
                UninstallCallCount++;
                UninstallContexts.Add(context);
                SharedLog?.Append("Uninstall:" + Label);
            }
        }

        private sealed class SpyRuntime : IPassiveRuntime
        {
            public int OnInstalledCount;
            public int OnUninstalledCount;
            public int TickCount;

            public PassiveDefinition Definition { get; set; }
            public object SourceToken { get; set; }

            public CallLog SharedLog;
            public string Label;

            public void OnInstalled(BrawlerState state)
            {
                OnInstalledCount++;
                SharedLog?.Append("Runtime.OnInstalled:" + Label);
            }

            public void Tick(BrawlerState state, uint currentTick)
            {
                TickCount++;
            }

            public void OnUninstalled(BrawlerState state)
            {
                OnUninstalledCount++;
                SharedLog?.Append("Runtime.OnUninstalled:" + Label);
            }
        }

        // ---------- ScriptableObject lifecycle housekeeping ----------
        // Same pattern as BrawlerBuildValidatorTests: every CreateInstance
        // produces a UnityEngine.Object that lives until DestroyImmediate.

        private List<Object> _spawned;

        [SetUp]
        public void SetUp()
        {
            _spawned = new List<Object>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Object.DestroyImmediate(_spawned[i]);
            }
            _spawned.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _spawned.Add(obj);
            return obj;
        }

        private SpyPassive MakeSpy(string label, CallLog log = null, IPassiveRuntime runtime = null)
        {
            SpyPassive spy = Track(ScriptableObject.CreateInstance<SpyPassive>());
            spy.Label = label;
            spy.SharedLog = log;
            spy.RuntimeToReturn = runtime;
            return spy;
        }

        // ---------- A. Install — cardinality, ordering, runtime callback ----------

        [Test]
        public void InstallAll_CallsInstall_OncePerEquippedPassive()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            SpyPassive b = MakeSpy("B");
            SpyPassive c = MakeSpy("C");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, b, c });

            // Act
            loadout.InstallAll(null, null);

            // Assert
            Assert.AreEqual(1, a.InstallCallCount);
            Assert.AreEqual(1, b.InstallCallCount);
            Assert.AreEqual(1, c.InstallCallCount);
        }

        [Test]
        public void InstallAll_CallsInstall_InEquippedListOrder()
        {
            // Arrange
            var log = new CallLog();
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A", log);
            SpyPassive b = MakeSpy("B", log);
            SpyPassive c = MakeSpy("C", log);
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, b, c });

            // Act
            loadout.InstallAll(null, null);

            // Assert — each Install is followed by its CreateRuntime; pin both
            // the order and the inter-call sequence in one shot.
            CollectionAssert.AreEqual(
                new[]
                {
                    "Install:A", "CreateRuntime:A",
                    "Install:B", "CreateRuntime:B",
                    "Install:C", "CreateRuntime:C",
                },
                log.Events);
        }

        [Test]
        public void InstallAll_CallsCreateRuntime_OncePerPassive_AfterInstall()
        {
            // Arrange
            var log = new CallLog();
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A", log);
            loadout.SetEquippedPassives(new PassiveDefinition[] { a });

            // Act
            loadout.InstallAll(null, null);

            // Assert
            Assert.AreEqual(1, a.CreateRuntimeCallCount);
            CollectionAssert.AreEqual(
                new[] { "Install:A", "CreateRuntime:A" },
                log.Events);
        }

        [Test]
        public void InstallAll_FiresRuntimeOnInstalled_WhenRuntimeNonNull()
        {
            // Arrange
            var runtime = new SpyRuntime();
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A", runtime: runtime);
            loadout.SetEquippedPassives(new PassiveDefinition[] { a });

            // Act
            loadout.InstallAll(null, null);

            // Assert
            Assert.AreEqual(1, runtime.OnInstalledCount);
        }

        [Test]
        public void InstallAll_DoesNotThrow_WhenCreateRuntimeReturnsNull()
        {
            // Arrange — RuntimeToReturn defaults to null on the spy
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a });

            // Act + Assert — the null-conditional `runtime?.OnInstalled` is
            // the contract we're pinning here. Pure existence test for the
            // null-runtime path: nothing to assert beyond "no NRE".
            Assert.DoesNotThrow(() => loadout.InstallAll(null, null));
        }

        // ---------- B. Context — target/owner/SourceToken propagated, token unique ----------

        [Test]
        public void InstallAll_PassesProvidedTarget_IntoEachInstallContext()
        {
            // Arrange — we don't have an easy way to mint a non-null
            // BrawlerState without dragging the full graph in. Instead we
            // pin "what BrawlerLoadout was given is what each spy sees":
            // pass null and assert null is propagated. The next test
            // covers the SourceToken side, which is the bit that actually
            // varies per passive.
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            SpyPassive b = MakeSpy("B");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, b });

            // Act
            loadout.InstallAll(null, null);

            // Assert
            Assert.IsNull(a.InstallContexts[0].State);
            Assert.IsNull(a.InstallContexts[0].Owner);
            Assert.IsNull(b.InstallContexts[0].State);
            Assert.IsNull(b.InstallContexts[0].Owner);
        }

        [Test]
        public void InstallAll_GivesEachPassive_AUniqueSourceToken()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            SpyPassive b = MakeSpy("B");
            SpyPassive c = MakeSpy("C");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, b, c });

            // Act
            loadout.InstallAll(null, null);

            // Assert — object identity, not equality. If any two passives
            // shared a source token, RemoveAllStatModifiersFromSource on one
            // would also yank the other's modifiers. This is the invariant
            // the source-token system relies on across the codebase.
            object tA = a.InstallContexts[0].SourceToken;
            object tB = b.InstallContexts[0].SourceToken;
            object tC = c.InstallContexts[0].SourceToken;

            Assert.IsNotNull(tA);
            Assert.IsNotNull(tB);
            Assert.IsNotNull(tC);
            Assert.AreNotSame(tA, tB);
            Assert.AreNotSame(tB, tC);
            Assert.AreNotSame(tA, tC);
        }

        // ---------- C. Uninstall — reverse order, same context, runtime-before-def ----------

        [Test]
        public void UninstallAll_CallsUninstall_OncePerInstalledPassive()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            SpyPassive b = MakeSpy("B");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, b });
            loadout.InstallAll(null, null);

            // Act
            loadout.UninstallAll(null);

            // Assert
            Assert.AreEqual(1, a.UninstallCallCount);
            Assert.AreEqual(1, b.UninstallCallCount);
        }

        // CROWN JEWEL #1 — reverse-order teardown
        [Test]
        public void UninstallAll_CallsUninstall_InReverseInstallOrder()
        {
            // Arrange
            var log = new CallLog();
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A", log);
            SpyPassive b = MakeSpy("B", log);
            SpyPassive c = MakeSpy("C", log);
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, b, c });
            loadout.InstallAll(null, null);
            // Pre-condition: install order is A, B, C.

            // Act
            loadout.UninstallAll(null);

            // Assert — only the uninstall portion of the log, to keep this
            // test focused on the single contract it exists to pin.
            var uninstallEvents = new List<string>();
            for (int i = 0; i < log.Events.Count; i++)
            {
                if (log.Events[i].StartsWith("Uninstall:"))
                    uninstallEvents.Add(log.Events[i]);
            }
            CollectionAssert.AreEqual(
                new[] { "Uninstall:C", "Uninstall:B", "Uninstall:A" },
                uninstallEvents);
        }

        // CROWN JEWEL #2 — same context object, not a re-fabricated one.
        [Test]
        public void UninstallAll_PassesSameContextObjectUsedAtInstall()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            SpyPassive b = MakeSpy("B");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, b });
            loadout.InstallAll(null, null);

            object aInstallToken = a.InstallContexts[0].SourceToken;
            object bInstallToken = b.InstallContexts[0].SourceToken;

            // Act
            loadout.UninstallAll(null);

            // Assert — SourceToken object identity is the proxy we use to
            // prove "same context". If BrawlerLoadout re-built the context
            // for uninstall, the new SourceToken would be a fresh object().
            Assert.AreSame(aInstallToken, a.UninstallContexts[0].SourceToken,
                "Uninstall context for A should carry the same SourceToken " +
                "that Install received — otherwise modifier cleanup-by-source " +
                "silently no-ops.");
            Assert.AreSame(bInstallToken, b.UninstallContexts[0].SourceToken,
                "Uninstall context for B should carry the same SourceToken " +
                "that Install received.");
        }

        // CROWN JEWEL #3 — runtime teardown before definition teardown.
        [Test]
        public void UninstallAll_FiresRuntimeOnUninstalled_BeforeDefinitionUninstall()
        {
            // Arrange
            var log = new CallLog();
            var runtime = new SpyRuntime { SharedLog = log, Label = "A" };
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A", log, runtime);
            loadout.SetEquippedPassives(new PassiveDefinition[] { a });
            loadout.InstallAll(null, null);

            // Act
            loadout.UninstallAll(null);

            // Assert — find the indices of the two uninstall events; the
            // runtime callback must come first. We read indices instead of
            // asserting the full log because the install-side events would
            // make this brittle to additions.
            int runtimeIdx = log.Events.IndexOf("Runtime.OnUninstalled:A");
            int defIdx = log.Events.IndexOf("Uninstall:A");

            Assert.GreaterOrEqual(runtimeIdx, 0,
                "Runtime.OnUninstalled should have been called.");
            Assert.GreaterOrEqual(defIdx, 0,
                "Definition.Uninstall should have been called.");
            Assert.Less(runtimeIdx, defIdx,
                "Runtime.OnUninstalled must fire BEFORE Definition.Uninstall " +
                "so the runtime can read state the definition is about to tear down.");
        }

        // ---------- D. State after uninstall ----------

        [Test]
        public void UninstallAll_DoesNotClear_EquippedPassivesList()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            SpyPassive b = MakeSpy("B");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, b });
            loadout.InstallAll(null, null);

            // Act
            loadout.UninstallAll(null);

            // Assert — the equipped list survives an uninstall; only the
            // *installed* runtime tuples are torn down. This is what lets
            // SetEquippedPassives + UninstallAll + InstallAll do a clean swap.
            Assert.AreEqual(2, loadout.EquippedPassives.Count);
            Assert.AreSame(a, loadout.EquippedPassives[0]);
            Assert.AreSame(b, loadout.EquippedPassives[1]);
        }

        [Test]
        public void UninstallAll_IsIdempotent_AfterFirstCallClearsInstalledList()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a });
            loadout.InstallAll(null, null);
            loadout.UninstallAll(null);
            Assert.AreEqual(1, a.UninstallCallCount, "sanity: first uninstall fired");

            // Act
            loadout.UninstallAll(null);

            // Assert — second uninstall must NOT call the spy again. Pins
            // "the installed list was cleared after the first uninstall".
            Assert.AreEqual(1, a.UninstallCallCount,
                "Second UninstallAll should be a no-op — installed list " +
                "should have been cleared by the first call.");
        }

        [Test]
        public void InstallAll_AfterUninstallAll_ReinstallsEquippedDefinitions_WithFreshTokens()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a });
            loadout.InstallAll(null, null);
            object firstToken = a.InstallContexts[0].SourceToken;
            loadout.UninstallAll(null);

            // Act
            loadout.InstallAll(null, null);

            // Assert
            Assert.AreEqual(2, a.InstallCallCount, "second install should fire");
            object secondToken = a.InstallContexts[1].SourceToken;
            Assert.IsNotNull(secondToken);
            Assert.AreNotSame(firstToken, secondToken,
                "Each install pass must mint a fresh SourceToken so the new " +
                "modifiers can be cleaned up independently of the prior pass.");
        }

        // ---------- E. Empty / no-equipped state ----------

        [Test]
        public void InstallAll_WithNoEquippedPassives_DoesNotThrow_AndInvokesNoSpies()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A"); // not equipped, just observed

            // Act + Assert
            Assert.DoesNotThrow(() => loadout.InstallAll(null, null));
            Assert.AreEqual(0, a.InstallCallCount);
            Assert.AreEqual(0, a.CreateRuntimeCallCount);
        }

        [Test]
        public void UninstallAll_WithNothingInstalled_DoesNotThrow()
        {
            // Arrange
            var loadout = new BrawlerLoadout();

            // Act + Assert
            Assert.DoesNotThrow(() => loadout.UninstallAll(null));
        }

        // ---------- Bonus — dedupe contract ----------
        //
        // SetEquippedPassives skips duplicate definitions. We could pin this
        // through the EquippedPassives list directly, but routing it through
        // the spy proves the dedupe survives all the way to the install loop —
        // exactly one Install call no matter how many times the same
        // definition was added.

        [Test]
        public void InstallAll_DoesNotDoubleInstall_WhenSamePassiveIsEquippedTwice()
        {
            // Arrange
            var loadout = new BrawlerLoadout();
            SpyPassive a = MakeSpy("A");
            loadout.SetEquippedPassives(new PassiveDefinition[] { a, a, a });

            // Act
            loadout.InstallAll(null, null);

            // Assert
            Assert.AreEqual(1, a.InstallCallCount);
        }
    }
}
