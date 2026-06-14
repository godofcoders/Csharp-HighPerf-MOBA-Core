using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerStealth — the "am I currently hidden?" substate.
    //
    // SUT: a small POCO with these public surfaces:
    //   IsInBush         { get; set; }
    //   IsRevealed       { get; set; } // compatibility setter maps to status reveal
    //   IsProximityRevealed { get; set; }
    //   IsStatusRevealed { get; set; }
    //   LastAttackTick   { get; set; }
    //   LastDamageTakenTick { get; set; }
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
    // IsHidden is a logical AND of core visibility conditions:
    //
    //   isHidden = IsInBush AND !IsRevealed AND !recentlyAttacked AND !recentlyDamaged
    //
    // The main table below covers bush/reveal/recent-attack combinations;
    // recent-damage reveal gets its own boundary test because it mirrors the
    // attack reveal window and keeps the table readable.
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
    //      other through bushes, or invisible while shooting). All table rows
    //      must hold; a single missing row is a regression.
    //
    //   2. IsHidden_BoundaryAtRecentlyAttackedWindow_StrictLessThan
    //      Tests delta == RecentlyAttackedTicks - 1 (still revealed by attack)
    //      and delta == RecentlyAttackedTicks (window expired, hidden again)
    //      in one method. Pins the strict `<` in the production code. A flip
    //      to `<=` would extend the reveal window by exactly one tick — 33ms
    //      at 30Hz, invisible in playtest, caught instantly here.
    //
    //   3. IsHidden_FreshBrawlerInBush_IsHiddenBeforeAnyAttack
    //      Pins the fixed sentinel behavior: LastAttackTick still reads as 0
    //      for compatibility, but it is not an active reveal window until an
    //      attack tick is actually assigned.

    public class BrawlerStealthTests
    {
        // Convenience: keep the const visible at fixture top.
        private const uint Window = BrawlerStealth.RecentlyAttackedTicks;

        // ---- Construction ---------------------------------------------------

        [Test]
        public void Construction_DefaultsToNotInBush_NotRevealed_NoActiveRevealWindows()
        {
            // Defaults matter because Reset() restores them and because a
            // freshly-constructed brawler is the simplest possible "is this
            // wired right?" check.
            var stealth = new BrawlerStealth();

            Assert.IsFalse(stealth.IsInBush, "Default IsInBush should be false");
            Assert.IsFalse(stealth.IsRevealed, "Default IsRevealed should be false");
            Assert.IsFalse(stealth.IsProximityRevealed, "Default proximity reveal should be false");
            Assert.IsFalse(stealth.IsStatusRevealed, "Default status reveal should be false");
            Assert.AreEqual(0u, stealth.LastAttackTick, "Default LastAttackTick should be 0");
            Assert.AreEqual(0u, stealth.LastDamageTakenTick, "Default LastDamageTakenTick should be 0");
            Assert.IsFalse(stealth.IsAttackRevealed(10), "A default tick value should not imply a fresh attack");
            Assert.IsFalse(stealth.IsDamageRevealed(10), "A default tick value should not imply fresh damage");
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

        [Test]
        public void RecentlyDamagedTicks_IsPinnedAtSixty()
        {
            Assert.AreEqual(60u, BrawlerStealth.RecentlyDamagedTicks);
        }

        // ---- IsHidden truth table (the central contract) --------------------

        // Rows: (inBush, revealed, recentlyAttacked) -> expectedHidden
        // The only `true` row is (true, false, false). Every other row must
        // return false. If anyone changes the AND
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

        // ---- Fresh-spawn sentinel behavior ----------------------------------

        [Test]
        public void IsHidden_FreshBrawlerInBush_IsHiddenBeforeAnyAttack()
        {
            var stealth = new BrawlerStealth
            {
                IsInBush = true,
                IsRevealed = false,
                // LastAttackTick reads as 0 by default, but has not been assigned.
            };

            Assert.IsTrue(
                stealth.IsHidden(10),
                "Fresh brawlers should be able to hide in grass before their first attack");
        }

        [Test]
        public void IsHidden_RecentDamageRevealsBrawlerUntilWindowExpires()
        {
            const uint currentTick = 1000;

            var stealth = new BrawlerStealth
            {
                IsInBush = true,
                IsRevealed = false,
                LastAttackTick = currentTick - Window,
            };

            stealth.LastDamageTakenTick = currentTick - (Window - 1);
            Assert.IsFalse(
                stealth.IsHidden(currentTick),
                "Recent damage should reveal a brawler in grass");

            stealth.LastDamageTakenTick = currentTick - Window;
            Assert.IsTrue(
                stealth.IsHidden(currentTick),
                "Damage reveal should expire at the same strict boundary as attack reveal");
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
                IsProximityRevealed = true,
                IsStatusRevealed = true,
                LastAttackTick = 999,
                LastDamageTakenTick = 998,
            };

            stealth.Reset();

            Assert.IsFalse(stealth.IsInBush, "Reset should clear IsInBush");
            Assert.IsFalse(stealth.IsRevealed, "Reset should clear IsRevealed");
            Assert.IsFalse(stealth.IsProximityRevealed, "Reset should clear proximity reveal");
            Assert.IsFalse(stealth.IsStatusRevealed, "Reset should clear status reveal");
            Assert.AreEqual(0u, stealth.LastAttackTick, "Reset should zero LastAttackTick");
            Assert.AreEqual(0u, stealth.LastDamageTakenTick, "Reset should zero LastDamageTakenTick");
            Assert.IsFalse(stealth.IsAttackRevealed(1000), "Reset should clear active attack reveal");
            Assert.IsFalse(stealth.IsDamageRevealed(1000), "Reset should clear active damage reveal");
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
