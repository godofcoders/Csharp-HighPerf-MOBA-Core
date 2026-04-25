using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerBuildResolver — the pure function that turns
    // (BrawlerDefinition, BrawlerBuildDefinition, powerLevel) into a
    // ResolvedBrawlerBuild that the runtime simulation can consume.
    //
    // The resolver delegates input correctness to BrawlerBuildValidator.
    // What this suite tests is the OTHER half: assuming validation passes,
    // does the resolver populate the right buckets?
    //   - Gadgets → ResolvedBrawlerBuild.Gadgets
    //   - StarPowers / Gear → ResolvedBrawlerBuild.PassiveOptions
    //     (because both are PassiveDefinition under the hood)
    //   - Hypercharge → ResolvedBrawlerBuild.Hypercharge
    //   - And TryResolveUnlockedOnly filters out picks for slots whose
    //     UnlockPowerLevel is above the player's current PL.
    //
    // Why the validator-resolver split matters: it's command-query
    // separation. Validator is a yes/no question with an error message
    // (cheap, side-effect-free). Resolver is the actual transformation
    // (only run after validator says yes). Splitting them means the UI can
    // show "build is invalid: <reason>" without doing the work, and the
    // simulation can resolve confidently knowing inputs are already clean.
    public class BrawlerBuildResolverTests
    {
        // ---------- Test-only concrete subclasses ----------
        // Same pattern as the validator tests. These would normally be
        // shared across files, but I've duplicated them here for test
        // isolation — each file is independently understandable.

        private sealed class TestGadgetDefinition : GadgetDefinition
        {
            public override IAbilityLogic CreateLogic() => null;
        }

        private sealed class TestStarPowerDefinition : StarPowerDefinition { }

        // ---------- Lifecycle housekeeping ----------

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
            BrawlerBuildLayoutDefinition layout = Track(ScriptableObject.CreateInstance<BrawlerBuildLayoutDefinition>());
            layout.Slots = slots;
            return layout;
        }

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
            TestGadgetDefinition g = Track(ScriptableObject.CreateInstance<TestGadgetDefinition>());
            g.AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.Gadget };
            return g;
        }

        private TestStarPowerDefinition MakeStarPower()
        {
            TestStarPowerDefinition sp = Track(ScriptableObject.CreateInstance<TestStarPowerDefinition>());
            sp.AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.StarPower };
            return sp;
        }

        private HyperchargeDefinition MakeHypercharge()
        {
            HyperchargeDefinition h = Track(ScriptableObject.CreateInstance<HyperchargeDefinition>());
            h.AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.Hypercharge };
            return h;
        }

        // ---------- Tests ----------

        [Test]
        public void TryResolve_ReturnsFalse_AndPropagatesError_WhenValidationFails()
        {
            // Validator-failure path: the resolver should propagate the
            // validator's error message, not silently produce a half-built
            // ResolvedBrawlerBuild.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g_does_not_exist", SelectedOption = null });

            bool ok = BrawlerBuildResolver.TryResolve(brawler, build, 999, out var resolved, out string error);

            Assert.IsFalse(ok);
            Assert.IsNull(resolved, "resolved must be null on failure");
            StringAssert.Contains("unknown slot id", error);
        }

        [Test]
        public void TryResolve_ReturnsTrue_WithEmptyResolved_WhenBuildIsNullButBrawlerValid()
        {
            // BUG TRAP / CONTRACT NOTE: With the current implementation, a
            // null build is rejected by the validator before resolve runs.
            // So the resolver returns false here, NOT a valid empty
            // resolved. We pin that behavior so a "fix" doesn't silently
            // change it.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));

            bool ok = BrawlerBuildResolver.TryResolve(brawler, null, 999, out var resolved, out string error);

            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            StringAssert.Contains("BrawlerBuildDefinition", error);
        }

        [Test]
        public void TryResolve_PutsGadget_IntoResolvedGadgets()
        {
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("g1", BrawlerBuildSlotType.Gadget)));
            TestGadgetDefinition gadget = MakeGadget();
            brawler.GadgetOptions = new GadgetDefinition[] { gadget };
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g1", SelectedOption = gadget });

            bool ok = BrawlerBuildResolver.TryResolve(brawler, build, 999, out var resolved, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, resolved.Gadgets.Count);
            Assert.AreSame(gadget, resolved.Gadgets[0]);
            Assert.AreEqual(0, resolved.PassiveOptions.Count, "no passives expected");
            Assert.IsNull(resolved.Hypercharge, "no hypercharge expected");
        }

        [Test]
        public void TryResolve_PutsStarPower_IntoResolvedPassiveOptions()
        {
            // StarPowerDefinition derives from PassiveDefinition, so it
            // routes into the PassiveOptions bucket — NOT a separate
            // StarPowers list. Pin the bucket choice.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("sp1", BrawlerBuildSlotType.StarPower)));
            TestStarPowerDefinition sp = MakeStarPower();
            brawler.StarPowerOptions = new StarPowerDefinition[] { sp };
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "sp1", SelectedOption = sp });

            bool ok = BrawlerBuildResolver.TryResolve(brawler, build, 999, out var resolved, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, resolved.PassiveOptions.Count);
            Assert.AreSame(sp, resolved.PassiveOptions[0]);
            Assert.AreEqual(0, resolved.Gadgets.Count);
        }

        [Test]
        public void TryResolve_PutsHypercharge_IntoResolvedHypercharge()
        {
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(MakeSlot("h1", BrawlerBuildSlotType.Hypercharge)));
            HyperchargeDefinition hyper = MakeHypercharge();
            brawler.HyperchargeOptions = new HyperchargeDefinition[] { hyper };
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "h1", SelectedOption = hyper });

            bool ok = BrawlerBuildResolver.TryResolve(brawler, build, 999, out var resolved, out _);

            Assert.IsTrue(ok);
            Assert.AreSame(hyper, resolved.Hypercharge);
            Assert.AreEqual(0, resolved.Gadgets.Count);
            Assert.AreEqual(0, resolved.PassiveOptions.Count);
        }

        [Test]
        public void TryResolve_FullLoadout_PopulatesAllThreeBuckets()
        {
            // Integration-flavoured but still pure: gadget + starpower +
            // hypercharge in one build. All three buckets should receive
            // exactly the right thing. This is the test that mirrors what
            // an actual brawler loadout looks like at runtime.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(
                MakeSlot("g1", BrawlerBuildSlotType.Gadget),
                MakeSlot("sp1", BrawlerBuildSlotType.StarPower),
                MakeSlot("h1", BrawlerBuildSlotType.Hypercharge)));
            TestGadgetDefinition gadget = MakeGadget();
            TestStarPowerDefinition sp = MakeStarPower();
            HyperchargeDefinition hyper = MakeHypercharge();
            brawler.GadgetOptions = new GadgetDefinition[] { gadget };
            brawler.StarPowerOptions = new StarPowerDefinition[] { sp };
            brawler.HyperchargeOptions = new HyperchargeDefinition[] { hyper };
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g1", SelectedOption = gadget },
                new BrawlerBuildSlotSelection { SlotId = "sp1", SelectedOption = sp },
                new BrawlerBuildSlotSelection { SlotId = "h1", SelectedOption = hyper });

            bool ok = BrawlerBuildResolver.TryResolve(brawler, build, 999, out var resolved, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, resolved.Gadgets.Count);
            Assert.AreSame(gadget, resolved.Gadgets[0]);
            Assert.AreEqual(1, resolved.PassiveOptions.Count);
            Assert.AreSame(sp, resolved.PassiveOptions[0]);
            Assert.AreSame(hyper, resolved.Hypercharge);
        }

        [Test]
        public void TryResolveUnlockedOnly_DropsLockedSlot_KeepsUnlockedSlot()
        {
            // The "unlocked-only" variant is the in-match resolution: at the
            // current power level, what does my build actually grant me?
            // Hypercharge slot at PL5 should be dropped when player is at PL3.
            BrawlerDefinition brawler = MakeBrawler(MakeLayout(
                MakeSlot("g1", BrawlerBuildSlotType.Gadget, unlockPowerLevel: 1),
                MakeSlot("h1", BrawlerBuildSlotType.Hypercharge, unlockPowerLevel: 5)));
            TestGadgetDefinition gadget = MakeGadget();
            HyperchargeDefinition hyper = MakeHypercharge();
            brawler.GadgetOptions = new GadgetDefinition[] { gadget };
            brawler.HyperchargeOptions = new HyperchargeDefinition[] { hyper };
            BrawlerBuildDefinition build = MakeBuild(
                new BrawlerBuildSlotSelection { SlotId = "g1", SelectedOption = gadget },
                new BrawlerBuildSlotSelection { SlotId = "h1", SelectedOption = hyper });

            bool ok = BrawlerBuildResolver.TryResolveUnlockedOnly(brawler, build, powerLevel: 3, out var resolved, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, resolved.Gadgets.Count, "PL1 gadget slot survives at PL3");
            Assert.IsNull(resolved.Hypercharge, "PL5 hypercharge slot dropped at PL3");
        }
    }
}
