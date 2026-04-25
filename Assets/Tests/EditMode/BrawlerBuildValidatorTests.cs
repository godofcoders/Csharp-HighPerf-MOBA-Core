using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerBuildValidator — the schema gate between
    // designer-authored loadout data and the runtime simulation.
    //
    // What this validator MUST do (and what each test pins down):
    //   - Reject obviously broken inputs early (null brawler, null build,
    //     missing layout) with a useful error message.
    //   - Reject selections that point at slots that don't exist on the
    //     brawler's layout (typo-resistance for designer changes).
    //   - Honour slot UNLOCK power levels — equipping a hypercharge slot at
    //     power-level 1 must fail, because a player at PL1 hasn't earned it.
    //   - Reject options whose type doesn't match the slot type (a gadget
    //     in a star-power slot, etc.).
    //   - Reject options that aren't available to THIS brawler (a Colt-only
    //     gadget on Byron is wrong).
    //   - Reject duplicate selections within a slot group, unless the slot
    //     is explicitly authored to allow duplicates.
    //
    // This is the boundary that catches "designer added a new gadget but
    // forgot to add it to the brawler's GadgetOptions" — the kind of mistake
    // that produces silent runtime no-ops without a validator. Locking the
    // contract here means future content additions can't quietly slip
    // through.
    public class BrawlerBuildValidatorTests
    {
        // ---------- Test-only concrete subclasses ----------
        // Several option types are abstract in production. To test them in
        // isolation we need concrete, instantiable subclasses. Same pattern
        // as our other fakes — except now the "fake" has to inherit from a
        // ScriptableObject base, so we use ScriptableObject.CreateInstance
        // instead of `new`.

        private sealed class TestGadgetDefinition : GadgetDefinition
        {
            // GadgetDefinition → AbilityDefinition (abstract CreateLogic).
            // Validator never calls this, so returning null is safe.
            public override IAbilityLogic CreateLogic() => null;
        }

        private sealed class TestStarPowerDefinition : StarPowerDefinition { }

        private sealed class TestGearDefinition : GearDefinition { }

        // ---------- ScriptableObject lifecycle housekeeping ----------
        // Every CreateInstance produces a UnityEngine.Object that lives until
        // the editor unloads it. We track each one and DestroyImmediate them
        // in TearDown so leaking instances don't accumulate across runs.

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

        // Track an instance for later cleanup, return it for fluent chaining.
        private T Track<T>(T obj) where T : Object
        {
            _spawned.Add(obj);
            return obj;
        }

        // ---------- Builder helpers ----------
        // Without these, every test would be 30 lines of CreateInstance
        // boilerplate and unreadable. The builders make each test's INTENT
        // legible: "make a layout with one Gadget slot at power level 1".

        private static BrawlerBuildSlotDefinition MakeSlot(
            string id,
            BrawlerBuildSlotType type,
            int unlockPowerLevel = 1,
            bool allowDuplicates = false)
        {
            return new BrawlerBuildSlotDefinition
            {
                SlotId = id,
                DisplayName = id,
                SlotType = type,
                UnlockPowerLevel = unlockPowerLevel,
                AllowDuplicateSelectionInSameTypeGroup = allowDuplicates,
            };
        }

        private BrawlerBuildLayoutDefinition MakeLayout(params BrawlerBuildSlotDefinition[] slots)
        {
            BrawlerBuildLayoutDefinition layout = Track(ScriptableObject.CreateInstance<BrawlerBuildLayoutDefinition>());
            layout.Slots = slots;
            return layout;
        }

        // Build a brawler with the given layout. Empty option arrays so
        // by default no gadget/starpower/hypercharge is "available" — tests
        // that need available options will populate explicitly.
        private BrawlerDefinition MakeBrawler(BrawlerBuildLayoutDefinition layout)
        {
            BrawlerDefinition brawler = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            brawler.BuildLayout = layout;
            brawler.GadgetOptions = new GadgetDefinition[0];
            brawler.StarPowerOptions = new StarPowerDefinition[0];
            brawler.HyperchargeOptions = new HyperchargeDefinition[0];
            return brawler;
        }

        private BrawlerBuildDefinition MakeBuild(params BrawlerBuildSlotSelection[] selections)
        {
            BrawlerBuildDefinition build = Track(ScriptableObject.CreateInstance<BrawlerBuildDefinition>());
            build.Selections = selections;
            return build;
        }

        private TestGadgetDefinition MakeGadget()
        {
            TestGadgetDefinition gadget = Track(ScriptableObject.CreateInstance<TestGadgetDefinition>());
            // OnValidate normally sets this in the editor, but CreateInstance
            // doesn't fire OnValidate, so we set it explicitly.
            gadget.AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.Gadget };
            return gadget;
        }

        private TestStarPowerDefinition MakeStarPower()
        {
            TestStarPowerDefinition sp = Track(ScriptableObject.CreateInstance<TestStarPowerDefinition>());
            sp.AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.StarPower };
            return sp;
        }

        // ---------- Tier 1: structural failure modes (no concrete options) ----------

        [Test]
        public void Validate_ReturnsInvalid_WhenBrawlerNull()
        {
            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(null, MakeBuild(), powerLevel: 999);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("BrawlerDefinition", result.Message);
        }

        [Test]
        public void Validate_ReturnsInvalid_WhenBuildNull()
        {
            BrawlerDefinition brawler = MakeBrawler(MakeLayout());

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, null, powerLevel: 999);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("BrawlerBuildDefinition", result.Message);
        }

        [Test]
        public void Validate_ReturnsInvalid_WhenBrawlerHasNoLayout()
        {
            BrawlerDefinition brawler = Track(ScriptableObject.CreateInstance<BrawlerDefinition>());
            brawler.BuildLayout = null;

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, MakeBuild(), powerLevel: 999);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("BuildLayout", result.Message);
        }

        [Test]
        public void Validate_ReturnsValid_WhenLayoutHasNoSlots()
        {
            // Edge case: a brawler with an empty layout is "valid but has no
            // build surface". The validator early-exits with Valid().
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(/* no slots */));

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, MakeBuild(), powerLevel: 999);

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void Validate_ReturnsValid_WhenSelectionsAreNull()
        {
            // A build with no selections at all is "the empty loadout" and is
            // valid. (Useful for fresh accounts that haven't equipped anything.)
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            BrawlerBuildDefinition build = Track(ScriptableObject.CreateInstance<BrawlerBuildDefinition>());
            build.Selections = null;

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 999);

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void Validate_ReturnsInvalid_WhenSelectionHasEmptySlotId()
        {
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "", SelectedOption = null });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 999);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("empty SlotId", result.Message);
        }

        [Test]
        public void Validate_ReturnsInvalid_WhenSelectionReferencesUnknownSlot()
        {
            // The build references a slot id that the brawler's layout
            // doesn't contain — typo-resistance.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g_does_not_exist", SelectedOption = null });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 999);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("unknown slot id", result.Message);
        }

        [Test]
        public void Validate_ReturnsInvalid_WhenPowerLevelBelowSlotUnlock()
        {
            // Slot unlocked at PL 5, validator called with PL 4. Reject.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(
                MakeSlot("hyper", BrawlerBuildSlotType.Hypercharge, unlockPowerLevel: 5)));
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "hyper", SelectedOption = null });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 4);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("locked", result.Message);
        }

        [Test]
        public void Validate_ReturnsValid_WhenPowerLevelMatchesUnlockExactly()
        {
            // Boundary: PL exactly equals UnlockPowerLevel. The check is `<`,
            // so equality should pass.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(
                MakeSlot("hyper", BrawlerBuildSlotType.Hypercharge, unlockPowerLevel: 5)));
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "hyper", SelectedOption = null });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 5);

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void Validate_ReturnsValid_WhenSelectedOptionIsNull()
        {
            // A "I haven't chosen anything for this slot yet" state is valid.
            // Different from "selection references unknown slot" — slot is
            // valid, just no option picked.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g1", SelectedOption = null });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 999);

            Assert.IsTrue(result.IsValid);
        }

        // ---------- Tier 2: option-type and ownership failure modes ----------

        [Test]
        public void Validate_ReturnsInvalid_WhenOptionTypeMismatchesSlotType()
        {
            // A star-power option in a gadget slot. The option's
            // CanEquipInBuildSlot check should reject it.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            TestStarPowerDefinition starPower = MakeStarPower();
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g1", SelectedOption = starPower });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 999);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("cannot be equipped", result.Message);
        }

        [Test]
        public void Validate_ReturnsInvalid_WhenGadget_NotInBrawlerOptions()
        {
            // Gadget is type-correct for the slot, but it's not on the
            // brawler's GadgetOptions list — i.e. wrong brawler's gadget.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            TestGadgetDefinition foreignGadget = MakeGadget();
            // Note: brawler.GadgetOptions is empty by default — gadget is unowned.
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g1", SelectedOption = foreignGadget });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 999);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("not available for brawler", result.Message);
        }

        [Test]
        public void Validate_ReturnsValid_WhenGadget_IsInBrawlerOptions()
        {
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            TestGadgetDefinition gadget = MakeGadget();
            brawler.GadgetOptions = new GadgetDefinition[] { gadget };
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g1", SelectedOption = gadget });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 999);

            Assert.IsTrue(result.IsValid, $"validation message: {result.Message}");
        }

        [Test]
        public void Validate_ReturnsInvalid_WhenSameGadgetEquippedTwice_AndDuplicatesNotAllowed()
        {
            // Two slots, same gadget in both. Default slot config disallows
            // duplicates — should reject.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(
                MakeSlot("g1", BrawlerBuildSlotType.Gadget),
                MakeSlot("g2", BrawlerBuildSlotType.Gadget)));
            TestGadgetDefinition gadget = MakeGadget();
            brawler.GadgetOptions = new GadgetDefinition[] { gadget };
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g1", SelectedOption = gadget },
                new BrawlerBuildSlotSelection { SlotId = "g2", SelectedOption = gadget });

            BrawlerBuildValidationResult result =
                BrawlerBuildValidator.Validate(brawler, build, powerLevel: 999);

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("more than once", result.Message);
        }
    }
}
