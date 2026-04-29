using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Spy-on-hooks tests for BrawlerLoadout's super-charge source pipeline.
    //
    // SUT:   BrawlerLoadout.{InstallSuperChargeSources, UninstallAllSuperChargeSources,
    //        TickSuperChargeSources, NotifyDamageDealt, NotifyHealApplied}
    // SPY:   SpySuperChargeSource (test-only SuperChargeSourceDefinition subclass)
    //        + SpyChargeRuntime (test-only SuperChargeSourceRuntime subclass with all
    //        5 hooks overridden to append into a shared chronological CallLog).
    //
    // PARALLEL TO THE PASSIVE FIXTURE:
    // BrawlerLoadoutPassiveLifecycleTests already pins the passive Install/Uninstall
    // pipeline using the same spy-on-hooks shape. This fixture is the structural twin
    // for super-charge sources — same five-hook surface (OnInstalled, OnUninstalled,
    // Tick, OnDamageDealt, OnHealApplied) but applied to a different runtime base
    // class. The super-charge pipeline was added in Session 4 and has been entirely
    // unverified by tests until now; the parallel fixture closes that gap.
    //
    // STRUCTURAL LUCK (same as the passive fixture):
    // BrawlerLoadout.InstallSuperChargeSources reads only `definition.SuperChargeSources`
    // and calls `CreateRuntime()` per source; the hook callbacks are null-conditional
    // (`runtime?.OnInstalled(target)`). So we can pass `target = null` for every test
    // — the spy hooks ignore the target argument completely, and the Loadout never
    // dereferences it. This keeps us isolated from the wide BrawlerState dependency
    // graph.
    //
    // CROWN-JEWEL TESTS in this fixture:
    //   1. UninstallAllSuperChargeSources_CallsOnUninstalled_InReverseInstallOrder
    //      Pins LIFO teardown. Same regression risk as the passive fixture's
    //      reverse-order crown jewel: a refactor that flips the loop direction
    //      would silently change teardown semantics. For super-charge sources this
    //      matters because some sources may rely on others' state during
    //      OnUninstalled — install order should reverse on tear-down.
    //
    //   2. InstallSuperChargeSources_SkipsDisabledSources
    //      The `Enabled` field on SuperChargeSourceDefinition is a designer-
    //      facing toggle: "deactivate this source without deleting the asset."
    //      If a refactor drops the `!sourceDefinition.Enabled` check, every
    //      disabled source silently turns back on. Designer-facing behavior is
    //      easy to break and hard to notice in playtest.
    //
    //   3. NotifyDamageDealt_FansToAllInstalledRuntimes_WithProvidedAmountAndVictim
    //      The Notify* methods are the whole point of this system — push-based
    //      event fan-out from the damage/heal pipelines into every installed
    //      source. If one fans incorrectly (skipping a runtime, passing wrong
    //      args), super-charge meters fill at wrong rates — a balance bug
    //      that's invisible per-tick but compounds across a match.

    public class BrawlerLoadoutSuperChargeSourceLifecycleTests
    {
        // ---------- Test doubles ----------

        // Shared chronological log across spies, so a single test can assert on
        // inter-spy ordering ("OnInstalled A, OnInstalled B, OnUninstalled B, OnUninstalled A").
        private sealed class CallLog
        {
            public readonly List<string> Events = new List<string>();
            public void Append(string e) => Events.Add(e);
        }

        // SpyChargeRuntime — concrete SuperChargeSourceRuntime that records every
        // hook call into the shared log + per-instance counters/captures.
        private sealed class SpyChargeRuntime : SuperChargeSourceRuntime
        {
            public string Label;
            public CallLog SharedLog;

            public int OnInstalledCount;
            public int OnUninstalledCount;
            public int TickCount;
            public int OnDamageDealtCount;
            public int OnHealAppliedCount;

            // Capture last-call args so we can assert on argument propagation
            // without dragging in the full BrawlerState graph.
            public float LastTickDeltaTime;
            public uint LastTickCurrentTick;
            public float LastDamageAmount;
            public BrawlerState LastDamageVictim;
            public float LastHealAmount;
            public BrawlerState LastHealRecipient;

            public override void OnInstalled(BrawlerState owner)
            {
                OnInstalledCount++;
                SharedLog?.Append("OnInstalled:" + Label);
            }

            public override void OnUninstalled(BrawlerState owner)
            {
                OnUninstalledCount++;
                SharedLog?.Append("OnUninstalled:" + Label);
            }

            public override void Tick(BrawlerState owner, float deltaTime, uint currentTick)
            {
                TickCount++;
                LastTickDeltaTime = deltaTime;
                LastTickCurrentTick = currentTick;
                SharedLog?.Append("Tick:" + Label);
            }

            public override void OnDamageDealt(BrawlerState owner, float damageAmount, BrawlerState victim)
            {
                OnDamageDealtCount++;
                LastDamageAmount = damageAmount;
                LastDamageVictim = victim;
                SharedLog?.Append("OnDamageDealt:" + Label);
            }

            public override void OnHealApplied(BrawlerState owner, float healAmount, BrawlerState recipient)
            {
                OnHealAppliedCount++;
                LastHealAmount = healAmount;
                LastHealRecipient = recipient;
                SharedLog?.Append("OnHealApplied:" + Label);
            }
        }

        // SpySuperChargeSource — concrete SuperChargeSourceDefinition that returns
        // a configured SpyChargeRuntime from CreateRuntime(). RuntimeToReturn is
        // settable so a test can force the null-runtime path.
        private sealed class SpySuperChargeSource : SuperChargeSourceDefinition
        {
            public SpyChargeRuntime RuntimeToReturn;
            public bool ReturnNullRuntime;
            public int CreateRuntimeCallCount;

            public override SuperChargeSourceRuntime CreateRuntime()
            {
                CreateRuntimeCallCount++;
                if (ReturnNullRuntime)
                    return null;
                return RuntimeToReturn;
            }
        }

        // ---------- ScriptableObject lifecycle housekeeping ----------
        // Same pattern as BrawlerLoadoutPassiveLifecycleTests / BrawlerBuildResolverTests.

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

        // ---------- Builders ----------

        private SpySuperChargeSource MakeSpySource(
            string label,
            CallLog log = null,
            bool enabled = true,
            bool returnNullRuntime = false)
        {
            SpySuperChargeSource source = Track(ScriptableObject.CreateInstance<SpySuperChargeSource>());
            source.Enabled = enabled;
            source.ReturnNullRuntime = returnNullRuntime;
            if (!returnNullRuntime)
            {
                source.RuntimeToReturn = new SpyChargeRuntime
                {
                    Label = label,
                    SharedLog = log,
                };
            }
            return source;
        }

        private BrawlerDefinition MakeDefinition(params SuperChargeSourceDefinition[] sources)
        {
            BrawlerDefinition def = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            def.SuperChargeSources = sources;
            return def;
        }

        // ====================================================================
        // A. InstallSuperChargeSources — cardinality + ordering
        // ====================================================================

        [Test]
        public void InstallSuperChargeSources_CallsCreateRuntime_OncePerEnabledSource()
        {
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A");
            SpySuperChargeSource b = MakeSpySource("B");
            SpySuperChargeSource c = MakeSpySource("C");
            BrawlerDefinition def = MakeDefinition(a, b, c);

            loadout.InstallSuperChargeSources(null, def);

            Assert.AreEqual(1, a.CreateRuntimeCallCount);
            Assert.AreEqual(1, b.CreateRuntimeCallCount);
            Assert.AreEqual(1, c.CreateRuntimeCallCount);
        }

        [Test]
        public void InstallSuperChargeSources_CallsOnInstalled_InDefinitionOrder()
        {
            var log = new CallLog();
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A", log);
            SpySuperChargeSource b = MakeSpySource("B", log);
            SpySuperChargeSource c = MakeSpySource("C", log);
            BrawlerDefinition def = MakeDefinition(a, b, c);

            loadout.InstallSuperChargeSources(null, def);

            // Pin definition-array order via the chronological log.
            CollectionAssert.AreEqual(
                new[] { "OnInstalled:A", "OnInstalled:B", "OnInstalled:C" },
                log.Events);
        }

        [Test]
        public void InstallSuperChargeSources_AddsRuntimeToInstalledList_OncePerEnabledSource()
        {
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A");
            SpySuperChargeSource b = MakeSpySource("B");
            BrawlerDefinition def = MakeDefinition(a, b);

            loadout.InstallSuperChargeSources(null, def);

            Assert.AreEqual(2, loadout.InstalledSuperChargeSources.Count);
            Assert.AreSame(a.RuntimeToReturn, loadout.InstalledSuperChargeSources[0]);
            Assert.AreSame(b.RuntimeToReturn, loadout.InstalledSuperChargeSources[1]);
        }

        // ====================================================================
        // B. Install gates — disabled sources, null sources, null runtimes, null def
        // ====================================================================

        // CROWN JEWEL.
        [Test]
        public void InstallSuperChargeSources_SkipsDisabledSources()
        {
            // The Enabled flag on SuperChargeSourceDefinition is the designer-
            // facing kill switch ("deactivate without deleting the asset"). If
            // anyone removes the `!sourceDefinition.Enabled` check from the
            // production loop, every disabled source silently turns on.
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource enabledA = MakeSpySource("A", enabled: true);
            SpySuperChargeSource disabledB = MakeSpySource("B", enabled: false);
            SpySuperChargeSource enabledC = MakeSpySource("C", enabled: true);
            BrawlerDefinition def = MakeDefinition(enabledA, disabledB, enabledC);

            loadout.InstallSuperChargeSources(null, def);

            Assert.AreEqual(1, enabledA.CreateRuntimeCallCount, "Enabled A: CreateRuntime fires");
            Assert.AreEqual(0, disabledB.CreateRuntimeCallCount, "Disabled B: CreateRuntime must NOT fire");
            Assert.AreEqual(1, enabledC.CreateRuntimeCallCount, "Enabled C: CreateRuntime fires");

            Assert.AreEqual(2, loadout.InstalledSuperChargeSources.Count,
                "Only enabled runtimes added to installed list");
            Assert.AreSame(enabledA.RuntimeToReturn, loadout.InstalledSuperChargeSources[0]);
            Assert.AreSame(enabledC.RuntimeToReturn, loadout.InstalledSuperChargeSources[1]);
        }

        [Test]
        public void InstallSuperChargeSources_SkipsNullSourceEntries()
        {
            // A null entry in the SuperChargeSources array (designer authoring
            // mishap) must not NRE.
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A");
            SpySuperChargeSource b = MakeSpySource("B");
            BrawlerDefinition def = MakeDefinition(null, a, null, b, null);

            Assert.DoesNotThrow(() => loadout.InstallSuperChargeSources(null, def));

            Assert.AreEqual(2, loadout.InstalledSuperChargeSources.Count);
            Assert.AreSame(a.RuntimeToReturn, loadout.InstalledSuperChargeSources[0]);
            Assert.AreSame(b.RuntimeToReturn, loadout.InstalledSuperChargeSources[1]);
        }

        [Test]
        public void InstallSuperChargeSources_SkipsSources_WhenCreateRuntimeReturnsNull()
        {
            // CreateRuntime returning null is documented as tolerated. The
            // source is skipped — not added to the installed list, no NRE.
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource normal = MakeSpySource("normal");
            SpySuperChargeSource nullReturning = MakeSpySource("nullR", returnNullRuntime: true);
            BrawlerDefinition def = MakeDefinition(normal, nullReturning);

            Assert.DoesNotThrow(() => loadout.InstallSuperChargeSources(null, def));

            Assert.AreEqual(1, normal.CreateRuntimeCallCount);
            Assert.AreEqual(1, nullReturning.CreateRuntimeCallCount,
                "CreateRuntime IS called even on null-returning sources (the source isn't pre-filtered)");
            Assert.AreEqual(1, loadout.InstalledSuperChargeSources.Count,
                "Only the non-null runtime is added to the installed list");
            Assert.AreSame(normal.RuntimeToReturn, loadout.InstalledSuperChargeSources[0]);
        }

        [Test]
        public void InstallSuperChargeSources_NoOp_WhenDefinitionIsNull()
        {
            var loadout = new BrawlerLoadout();

            Assert.DoesNotThrow(() => loadout.InstallSuperChargeSources(null, null));
            Assert.AreEqual(0, loadout.InstalledSuperChargeSources.Count);
        }

        [Test]
        public void InstallSuperChargeSources_NoOp_WhenSuperChargeSourcesArrayIsNull()
        {
            var loadout = new BrawlerLoadout();
            BrawlerDefinition def = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            // def.SuperChargeSources stays at its default (null) — fresh SO authoring.

            Assert.DoesNotThrow(() => loadout.InstallSuperChargeSources(null, def));
            Assert.AreEqual(0, loadout.InstalledSuperChargeSources.Count);
        }

        // ====================================================================
        // C. UninstallAllSuperChargeSources — reverse-order, clears list
        // ====================================================================

        [Test]
        public void UninstallAllSuperChargeSources_CallsOnUninstalled_OncePerInstalledRuntime()
        {
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A");
            SpySuperChargeSource b = MakeSpySource("B");
            BrawlerDefinition def = MakeDefinition(a, b);
            loadout.InstallSuperChargeSources(null, def);

            loadout.UninstallAllSuperChargeSources(null);

            Assert.AreEqual(1, a.RuntimeToReturn.OnUninstalledCount);
            Assert.AreEqual(1, b.RuntimeToReturn.OnUninstalledCount);
        }

        // CROWN JEWEL.
        [Test]
        public void UninstallAllSuperChargeSources_CallsOnUninstalled_InReverseInstallOrder()
        {
            // Install A, B, C → OnInstalled fires in order. Uninstall must
            // walk LIFO: C, B, A. A refactor that flips the loop direction
            // would break the contract silently.
            var log = new CallLog();
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A", log);
            SpySuperChargeSource b = MakeSpySource("B", log);
            SpySuperChargeSource c = MakeSpySource("C", log);
            BrawlerDefinition def = MakeDefinition(a, b, c);
            loadout.InstallSuperChargeSources(null, def);

            // Sanity: install order is A, B, C.
            CollectionAssert.AreEqual(
                new[] { "OnInstalled:A", "OnInstalled:B", "OnInstalled:C" },
                log.Events);

            log.Events.Clear();
            loadout.UninstallAllSuperChargeSources(null);

            // The contract under test: reverse install order.
            CollectionAssert.AreEqual(
                new[] { "OnUninstalled:C", "OnUninstalled:B", "OnUninstalled:A" },
                log.Events);
        }

        [Test]
        public void UninstallAllSuperChargeSources_ClearsInstalledList()
        {
            var loadout = new BrawlerLoadout();
            BrawlerDefinition def = MakeDefinition(MakeSpySource("A"), MakeSpySource("B"));
            loadout.InstallSuperChargeSources(null, def);
            Assert.AreEqual(2, loadout.InstalledSuperChargeSources.Count, "Pre-condition: 2 installed");

            loadout.UninstallAllSuperChargeSources(null);

            Assert.AreEqual(0, loadout.InstalledSuperChargeSources.Count);
        }

        [Test]
        public void UninstallAllSuperChargeSources_NoOp_WhenNothingInstalled()
        {
            var loadout = new BrawlerLoadout();

            Assert.DoesNotThrow(() => loadout.UninstallAllSuperChargeSources(null));
        }

        // ====================================================================
        // D. TickSuperChargeSources fan-out
        // ====================================================================

        [Test]
        public void TickSuperChargeSources_CallsTick_OnEveryInstalled_WithProvidedDeltaAndCurrentTick()
        {
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A");
            SpySuperChargeSource b = MakeSpySource("B");
            BrawlerDefinition def = MakeDefinition(a, b);
            loadout.InstallSuperChargeSources(null, def);

            const float dt = 1f / 30f;
            const uint tick = 1234;

            loadout.TickSuperChargeSources(null, dt, tick);

            Assert.AreEqual(1, a.RuntimeToReturn.TickCount);
            Assert.AreEqual(1, b.RuntimeToReturn.TickCount);

            Assert.AreEqual(dt, a.RuntimeToReturn.LastTickDeltaTime);
            Assert.AreEqual(dt, b.RuntimeToReturn.LastTickDeltaTime);
            Assert.AreEqual(tick, a.RuntimeToReturn.LastTickCurrentTick);
            Assert.AreEqual(tick, b.RuntimeToReturn.LastTickCurrentTick);
        }

        [Test]
        public void TickSuperChargeSources_NoOp_WhenNoneInstalled()
        {
            var loadout = new BrawlerLoadout();

            Assert.DoesNotThrow(() => loadout.TickSuperChargeSources(null, 1f / 30f, 0));
        }

        // ====================================================================
        // E. NotifyDamageDealt fan-out
        // ====================================================================

        // CROWN JEWEL.
        [Test]
        public void NotifyDamageDealt_FansToAllInstalledRuntimes_WithProvidedAmountAndVictim()
        {
            // The push-based fan-out is the entire reason this system exists.
            // Pin: every installed runtime sees the call, with the same args
            // the caller provided.
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A");
            SpySuperChargeSource b = MakeSpySource("B");
            SpySuperChargeSource c = MakeSpySource("C");
            BrawlerDefinition def = MakeDefinition(a, b, c);
            loadout.InstallSuperChargeSources(null, def);

            const float damage = 42.5f;
            BrawlerState victim = null; // POCO; null OK for the spy hooks

            loadout.NotifyDamageDealt(null, damage, victim);

            Assert.AreEqual(1, a.RuntimeToReturn.OnDamageDealtCount);
            Assert.AreEqual(1, b.RuntimeToReturn.OnDamageDealtCount);
            Assert.AreEqual(1, c.RuntimeToReturn.OnDamageDealtCount);

            Assert.AreEqual(damage, a.RuntimeToReturn.LastDamageAmount);
            Assert.AreEqual(damage, b.RuntimeToReturn.LastDamageAmount);
            Assert.AreEqual(damage, c.RuntimeToReturn.LastDamageAmount);

            Assert.AreSame(victim, a.RuntimeToReturn.LastDamageVictim);
            Assert.AreSame(victim, b.RuntimeToReturn.LastDamageVictim);
            Assert.AreSame(victim, c.RuntimeToReturn.LastDamageVictim);
        }

        [Test]
        public void NotifyDamageDealt_NoOp_WhenNoneInstalled()
        {
            var loadout = new BrawlerLoadout();

            Assert.DoesNotThrow(() => loadout.NotifyDamageDealt(null, 100f, null));
        }

        // ====================================================================
        // F. NotifyHealApplied fan-out
        // ====================================================================

        [Test]
        public void NotifyHealApplied_FansToAllInstalledRuntimes_WithProvidedAmountAndRecipient()
        {
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A");
            SpySuperChargeSource b = MakeSpySource("B");
            BrawlerDefinition def = MakeDefinition(a, b);
            loadout.InstallSuperChargeSources(null, def);

            const float heal = 250f;
            BrawlerState recipient = null;

            loadout.NotifyHealApplied(null, heal, recipient);

            Assert.AreEqual(1, a.RuntimeToReturn.OnHealAppliedCount);
            Assert.AreEqual(1, b.RuntimeToReturn.OnHealAppliedCount);

            Assert.AreEqual(heal, a.RuntimeToReturn.LastHealAmount);
            Assert.AreEqual(heal, b.RuntimeToReturn.LastHealAmount);

            Assert.AreSame(recipient, a.RuntimeToReturn.LastHealRecipient);
            Assert.AreSame(recipient, b.RuntimeToReturn.LastHealRecipient);
        }

        [Test]
        public void NotifyHealApplied_NoOp_WhenNoneInstalled()
        {
            var loadout = new BrawlerLoadout();

            Assert.DoesNotThrow(() => loadout.NotifyHealApplied(null, 100f, null));
        }

        // ====================================================================
        // G. End-to-end lifecycle invariants
        // ====================================================================

        [Test]
        public void AfterUninstallAll_TickAndNotify_AreNoOpAcrossAllFormerlyInstalledRuntimes()
        {
            // Once UninstallAll has run, the installed-list is clear and any
            // subsequent Tick/Notify must not reach the formerly-installed
            // runtimes. Combined check across all three fan-out methods.
            var loadout = new BrawlerLoadout();
            SpySuperChargeSource a = MakeSpySource("A");
            SpySuperChargeSource b = MakeSpySource("B");
            BrawlerDefinition def = MakeDefinition(a, b);
            loadout.InstallSuperChargeSources(null, def);

            loadout.UninstallAllSuperChargeSources(null);

            loadout.TickSuperChargeSources(null, 1f / 30f, 0);
            loadout.NotifyDamageDealt(null, 100f, null);
            loadout.NotifyHealApplied(null, 100f, null);

            Assert.AreEqual(0, a.RuntimeToReturn.TickCount, "A: no tick after uninstall");
            Assert.AreEqual(0, b.RuntimeToReturn.TickCount, "B: no tick after uninstall");
            Assert.AreEqual(0, a.RuntimeToReturn.OnDamageDealtCount, "A: no damage after uninstall");
            Assert.AreEqual(0, b.RuntimeToReturn.OnDamageDealtCount, "B: no damage after uninstall");
            Assert.AreEqual(0, a.RuntimeToReturn.OnHealAppliedCount, "A: no heal after uninstall");
            Assert.AreEqual(0, b.RuntimeToReturn.OnHealAppliedCount, "B: no heal after uninstall");
        }
    }
}
