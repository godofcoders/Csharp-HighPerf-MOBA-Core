using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerStealth — the "am I currently hidden?" substate.
    //
    // SUT: a small POCO with these public surfaces:
    //   IsInBush         { get; set; }
    //   IsRevealed       { get; set; }
    //   LastAttackTick   { get; set; }
    //   const RecentlyAttackedTicks = 60
    //   IsHidden(currentTick) -> bool
    //   Reset()
    //
    // No collaborators — like BrawlerActionStateMachine, the SUT takes the
    // current tick as a method parameter, so the fixture is trivially
    // deterministic. No FakeClock, no SetUp/TearDown discipline needed.
    //
    // SCOPE BOUNDARY:
    // The broader "IsHiddenTo(observerTeam)" question is intentionally NOT
    // tested here. That method lives on BrawlerState as a coordinator (it
    // needs Team and ISimulationClock from ServiceProvider). The production
    // class's own doc-comment draws this line, and we honor it. This fixture
    // tests the *pure* contract: given a tick, am I hidden?
    //
    // WHY THIS IS A NEW LESSON SHAPE — TRUTH-TABLE TESTING:
    // IsHidden is a logical AND of three conditions:
    //
    //   isHidden = IsInBush AND !IsRevealed AND !recentlyAttacked
    //
    // Three booleans means 2^3 = 8 combinations. Rather than write 8 separate
    // test methods, we use NUnit [TestCase] to drive ONE method through the
    // full truth table. The body asserts the AND contract once; the rows
    // assert "and here's every input combination it must satisfy."
    //
    // This is the same idea as state-machine table testing
    // (BrawlerActionStateMachine), applied to a logical predicate instead of
    // a state graph. The win: if someone changes IsHidden from AND to OR,
    // the truth-table test fails on multiple rows simultaneously — much
    // louder than a single "and here's a case I forgot" failure.
    //
    // CROWN-JEWEL TESTS in this fixture:
    //   1. IsHidden_RequiresAllThreeConditions_TruthTable
    //      The defining contract of stealth. If this fails, the predicate
    //      semantics have changed and gameplay is wrong (people seeing each
    //      other through bushes, or invisible while shooting). All 8 rows
    //      must hold; a single missing row is a regression.
    //
    //   2. IsHidden_BoundaryAtRecentlyAttackedWindow_StrictLessThan
    //      Tests delta == RecentlyAttackedTicks - 1 (still revealed by attack)
    //      and delta == RecentlyAttackedTicks (window expired, hidden again)
    //      in one method. Pins the strict `<` in the production code. A flip
    //      to `<=` would extend the reveal window by exactly one tick — 33ms
    //      at 30Hz, invisible in playtest, caught instantly here.
    //
    //   3. IsHidden_FreshBrawlerInBush_AppearsVisible_KnownGapDueToLastAttackTickDefault
    //      Documents a real footgun in the current implementation. A brawler
    //      who has never attacked has LastAttackTick == 0. In the first ~60
    //      ticks of the match, (currentTick - 0) < 60, so the recently-
    //      attacked window treats them as if they just shot — and they show
    //      up visible inside a bush despite never firing. This test PINS the
    //      current behavior so any future "fix" is intentional rather than
    //      accidental, and so the gap stays surfaced in the test trace until
    //      it's properly addressed (likely: sentinel value like uint.MaxValue
    //      for "never attacked," or a separate HasEverAttacked bool).

    public class BrawlerStealthTests
    {
        // Convenience: keep the const visible at fixture top.
        private const uint Window = BrawlerStealth.RecentlyAttackedTicks;

        // ---- Construction ---------------------------------------------------

        [Test]
        public void Construction_DefaultsToNotInBush_NotRevealed_LastAttackTickZero()
        {
            // Defaults matter because Reset() restores them and because a
            // freshly-constructed brawler is the simplest possible "is this
            // wired right?" check.
            var stealth = new BrawlerStealth();

            Assert.IsFalse(stealth.IsInBush, "Default IsInBush should be false");
            Assert.IsFalse(stealth.IsRevealed, "Default IsRevealed should be false");
            Assert.AreEqual(0u, stealth.LastAttackTick, "Default LastAttackTick should be 0");
        }

        [Test]
        public void RecentlyAttackedTicks_IsPinnedAtSixty()
        {
            // Pin the constant. 60 ticks = ~2 seconds at 30 TPS. If this
            // changes during a balance pass, that's a *design* decision and
            // this test should be updated alongside the change — not silently
            // moved by someone "tidying up." Catches any drift.
            Assert.AreEqual(60u, BrawlerStealth.RecentlyAttackedTicks);
        }

        // ---- IsHidden truth table (the central contract) --------------------

        // Rows: (inBush, revealed, recentlyAttacked) -> expectedHidden
        // The only `true` row is (true, false, false). Every other row of the
        // 2^3 = 8 combinations must return false. If anyone changes the AND
        // to an OR, or drops one of the three checks, multiple rows fail.

        [TestCase(false, false, false, false)] // not in bush — visible
        [TestCase(false, false, true,  false)] // not in bush — visible
        [TestCase(false, true,  false, false)] // not in bush — visible
        [TestCase(false, true,  true,  false)] // not in bush — visible
        [TestCase(true,  false, false, true)]  // ONLY hidden case
        [TestCase(true,  false, true,  false)] // recently attacked — visible
        [TestCase(true,  true,  false, false)] // revealed — visible
        [TestCase(true,  true,  true,  false)] // revealed AND attacked — visible
        public void IsHidden_RequiresAllThreeConditions_TruthTable(
            bool inBush,
            bool revealed,
            bool recentlyAttacked,
            bool expectedHidden)
        {
            // Pick a "now" comfortably past the window so we can express
            // "recently attacked" via a recent LastAttackTick and "old"
            // via one well outside the window.
            const uint currentTick = 1000;
            uint lastAttack = recentlyAttacked
                ? currentTick - 30   // 30 < 60 → inside window
                : currentTick - 200; // 200 > 60 → outside window

            var stealth = new BrawlerStealth
            {
                IsInBush = inBush,
                IsRevealed = revealed,
                LastAttackTick = lastAttack,
            };

            Assert.AreEqual(expectedHidden, stealth.IsHidden(currentTick));
        }

        // ---- Recently-attacked window: the strict `<` boundary --------------

        [Test]
        public void IsHidden_BoundaryAtRecentlyAttackedWindow_StrictLessThan()
        {
            // Contract: recentlyAttacked = (currentTick - LastAttackTick) < Window
            //
            // delta == Window - 1  → still recently-attacked → IsHidden = false
            // delta == Window      → window has expired       → IsHidden = true
            //
            // Both sides asserted in one method so a refactor flipping `<`
            // to `<=` (which would extend the reveal by exactly one tick)
            // fails the second assertion immediately.

            const uint currentTick = 1000;

            var stealth = new BrawlerStealth
            {
                IsInBush = true,
                IsRevealed = false,
            };

            // Inside the window by one tick — visible.
            stealth.LastAttackTick = currentTick - (Window - 1);
            Assert.IsFalse(
                stealth.IsHidden(currentTick),
                $"At delta = {Window - 1} (Window - 1), recently-attacked still applies; should be visible");

            // Exactly at the window — strict `<` means the window has
            // expired. Hidden again.
            stealth.LastAttackTick = currentTick - Window;
            Assert.IsTrue(
                stealth.IsHidden(currentTick),
                $"At delta = {Window} (Window), recently-attacked window has expired; should be hidden");
        }

        [Test]
        public void IsHidden_LastAttackTickEqualsCurrentTick_IsRecentlyAttacked()
        {
            // The "you just shot this very tick" case — delta = 0.
            // Pins the other boundary of the window: 0 is inside [0, Window),
            // so IsHidden returns false even in a bush.
            var stealth = new BrawlerStealth
            {
                IsInBush = true,
                IsRevealed = false,
                LastAttackTick = 1000,
            };

            Assert.IsFalse(stealth.IsHidden(1000));
        }

        // ---- Known-gap documentation (footgun pin) --------------------------

        [Test]
        public void IsHidden_FreshBrawlerInBush_AppearsVisible_KnownGapDueToLastAttackTickDefault()
        {
            // KNOWN GAP — pinning current behavior, not endorsing it.
            //
            // A brawler who has never attacked has LastAttackTick == 0. In
            // the first ~60 ticks of the match (or anywhere currentTick is
            // small), (currentTick - 0) < RecentlyAttackedTicks evaluates
            // true, and IsHidden returns false despite the brawler never
            // having fired a shot.
            //
            // Real-world impact: small. Matches don't usually have brawlers
            // sitting in bushes at tick 5. But the implementation has a
            // sentinel-vs-default footgun, and any future cleanup must
            // either:
            //   (a) introduce a HasEverAttacked bool, or
            //   (b) initialize LastAttackTick to a value such that
            //       (0 - LastAttackTick) >= Window (e.g. uint.MaxValue,
            //       relying on uint wraparound), or
            //   (c) accept this gap explicitly.
            //
            // This test exists so the gap stays loud in the test trace and
            // any "fix" is an intentional decision documented in code review.

            var stealth = new BrawlerStealth
            {
                IsInBush = true,
                IsRevealed = false,
                // LastAttackTick = 0 (default — never attacked)
            };

            // currentTick = 10 → delta = 10 → recentlyAttacked = true → visible
            Assert.IsFalse(
                stealth.IsHidden(10),
                "KNOWN GAP: fresh brawler with LastAttackTick=0 is incorrectly " +
                "treated as recently-attacked in the early-game window. See test comment.");
        }

        // ---- Reset ----------------------------------------------------------

        [Test]
        public void Reset_ClearsAllStealthFlags()
        {
            // After Reset, the brawler must be in the same shape as a fresh
            // construction. Reset is called on respawn, so any leftover
            // stealth state from before death would be a real bug (e.g.
            // respawning while still flagged as IsRevealed from the killing
            // blow's reveal effect).
            var stealth = new BrawlerStealth
            {
                IsInBush = true,
                IsRevealed = true,
                LastAttackTick = 999,
            };

            stealth.Reset();

            Assert.IsFalse(stealth.IsInBush, "Reset should clear IsInBush");
            Assert.IsFalse(stealth.IsRevealed, "Reset should clear IsRevealed");
            Assert.AreEqual(0u, stealth.LastAttackTick, "Reset should zero LastAttackTick");
        }

        [Test]
        public void Reset_AfterFullSetup_RestoresIsHiddenFalse_ViaIsInBushClear()
        {
            // Behavior assertion at the contract level: after Reset, IsHidden
            // returns false because IsInBush is back to false (the first
            // short-circuit). Belt-and-suspenders alongside the property-
            // level assertions above.
            var stealth = new BrawlerStealth
            {
                IsInBush = true,
                IsRevealed = false,
                LastAttackTick = 0, // would normally make us hidden at currentTick=200
            };
            // Sanity: would have been hidden before Reset.
            Assert.IsTrue(stealth.IsHidden(200), "Pre-condition: should be hidden before Reset");

            stealth.Reset();

            Assert.IsFalse(stealth.IsHidden(200), "Post-Reset: must not be hidden (IsInBush cleared)");
        }
    }
}
