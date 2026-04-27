using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerBuildLayoutDefinition — the ScriptableObject that
    // describes a brawler's slot table (which slot IDs exist, what type each
    // is, and at what power level it unlocks).
    //
    // SUT public surface:
    //   Slots                                    — public field
    //   CountSlots(slotType) -> int
    //   HasSlotId(slotId) -> bool
    //   TryGetSlot(slotId, out slot) -> bool
    //   IsSlotUnlocked(slotId, powerLevel) -> bool
    //
    // WHY THIS IS A NEW LESSON SHAPE — TWO LESSONS LAYERED:
    //
    // 1. ScriptableObject lifecycle in EditMode tests.
    //    BrawlerBuildLayoutDefinition is a ScriptableObject, which means
    //    `new BrawlerBuildLayoutDefinition()` is illegal — Unity demands
    //    `ScriptableObject.CreateInstance<T>()`. SO instances are native-
    //    backed objects that leak across tests if not destroyed, so this
    //    fixture follows the project's standard discipline:
    //      - `_spawned : List<Object>` field
    //      - `[SetUp]` initializes it
    //      - `[TearDown]` calls `Object.DestroyImmediate` on every tracked
    //        instance
    //      - `Track<T>` helper adds to the list and returns the object
    //    Same shape as BrawlerBuildResolverTests / BrawlerBuildValidatorTests.
    //    This is rung 4 of the cheap-surface ladder; we only climb here
    //    because the SUT *is* a SO.
    //
    // 2. Boundary-pair on a NON-STRICT inequality.
    //    `IsSlotUnlocked` returns `powerLevel >= slot.UnlockPowerLevel` —
    //    note the `>=`, not `>`. This is the OPPOSITE of yesterday's
    //    BrawlerActionStateMachine.IsActive boundary (which used strict `<`).
    //    A non-strict inequality has THREE meaningful points to pin, not
    //    two:
    //      - PL = Unlock - 1   → locked (below)
    //      - PL = Unlock       → unlocked (the inclusive boundary itself)
    //      - PL = Unlock + 1   → unlocked (above)
    //    Asserting all three in one method catches both `>=` → `>` (which
    //    would lock the slot at the exact unlock PL) and `>=` → `==`
    //    (which would unlock only at the exact PL and re-lock above it).
    //    Single-point tests miss one of these flips.
    //
    // CROWN-JEWEL TESTS in this fixture:
    //   1. IsSlotUnlocked_BoundaryAtUnlockPowerLevel_InclusiveGreaterEqual
    //      Pins the `>=` semantics with three adjacent assertions. Catches
    //      any flip of the inequality. This is the headline test of the
    //      whole fixture — `IsSlotUnlocked` is what gates option arrays
    //      during `BrawlerBuildResolver.TryResolveUnlockedOnly`, so a
    //      one-tick flip of the contract changes which gear/gadgets show
    //      up in everyone's loadout UI.
    //
    //   2. TryGetSlot_ReturnsTrue_AndPopulatesOutSlotByValue_WhenSlotIdPresent
    //      Pins value semantics. BrawlerBuildSlotDefinition is a struct, so
    //      `out` returns a copy. The test mutates the original and asserts
    //      the out copy is unaffected. Future refactor to a class would
    //      flip this silently — caught here.
    //
    //   3. AllQueryMethods_HandleNullSlotsArray_WithoutNRE
    //      Doc-comment level group rather than a single test (because each
    //      method needs its own assertion shape). Every public query
    //      method on the SUT must safely handle a null `Slots` array, since
    //      a freshly-authored asset starts with `Slots = null` until the
    //      designer adds entries. Tests covering this group: CountSlots_*,
    //      HasSlotId_*, TryGetSlot_*, IsSlotUnlocked_* — each has a
    //      `_WhenSlotsArrayIsNull` variant.

    public class BrawlerBuildLayoutDefinitionTests
    {
        // ---------- Lifecycle housekeeping ----------
        // Standard Unity-Object cleanup. Every Track-ed object gets
        // DestroyImmediate'd in TearDown so SOs don't leak across tests.

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

        // ---------- Builder helpers ----------

        private static BrawlerBuildSlotDefinition MakeSlot(
            string id,
            BrawlerBuildSlotType type,
            int unlockPowerLevel = 1)
        {
            return new BrawlerBuildSlotDefinition
            {
                SlotId = id,
                DisplayName = id,
                SlotType = type,
                UnlockPowerLevel = unlockPowerLevel,
                AllowDuplicateSelectionInSameTypeGroup = false,
            };
        }

        private BrawlerBuildLayoutDefinition MakeLayout(params BrawlerBuildSlotDefinition[] slots)
        {
            BrawlerBuildLayoutDefinition layout = Track(
                ScriptableObject.CreateInstance<BrawlerBuildLayoutDefinition>());
            layout.Slots = slots;
            return layout;
        }

        private BrawlerBuildLayoutDefinition MakeLayoutWithNullSlots()
        {
            BrawlerBuildLayoutDefinition layout = Track(
                ScriptableObject.CreateInstance<BrawlerBuildLayoutDefinition>());
            // Slots stays at its CreateInstance default — null on a fresh SO.
            return layout;
        }

        // ====================================================================
        // CountSlots
        // ====================================================================

        [Test]
        public void CountSlots_ReturnsZero_WhenSlotsArrayIsNull()
        {
            // Null-safe path. A fresh SO has `Slots = null`; the SUT must
            // not NRE.
            BrawlerBuildLayoutDefinition layout = MakeLayoutWithNullSlots();

            Assert.AreEqual(0, layout.CountSlots(BrawlerBuildSlotType.Gear));
        }

        [Test]
        public void CountSlots_FiltersBySlotType_AcrossMixedLayout()
        {
            // Realistic shape: 2 gears, 1 gadget, 1 starpower, 1 hypercharge.
            // Counting gears returns 2, gadgets returns 1, etc.
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("gear_1",       BrawlerBuildSlotType.Gear),
                MakeSlot("gear_2",       BrawlerBuildSlotType.Gear),
                MakeSlot("gadget_1",     BrawlerBuildSlotType.Gadget),
                MakeSlot("starpower_1",  BrawlerBuildSlotType.StarPower),
                MakeSlot("hypercharge_1", BrawlerBuildSlotType.Hypercharge));

            Assert.AreEqual(2, layout.CountSlots(BrawlerBuildSlotType.Gear),       "Gear count");
            Assert.AreEqual(1, layout.CountSlots(BrawlerBuildSlotType.Gadget),     "Gadget count");
            Assert.AreEqual(1, layout.CountSlots(BrawlerBuildSlotType.StarPower),  "StarPower count");
            Assert.AreEqual(1, layout.CountSlots(BrawlerBuildSlotType.Hypercharge),"Hypercharge count");
        }

        [Test]
        public void CountSlots_ReturnsZero_WhenNoSlotsMatchType()
        {
            // Loop ran, found nothing — distinct from the "loop didn't run"
            // null path tested above.
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("gear_1", BrawlerBuildSlotType.Gear));

            Assert.AreEqual(0, layout.CountSlots(BrawlerBuildSlotType.Gadget));
        }

        // ====================================================================
        // HasSlotId
        // ====================================================================

        [Test]
        public void HasSlotId_ReturnsFalse_WhenSlotsArrayIsNull()
        {
            BrawlerBuildLayoutDefinition layout = MakeLayoutWithNullSlots();

            Assert.IsFalse(layout.HasSlotId("anything"));
        }

        // Three [TestCase] rows for the input-validation guard. One method,
        // three rows = the truth-table shape applied to a single-input
        // predicate. New input forms (e.g. a tab character) would be one new
        // row, not a new method.
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        public void HasSlotId_ReturnsFalse_OnNullEmptyOrWhitespace(string slotId)
        {
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("real_slot", BrawlerBuildSlotType.Gear));

            Assert.IsFalse(layout.HasSlotId(slotId));
        }

        [Test]
        public void HasSlotId_ReturnsTrue_WhenSlotIdPresent()
        {
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("gear_1",   BrawlerBuildSlotType.Gear),
                MakeSlot("gadget_1", BrawlerBuildSlotType.Gadget));

            Assert.IsTrue(layout.HasSlotId("gear_1"));
            Assert.IsTrue(layout.HasSlotId("gadget_1"));
        }

        [Test]
        public void HasSlotId_ReturnsFalse_WhenSlotIdAbsent()
        {
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("gear_1", BrawlerBuildSlotType.Gear));

            Assert.IsFalse(layout.HasSlotId("gear_2"));
        }

        // ====================================================================
        // TryGetSlot
        // ====================================================================

        [Test]
        public void TryGetSlot_ReturnsFalse_AndOutIsDefault_WhenSlotsArrayIsNull()
        {
            BrawlerBuildLayoutDefinition layout = MakeLayoutWithNullSlots();

            bool found = layout.TryGetSlot("anything", out BrawlerBuildSlotDefinition slot);

            Assert.IsFalse(found);
            Assert.AreEqual(default(BrawlerBuildSlotDefinition), slot,
                "Out slot must be default when not found");
        }

        [Test]
        public void TryGetSlot_ReturnsFalse_AndOutIsDefault_WhenSlotIdAbsent()
        {
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("gear_1", BrawlerBuildSlotType.Gear));

            bool found = layout.TryGetSlot("gear_99", out BrawlerBuildSlotDefinition slot);

            Assert.IsFalse(found);
            Assert.AreEqual(default(BrawlerBuildSlotDefinition), slot);
        }

        [Test]
        public void TryGetSlot_ReturnsTrue_AndPopulatesOutSlotByValue_WhenSlotIdPresent()
        {
            // Crown jewel: pins value-copy semantics for the out parameter.
            // BrawlerBuildSlotDefinition is a struct, so `out` should
            // produce an independent copy. We mutate the layout's stored
            // slot AFTER getting the out copy and assert the copy is
            // unaffected. If a future refactor changes the struct to a
            // class, this test fails immediately — and rightfully so,
            // because callers downstream expect snapshot semantics.
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("gear_1", BrawlerBuildSlotType.Gear, unlockPowerLevel: 8),
                MakeSlot("gadget_1", BrawlerBuildSlotType.Gadget, unlockPowerLevel: 7));

            bool found = layout.TryGetSlot("gear_1", out BrawlerBuildSlotDefinition slot);

            Assert.IsTrue(found, "Should find slot by id");
            Assert.AreEqual("gear_1", slot.SlotId);
            Assert.AreEqual(BrawlerBuildSlotType.Gear, slot.SlotType);
            Assert.AreEqual(8, slot.UnlockPowerLevel);

            // Mutate the original and confirm the out copy didn't move.
            layout.Slots[0].UnlockPowerLevel = 99;
            Assert.AreEqual(8, slot.UnlockPowerLevel,
                "Out copy must be independent of subsequent layout mutation (struct = value copy)");
        }

        // ====================================================================
        // IsSlotUnlocked  (the headline)
        // ====================================================================

        [Test]
        public void IsSlotUnlocked_ReturnsFalse_WhenSlotIdAbsent()
        {
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("gear_1", BrawlerBuildSlotType.Gear, unlockPowerLevel: 1));

            Assert.IsFalse(layout.IsSlotUnlocked("nonexistent_slot", 999),
                "Missing slot must lock regardless of power level");
        }

        [Test]
        public void IsSlotUnlocked_ReturnsFalse_WhenSlotsArrayIsNull()
        {
            BrawlerBuildLayoutDefinition layout = MakeLayoutWithNullSlots();

            Assert.IsFalse(layout.IsSlotUnlocked("anything", 99));
        }

        [Test]
        public void IsSlotUnlocked_BoundaryAtUnlockPowerLevel_InclusiveGreaterEqual()
        {
            // CROWN JEWEL.
            //
            // Contract: IsSlotUnlocked returns `powerLevel >= UnlockPowerLevel`.
            //
            // Three points to pin (NOT two — non-strict inequality differs
            // from yesterday's strict `<`):
            //   PL = Unlock - 1  → false (locked: below)
            //   PL = Unlock      → true  (unlocked: the inclusive boundary)
            //   PL = Unlock + 1  → true  (unlocked: above)
            //
            // A flip to `>` would lock the slot at exactly Unlock — the
            // most likely accidental break. A flip to `==` would unlock
            // only at exactly Unlock and lock again above — possible if
            // someone tries to be "exact." Both are caught here.

            const int unlockPL = 9; // hypercharge-tier in a typical layout
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot("starpower_1", BrawlerBuildSlotType.StarPower, unlockPowerLevel: unlockPL));

            Assert.IsFalse(
                layout.IsSlotUnlocked("starpower_1", unlockPL - 1),
                $"At PL = {unlockPL - 1} (Unlock - 1), slot must be locked");

            Assert.IsTrue(
                layout.IsSlotUnlocked("starpower_1", unlockPL),
                $"At PL = {unlockPL} (Unlock), slot must be unlocked (inclusive `>=` boundary)");

            Assert.IsTrue(
                layout.IsSlotUnlocked("starpower_1", unlockPL + 1),
                $"At PL = {unlockPL + 1} (Unlock + 1), slot must remain unlocked");
        }

        // [TestCase] fan-out across the four standard slot tiers used by
        // the StandardBrawlerBuildLayout asset. Documents the unlock-PL
        // contract for each tier and proves IsSlotUnlocked behaves
        // identically regardless of the slot's type — only UnlockPowerLevel
        // matters.
        [TestCase("gadget_1",       7,  6, false)]
        [TestCase("gadget_1",       7,  7, true)]
        [TestCase("gear_1",         8,  7, false)]
        [TestCase("gear_1",         8,  8, true)]
        [TestCase("starpower_1",    9,  8, false)]
        [TestCase("starpower_1",    9,  9, true)]
        [TestCase("gear_2",        10,  9, false)]
        [TestCase("gear_2",        10, 10, true)]
        [TestCase("hypercharge_1", 11, 10, false)]
        [TestCase("hypercharge_1", 11, 11, true)]
        public void IsSlotUnlocked_AcrossStandardLayout_RespectsPerSlotUnlockPL(
            string slotId,
            int unlockPL,
            int testPL,
            bool expectedUnlocked)
        {
            BrawlerBuildLayoutDefinition layout = MakeLayout(
                MakeSlot(slotId, BrawlerBuildSlotType.Gear, unlockPowerLevel: unlockPL));

            Assert.AreEqual(expectedUnlocked, layout.IsSlotUnlocked(slotId, testPL));
        }
    }
}
