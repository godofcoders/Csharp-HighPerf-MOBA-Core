using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerLoadout HELPERS — the non-lifecycle public surface:
    //
    //   Construction defaults              (CurrentPowerLevel, RuntimeBuild,
    //                                       RuntimeKit, HyperchargeModifierSource)
    //   SetPowerLevel(int)                 (clamp-below-one)
    //   SetEquippedHypercharge(def)        (simple setter)
    //   SetEquippedPassives(IEnumerable)   (replace + dedupe + null-skip)
    //   RefreshRuntimeBuildUnlockState     (null-safe, pushes 5 slot flags)
    //   ResetRuntimeState                  (clears + refreshes)
    //   HasUnlocked*SlotPassthroughs       (4 convenience reads)
    //   GetCurrent*Definition              (4 lookups, RuntimeKit-first w/ fallbacks)
    //
    // OUT OF SCOPE for this fixture (covered elsewhere or future work):
    //   - InstallAll / UninstallAll / TickPassives — covered by
    //     BrawlerLoadoutPassiveLifecycleTests (spy-on-hooks fixture)
    //   - InstallSuperChargeSources / UninstallAllSuperChargeSources / Tick* /
    //     Notify* — separate lifecycle, would deserve its own fixture if ever
    //     wanted (BrawlerLoadoutSuperChargeSourceLifecycleTests, parallel
    //     shape to the passive one)
    //
    // PATTERNS REUSED (no new ones introduced — this is the closer of the
    // testing arc, applying what the previous six fixtures established):
    //   - POCO value testing (most tests — `new BrawlerLoadout()` + assert)
    //   - ScriptableObject lifecycle (Track<T> + TearDown DestroyImmediate,
    //     same as BrawlerBuildResolverTests / BrawlerBuildLayoutDefinitionTests)
    //   - [TestCase] parameterization (SetPowerLevel clamp, gear-slot OR truth
    //     table, slot-unlock pass-through asserts)
    //   - Truth-table testing (HasAnyUnlockedGearSlot OR over two booleans —
    //     2^2 = 4 rows)
    //   - Crown-jewel naming (3 named below)
    //   - Test-against-contract (assert through the public API; never reach
    //     into _equippedPassives or _installedPassives)
    //
    // CROWN-JEWEL TESTS:
    //   1. Construction_HyperchargeModifierSourceIsNonNull_AndUniquePerInstance
    //      The production doc-comment explicitly warns: "two brawlers must
    //      not share the same token or their modifier cleanups would
    //      collide." This test pins `HyperchargeModifierSource` as a fresh
    //      `new object()` per instance. If anyone refactors to a static
    //      field (a tempting "optimization"), modifier teardown would
    //      cross-contaminate between brawlers — invisible until two
    //      hypercharges run concurrently and one's stat-modifier cleanup
    //      removes the OTHER brawler's modifiers.
    //
    //   2. SetEquippedPassives_ReplacesPreviousList_NotAppends
    //      The doc-comment says "Replaces the equipped-passive list."
    //      Calling SetEquippedPassives twice must result in the SECOND
    //      call's contents — not a concatenation. If someone changes the
    //      `Clear()` at the top of the method to a no-op, this test
    //      catches it. Append-by-accident would mean swapping a build mid-
    //      match would silently grow the passive list every swap.
    //
    //   3. HasAnyUnlockedGearSlot_TruthTable
    //      Four rows for the OR over two booleans (gear1, gear2). If
    //      anyone flips OR to AND ("gear is available only when BOTH slots
    //      are unlocked"), three of four rows fail loudly — matching the
    //      truth-table pattern's strength: multi-row failure when a
    //      logical operator gets flipped.

    public class BrawlerLoadoutHelperTests
    {
        // ---------- Test-only concrete subclasses ----------
        // Several SO base classes are abstract; we need concrete subclasses
        // to call ScriptableObject.CreateInstance on them.
        //
        //   PassiveDefinition: abstract — TestStarPowerDefinition (empty
        //     subclass; StarPowerDefinition itself is concrete).
        //   AbilityDefinition: abstract, single abstract method
        //     CreateLogic() — TestAbilityDefinition returns null.
        //   GadgetDefinition: abstract (inherits CreateLogic from
        //     AbilityDefinition) — TestGadgetDefinition returns null.
        //
        // None of these are exercised by the helpers under test (which
        // only STORE references), so returning null from CreateLogic is
        // safe. Same pattern as TestGadgetDefinition in
        // BrawlerBuildResolverTests.

        private sealed class TestStarPowerDefinition : StarPowerDefinition { }

        private sealed class TestAbilityDefinition : AbilityDefinition
        {
            public override IAbilityLogic CreateLogic() => null;
        }

        private sealed class TestGadgetDefinition : GadgetDefinition
        {
            public override IAbilityLogic CreateLogic() => null;
        }

        // ---------- ScriptableObject lifecycle ----------
        // Same pattern as BrawlerBuildResolverTests / BrawlerBuildLayoutDefinitionTests.

        private List<Object> _spawned;

        [SetUp]
        public void SetUp() => _spawned = new List<Object>();

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

        private TestStarPowerDefinition MakePassive() =>
            Track(ScriptableObject.CreateInstance<TestStarPowerDefinition>());

        private TestGadgetDefinition MakeGadget() =>
            Track(ScriptableObject.CreateInstance<TestGadgetDefinition>());

        private TestAbilityDefinition MakeAbility() =>
            Track(ScriptableObject.CreateInstance<TestAbilityDefinition>());

        private HyperchargeDefinition MakeHypercharge() =>
            Track(ScriptableObject.CreateInstance<HyperchargeDefinition>());

        // ====================================================================
        // A. Construction
        // ====================================================================

        [Test]
        public void Construction_DefaultsCurrentPowerLevelToOne()
        {
            var loadout = new BrawlerLoadout();

            Assert.AreEqual(1, loadout.CurrentPowerLevel);
        }

        [Test]
        public void Construction_RuntimeBuildAndRuntimeKitAreNonNull()
        {
            var loadout = new BrawlerLoadout();

            Assert.NotNull(loadout.RuntimeBuild);
            Assert.NotNull(loadout.RuntimeKit);
        }

        [Test]
        public void Construction_HyperchargeModifierSourceIsNonNull_AndUniquePerInstance()
        {
            // CROWN JEWEL.
            // Two BrawlerLoadout instances must NOT share the same modifier
            // source token. The token is used as the lookup key when the
            // hypercharge system removes its stat modifiers; if two
            // brawlers shared a token, one's cleanup would tear down the
            // OTHER's modifiers. The production doc-comment explicitly
            // warns about this.
            var a = new BrawlerLoadout();
            var b = new BrawlerLoadout();

            Assert.NotNull(a.HyperchargeModifierSource);
            Assert.NotNull(b.HyperchargeModifierSource);
            Assert.AreNotSame(a.HyperchargeModifierSource, b.HyperchargeModifierSource,
                "Each BrawlerLoadout must have its OWN HyperchargeModifierSource " +
                "instance, otherwise stat-modifier cleanups across brawlers collide.");
        }

        // ====================================================================
        // B. SetPowerLevel
        // ====================================================================

        [TestCase(-5)]
        [TestCase(-1)]
        [TestCase(0)]
        public void SetPowerLevel_ClampsToOne_WhenInputBelowOne(int input)
        {
            var loadout = new BrawlerLoadout();

            loadout.SetPowerLevel(input);

            Assert.AreEqual(1, loadout.CurrentPowerLevel,
                $"Input {input} must clamp to 1 (the minimum power level).");
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(11)]
        public void SetPowerLevel_StoresValue_WhenAtOrAboveOne(int input)
        {
            var loadout = new BrawlerLoadout();

            loadout.SetPowerLevel(input);

            Assert.AreEqual(input, loadout.CurrentPowerLevel);
        }

        [Test]
        public void SetPowerLevel_ReassignsExistingValue()
        {
            // Idempotency canary: calling SetPowerLevel twice should leave
            // the brawler at the second value, not concatenate or otherwise
            // accumulate.
            var loadout = new BrawlerLoadout();

            loadout.SetPowerLevel(7);
            loadout.SetPowerLevel(3);

            Assert.AreEqual(3, loadout.CurrentPowerLevel);
        }

        // ====================================================================
        // C. SetEquippedHypercharge
        // ====================================================================

        [Test]
        public void SetEquippedHypercharge_StoresProvidedDefinition()
        {
            var loadout = new BrawlerLoadout();
            HyperchargeDefinition hc = MakeHypercharge();

            loadout.SetEquippedHypercharge(hc);

            Assert.AreSame(hc, loadout.EquippedHypercharge);
        }

        [Test]
        public void SetEquippedHypercharge_AcceptsNull_ClearsValue()
        {
            // Null is a valid input — used to "unequip" a hypercharge.
            var loadout = new BrawlerLoadout();
            loadout.SetEquippedHypercharge(MakeHypercharge());

            loadout.SetEquippedHypercharge(null);

            Assert.IsNull(loadout.EquippedHypercharge);
        }

        // ====================================================================
        // D. SetEquippedPassives
        // ====================================================================

        [Test]
        public void SetEquippedPassives_StoresInOrder_WithoutModification()
        {
            var loadout = new BrawlerLoadout();
            var p1 = MakePassive();
            var p2 = MakePassive();
            var p3 = MakePassive();

            loadout.SetEquippedPassives(new PassiveDefinition[] { p1, p2, p3 });

            Assert.AreEqual(3, loadout.EquippedPassives.Count);
            Assert.AreSame(p1, loadout.EquippedPassives[0]);
            Assert.AreSame(p2, loadout.EquippedPassives[1]);
            Assert.AreSame(p3, loadout.EquippedPassives[2]);
        }

        [Test]
        public void SetEquippedPassives_DedupesDuplicateInstances()
        {
            // Same reference twice → stored once. Different instances of the
            // same SO subclass would NOT dedupe (they'd be two distinct
            // references), but that's fine — designers shouldn't author
            // duplicates and accidental re-references should collapse.
            var loadout = new BrawlerLoadout();
            var p1 = MakePassive();
            var p2 = MakePassive();

            loadout.SetEquippedPassives(new PassiveDefinition[] { p1, p2, p1, p2 });

            Assert.AreEqual(2, loadout.EquippedPassives.Count);
            Assert.AreSame(p1, loadout.EquippedPassives[0]);
            Assert.AreSame(p2, loadout.EquippedPassives[1]);
        }

        [Test]
        public void SetEquippedPassives_SkipsNullEntries()
        {
            var loadout = new BrawlerLoadout();
            var p1 = MakePassive();
            var p2 = MakePassive();

            loadout.SetEquippedPassives(new PassiveDefinition[] { null, p1, null, p2, null });

            Assert.AreEqual(2, loadout.EquippedPassives.Count);
            Assert.AreSame(p1, loadout.EquippedPassives[0]);
            Assert.AreSame(p2, loadout.EquippedPassives[1]);
        }

        [Test]
        public void SetEquippedPassives_ReplacesPreviousList_NotAppends()
        {
            // CROWN JEWEL.
            // The doc-comment says "Replaces the equipped-passive list." A
            // second call must REPLACE the contents, not concatenate. If
            // anyone changes the Clear() at the top of the method to a no-
            // op, this test catches it. Append-by-accident would mean a
            // mid-match build swap silently grows the passive list each
            // time, leading to OOM-style accumulation across many swaps.
            var loadout = new BrawlerLoadout();
            var first = new PassiveDefinition[] { MakePassive(), MakePassive() };
            var second = new PassiveDefinition[] { MakePassive() };

            loadout.SetEquippedPassives(first);
            Assert.AreEqual(2, loadout.EquippedPassives.Count, "Pre-condition: first call stored 2.");

            loadout.SetEquippedPassives(second);

            Assert.AreEqual(1, loadout.EquippedPassives.Count,
                "Second call must REPLACE (count == 1), not append (would be 3).");
            Assert.AreSame(second[0], loadout.EquippedPassives[0]);
        }

        [Test]
        public void SetEquippedPassives_AcceptsNullEnumerable_ClearsList()
        {
            // Null IEnumerable → list is cleared, no NRE. Used by callers
            // that want to wipe the equipped passives entirely.
            var loadout = new BrawlerLoadout();
            loadout.SetEquippedPassives(new PassiveDefinition[] { MakePassive(), MakePassive() });
            Assert.AreEqual(2, loadout.EquippedPassives.Count, "Pre-condition: 2 stored.");

            loadout.SetEquippedPassives(null);

            Assert.AreEqual(0, loadout.EquippedPassives.Count);
        }

        // ====================================================================
        // E. RefreshRuntimeBuildUnlockState
        // ====================================================================

        [Test]
        public void RefreshRuntimeBuildUnlockState_NoOp_WhenDefinitionIsNull()
        {
            // Capture state, call with null, assert flags unchanged. The
            // method must not NRE and must not corrupt RuntimeBuild's flags.
            var loadout = new BrawlerLoadout();
            loadout.RuntimeBuild.SetUnlockedState(true, true, true, true, true);

            loadout.RefreshRuntimeBuildUnlockState(null);

            Assert.IsTrue(loadout.RuntimeBuild.IsGearSlot1Unlocked);
            Assert.IsTrue(loadout.RuntimeBuild.IsGearSlot2Unlocked);
            Assert.IsTrue(loadout.RuntimeBuild.IsGadgetSlotUnlocked);
            Assert.IsTrue(loadout.RuntimeBuild.IsStarPowerSlotUnlocked);
            Assert.IsTrue(loadout.RuntimeBuild.IsHyperchargeSlotUnlocked);
        }

        [Test]
        public void RefreshRuntimeBuildUnlockState_NoOp_WhenBuildLayoutIsNull()
        {
            // BrawlerDefinition exists but its BuildLayout is null. Same
            // contract as above — no NRE, no flag mutation.
            var loadout = new BrawlerLoadout();
            loadout.RuntimeBuild.SetUnlockedState(true, false, true, false, true);

            BrawlerDefinition def = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            // def.BuildLayout stays at its default (null).

            loadout.RefreshRuntimeBuildUnlockState(def);

            Assert.IsTrue(loadout.RuntimeBuild.IsGearSlot1Unlocked);
            Assert.IsFalse(loadout.RuntimeBuild.IsGearSlot2Unlocked);
            Assert.IsTrue(loadout.RuntimeBuild.IsGadgetSlotUnlocked);
            Assert.IsFalse(loadout.RuntimeBuild.IsStarPowerSlotUnlocked);
            Assert.IsTrue(loadout.RuntimeBuild.IsHyperchargeSlotUnlocked);
        }

        [Test]
        public void RefreshRuntimeBuildUnlockState_PushesFiveSlotFlagsBasedOnPowerLevel()
        {
            // Build a layout that mirrors StandardBrawlerBuildLayout's tier
            // shape: gadget@7, gear@8, starpower@9, gear@10, hypercharge@11.
            // At PL 9, only gadget+gear@8+starpower should be unlocked.
            var layout = Track(ScriptableObject.CreateInstance<BrawlerBuildLayoutDefinition>());
            layout.Slots = new[]
            {
                new BrawlerBuildSlotDefinition { SlotId = "gadget_1",      SlotType = BrawlerBuildSlotType.Gadget,      UnlockPowerLevel = 7 },
                new BrawlerBuildSlotDefinition { SlotId = "gear_1",        SlotType = BrawlerBuildSlotType.Gear,        UnlockPowerLevel = 8 },
                new BrawlerBuildSlotDefinition { SlotId = "starpower_1",   SlotType = BrawlerBuildSlotType.StarPower,   UnlockPowerLevel = 9 },
                new BrawlerBuildSlotDefinition { SlotId = "gear_2",        SlotType = BrawlerBuildSlotType.Gear,        UnlockPowerLevel = 10 },
                new BrawlerBuildSlotDefinition { SlotId = "hypercharge_1", SlotType = BrawlerBuildSlotType.Hypercharge, UnlockPowerLevel = 11 },
            };

            BrawlerDefinition def = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            def.BuildLayout = layout;

            var loadout = new BrawlerLoadout();
            loadout.SetPowerLevel(9);

            loadout.RefreshRuntimeBuildUnlockState(def);

            Assert.IsTrue (loadout.RuntimeBuild.IsGadgetSlotUnlocked,      "gadget_1 unlocks at PL 7");
            Assert.IsTrue (loadout.RuntimeBuild.IsGearSlot1Unlocked,       "gear_1 unlocks at PL 8");
            Assert.IsTrue (loadout.RuntimeBuild.IsStarPowerSlotUnlocked,   "starpower_1 unlocks at PL 9 (inclusive)");
            Assert.IsFalse(loadout.RuntimeBuild.IsGearSlot2Unlocked,       "gear_2 unlocks at PL 10 (locked at PL 9)");
            Assert.IsFalse(loadout.RuntimeBuild.IsHyperchargeSlotUnlocked, "hypercharge_1 unlocks at PL 11 (locked at PL 9)");
        }

        // ====================================================================
        // F. ResetRuntimeState
        // ====================================================================

        [Test]
        public void ResetRuntimeState_ClearsRuntimeBuildAndKit()
        {
            // Pre-load state into both, reset, assert cleared.
            var loadout = new BrawlerLoadout();

            loadout.RuntimeBuild.SetEquippedGadget(MakeGadget());
            loadout.RuntimeBuild.SetEquippedHypercharge(MakeHypercharge());
            loadout.RuntimeKit.SetGadget(MakeGadget(), null);

            loadout.ResetRuntimeState(null); // null def is fine; the no-op path of Refresh

            Assert.IsNull(loadout.RuntimeBuild.EquippedGadget,      "RuntimeBuild.EquippedGadget cleared");
            Assert.IsNull(loadout.RuntimeBuild.EquippedHypercharge, "RuntimeBuild.EquippedHypercharge cleared");
            Assert.IsNull(loadout.RuntimeKit.GadgetDefinition,      "RuntimeKit.GadgetDefinition cleared");
        }

        [Test]
        public void ResetRuntimeState_RefreshesUnlockFlagsBasedOnCurrentPowerLevel()
        {
            // Reset with a real BuildLayout: clears RuntimeBuild then re-
            // applies unlock flags for the current PL. Together this means
            // a respawn-time reset doesn't strand the brawler with stale
            // unlock flags.
            var layout = Track(ScriptableObject.CreateInstance<BrawlerBuildLayoutDefinition>());
            layout.Slots = new[]
            {
                new BrawlerBuildSlotDefinition { SlotId = "gadget_1",    SlotType = BrawlerBuildSlotType.Gadget,    UnlockPowerLevel = 7 },
                new BrawlerBuildSlotDefinition { SlotId = "starpower_1", SlotType = BrawlerBuildSlotType.StarPower, UnlockPowerLevel = 9 },
            };

            BrawlerDefinition def = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            def.BuildLayout = layout;

            var loadout = new BrawlerLoadout();
            loadout.SetPowerLevel(8);

            loadout.ResetRuntimeState(def);

            Assert.IsTrue (loadout.RuntimeBuild.IsGadgetSlotUnlocked,    "gadget_1 unlocks at PL 7, available at PL 8");
            Assert.IsFalse(loadout.RuntimeBuild.IsStarPowerSlotUnlocked, "starpower_1 unlocks at PL 9, locked at PL 8");
        }

        // ====================================================================
        // G. Slot-unlock convenience reads
        // ====================================================================

        [TestCase(true,  true)]
        [TestCase(false, false)]
        public void HasUnlockedGadgetSlot_PassesThroughToRuntimeBuild(bool flag, bool expected)
        {
            var loadout = new BrawlerLoadout();
            loadout.RuntimeBuild.SetUnlockedState(false, false, gadget: flag, starPower: false, hypercharge: false);

            Assert.AreEqual(expected, loadout.HasUnlockedGadgetSlot());
        }

        [TestCase(true,  true)]
        [TestCase(false, false)]
        public void HasUnlockedStarPowerSlot_PassesThroughToRuntimeBuild(bool flag, bool expected)
        {
            var loadout = new BrawlerLoadout();
            loadout.RuntimeBuild.SetUnlockedState(false, false, gadget: false, starPower: flag, hypercharge: false);

            Assert.AreEqual(expected, loadout.HasUnlockedStarPowerSlot());
        }

        [TestCase(true,  true)]
        [TestCase(false, false)]
        public void HasUnlockedHyperchargeSlot_PassesThroughToRuntimeBuild(bool flag, bool expected)
        {
            var loadout = new BrawlerLoadout();
            loadout.RuntimeBuild.SetUnlockedState(false, false, gadget: false, starPower: false, hypercharge: flag);

            Assert.AreEqual(expected, loadout.HasUnlockedHyperchargeSlot());
        }

        // CROWN JEWEL.
        // Truth table for OR over two booleans: 2^2 = 4 rows. If anyone
        // flips OR to AND ("any unlocked" → "both unlocked"), three of the
        // four rows fail simultaneously — a much louder regression than
        // a single missed case.
        [TestCase(false, false, false)]
        [TestCase(false, true,  true)]
        [TestCase(true,  false, true)]
        [TestCase(true,  true,  true)]
        public void HasAnyUnlockedGearSlot_TruthTable(bool gear1, bool gear2, bool expected)
        {
            var loadout = new BrawlerLoadout();
            loadout.RuntimeBuild.SetUnlockedState(gear1, gear2, gadget: false, starPower: false, hypercharge: false);

            Assert.AreEqual(expected, loadout.HasAnyUnlockedGearSlot());
        }

        // ====================================================================
        // H. Current ability lookups (RuntimeKit-first with fallbacks)
        // ====================================================================

        [Test]
        public void GetCurrentMainAttackDefinition_ReturnsRuntimeKit_WhenSet()
        {
            var loadout = new BrawlerLoadout();
            AbilityDefinition kitAttack = MakeAbility();
            AbilityDefinition defAttack = MakeAbility();

            loadout.RuntimeKit.SetMainAttack(kitAttack, null);

            BrawlerDefinition def = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            def.MainAttack = defAttack;

            Assert.AreSame(kitAttack, loadout.GetCurrentMainAttackDefinition(def),
                "RuntimeKit value wins over the brawler-definition fallback when set.");
        }

        [Test]
        public void GetCurrentMainAttackDefinition_FallsBackToBrawlerDefinition_WhenRuntimeKitNotSet()
        {
            // Fresh loadout — RuntimeKit.MainAttackDefinition is null.
            // Lookup must fall through to the brawler definition's MainAttack.
            var loadout = new BrawlerLoadout();
            AbilityDefinition defAttack = MakeAbility();

            BrawlerDefinition def = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            def.MainAttack = defAttack;

            Assert.AreSame(defAttack, loadout.GetCurrentMainAttackDefinition(def));
        }

        [Test]
        public void GetCurrentGadgetDefinition_ReturnsRuntimeKitOrNull_NoFallback()
        {
            // Gadget lookup deliberately has NO brawler-definition fallback,
            // because gadgets are equipped per-build and brawler definitions
            // don't carry a default gadget. Pin both branches.
            var loadout = new BrawlerLoadout();

            Assert.IsNull(loadout.GetCurrentGadgetDefinition(),
                "When RuntimeKit.GadgetDefinition is null, lookup returns null (no fallback).");

            GadgetDefinition gadget = MakeGadget();
            loadout.RuntimeKit.SetGadget(gadget, null);

            Assert.AreSame(gadget, loadout.GetCurrentGadgetDefinition(),
                "When RuntimeKit.GadgetDefinition is set, lookup returns it.");
        }

        [Test]
        public void GetCurrentHyperchargeDefinition_FallsBackToEquippedHypercharge()
        {
            // Three branches:
            //   1. RuntimeKit.HyperchargeDefinition set    → return that
            //   2. RuntimeKit null, EquippedHypercharge set → return Equipped
            //   3. Both null                                → null
            var loadout = new BrawlerLoadout();
            HyperchargeDefinition kitHc = MakeHypercharge();
            HyperchargeDefinition equippedHc = MakeHypercharge();

            // (3) Both null
            Assert.IsNull(loadout.GetCurrentHyperchargeDefinition());

            // (2) Equipped only
            loadout.SetEquippedHypercharge(equippedHc);
            Assert.AreSame(equippedHc, loadout.GetCurrentHyperchargeDefinition());

            // (1) RuntimeKit overrides
            loadout.RuntimeKit.SetHypercharge(kitHc);
            Assert.AreSame(kitHc, loadout.GetCurrentHyperchargeDefinition(),
                "RuntimeKit value wins over EquippedHypercharge fallback when set.");
        }
    }
}
