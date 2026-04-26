using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for BrawlerActionStateMachine — the lock-out window owner.
    //
    // SUT: a small POCO state machine with these public surfaces:
    //   Enter(stateType, currentTick, durationTicks, allowMovement,
    //         allowActionInput, isInterruptible)
    //   Clear()
    //   UpdateExpiry(currentTick)
    //   IsActive(currentTick)
    //   IsInState(type, currentTick)
    //   TryInterrupt()
    //
    // No collaborators — no clock, no services. Tick is always passed in by
    // the caller, which makes the fixture trivially deterministic and means
    // we don't need any of the SetUp/TearDown discipline from the
    // StatusEffectService fixture.
    //
    // WHY THIS IS A NEW LESSON SHAPE — STATE-MACHINE TABLE TESTING:
    // A state machine has finitely many (state, transition, condition)
    // cells. The fixture's job is to walk every interesting cell and pin
    // what should happen at each. Two NUnit idioms keep the table compact:
    //
    //   1. [TestCase(...)] parameterization. One test method, many state
    //      inputs. When a new BrawlerActionStateType is introduced, a single
    //      [TestCase] row is added — no copy-paste of the test body. We use
    //      this for "every interruptible state can be interrupted" and
    //      similar fan-outs.
    //
    //   2. Boundary-pair tests. When the contract is `currentTick <
    //      LockUntilTick` (strict less-than), we test BOTH `LockUntilTick - 1`
    //      (still active) and `LockUntilTick` (expired) in one method. A
    //      refactor that flips < to <= shows up as a red right at the line
    //      where the inequality lives.
    //
    // CROWN-JEWEL TESTS in this fixture:
    //   1. Enter_OverwritesExistingState_EvenWhenExistingIsNonInterruptible
    //      Pins the design contract that Enter does NOT honor the existing
    //      state's IsInterruptible flag — callers are responsible for asking
    //      TryInterrupt first. Easy to "fix" wrongly and silently shift
    //      the meaning of Enter from "force replace" to "try replace".
    //
    //   2. IsActive_BoundaryAtLockUntilTick_StrictLessThan
    //      Tests both LockUntilTick - 1 (active) and LockUntilTick (expired).
    //      The contract is `currentTick < LockUntilTick`. If a refactor
    //      flips this to `<=`, casts last one tick longer than designers
    //      authored — a 0.5s cast becomes 0.533s. Catches it instantly.
    //
    //   3. TryInterrupt_DoesNotConsultTick_NonInterruptibleExpired_StillReturnsFalse
    //      Pins the subtle behavior that TryInterrupt is tick-blind: an
    //      expired but non-interruptible state has not yet been swept by
    //      UpdateExpiry, and TryInterrupt still says "no". A refactor that
    //      tries to be helpful ("if it's already expired, allow interrupt")
    //      would change semantics — UpdateExpiry is the ONLY path that
    //      clears non-interruptible states.
    //
    //   4. Enter_FullyReplacesCurrent_NotMutatesAStaleStructCopy
    //      Same regression canary as BrawlerCooldownsTests had. The doc-
    //      comment in the production file explicitly warns about the
    //      struct-in-property foot-gun; this test proves Enter writes
    //      through the setter, not into a local copy that's discarded.

    public class BrawlerActionStateMachineTests
    {
        // Common builders so each test reads as intent, not setup boilerplate.
        // Defaults match the "casting an ability" shape: input locked,
        // movement allowed, interruptible — like a typical Brawl Stars cast.
        private static void EnterCast(
            BrawlerActionStateMachine sm,
            uint currentTick = 100,
            uint durationTicks = 30,
            BrawlerActionStateType type = BrawlerActionStateType.CastingMainAttack,
            bool allowMovement = true,
            bool allowActionInput = false,
            bool isInterruptible = true)
        {
            sm.Enter(
                type,
                currentTick,
                durationTicks,
                allowMovement,
                allowActionInput,
                isInterruptible);
        }

        // ---------- A. Construction / initial state ----------

        [Test]
        public void Construction_StartsInNoneState_WithFullyPermissiveFlags()
        {
            // Arrange + Act
            var sm = new BrawlerActionStateMachine();

            // Assert — None state means "no lockout"; everything is allowed
            // and a stray TryInterrupt call should succeed trivially.
            Assert.AreEqual(BrawlerActionStateType.None, sm.Current.StateType);
            Assert.AreEqual(0u, sm.Current.StartTick);
            Assert.AreEqual(0u, sm.Current.LockUntilTick);
            Assert.IsTrue(sm.Current.AllowMovement);
            Assert.IsTrue(sm.Current.AllowActionInput);
            Assert.IsTrue(sm.Current.IsInterruptible);
        }

        [Test]
        public void Construction_IsActiveReturnsFalse_AtAnyTick()
        {
            // Arrange + Act
            var sm = new BrawlerActionStateMachine();

            // Assert — None state must never report active, regardless of how
            // far in the future we ask. The (StateType != None) guard in
            // IsActive is what makes this hold.
            Assert.IsFalse(sm.IsActive(0));
            Assert.IsFalse(sm.IsActive(100));
            Assert.IsFalse(sm.IsActive(uint.MaxValue));
        }

        // ---------- B. Enter ----------

        [Test]
        public void Enter_StoresAllFields_OnTheCurrentStruct()
        {
            // Arrange
            var sm = new BrawlerActionStateMachine();

            // Act
            sm.Enter(
                BrawlerActionStateType.CastingSuper,
                currentTick: 1000,
                durationTicks: 45,
                allowMovement: false,
                allowActionInput: false,
                isInterruptible: false);

            // Assert — every field on the struct should reflect the Enter
            // arguments. LockUntilTick is the derived field worth checking
            // explicitly: currentTick + durationTicks.
            Assert.AreEqual(BrawlerActionStateType.CastingSuper, sm.Current.StateType);
            Assert.AreEqual(1000u, sm.Current.StartTick);
            Assert.AreEqual(1045u, sm.Current.LockUntilTick);
            Assert.IsFalse(sm.Current.AllowMovement);
            Assert.IsFalse(sm.Current.AllowActionInput);
            Assert.IsFalse(sm.Current.IsInterruptible);
        }

        // CROWN JEWEL #4 — struct-replace regression canary
        [Test]
        public void Enter_FullyReplacesCurrent_NotMutatesAStaleStructCopy()
        {
            // Arrange — first cast.
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, currentTick: 100, durationTicks: 30);

            // Act — second Enter with completely different values. If Enter
            // mutated a local copy of the struct (the foot-gun the
            // production doc-comment warns about), Current would still
            // reflect the FIRST Enter's data here.
            sm.Enter(
                BrawlerActionStateType.CastingGadget,
                currentTick: 200,
                durationTicks: 60,
                allowMovement: false,
                allowActionInput: true,
                isInterruptible: false);

            // Assert
            Assert.AreEqual(BrawlerActionStateType.CastingGadget, sm.Current.StateType);
            Assert.AreEqual(200u, sm.Current.StartTick);
            Assert.AreEqual(260u, sm.Current.LockUntilTick);
            Assert.IsFalse(sm.Current.AllowMovement);
            Assert.IsTrue(sm.Current.AllowActionInput);
            Assert.IsFalse(sm.Current.IsInterruptible);
        }

        // CROWN JEWEL #1 — Enter is unconditional force-replace.
        [Test]
        public void Enter_OverwritesExistingState_EvenWhenExistingIsNonInterruptible()
        {
            // Arrange — pre-load a non-interruptible Stun.
            var sm = new BrawlerActionStateMachine();
            sm.Enter(
                BrawlerActionStateType.Stunned,
                currentTick: 50,
                durationTicks: 100,
                allowMovement: false,
                allowActionInput: false,
                isInterruptible: false);

            // Act — Enter a different state on top. Critically: WITHOUT
            // checking TryInterrupt first. Production callers that care
            // about not stomping a hard-CC must check themselves.
            sm.Enter(
                BrawlerActionStateType.CastingMainAttack,
                currentTick: 75,
                durationTicks: 30,
                allowMovement: true,
                allowActionInput: false,
                isInterruptible: true);

            // Assert
            Assert.AreEqual(BrawlerActionStateType.CastingMainAttack, sm.Current.StateType,
                "Enter is unconditional force-replace by design — callers " +
                "are responsible for honouring TryInterrupt before calling Enter.");
            Assert.AreEqual(75u, sm.Current.StartTick);
            Assert.AreEqual(105u, sm.Current.LockUntilTick);
            Assert.IsTrue(sm.Current.IsInterruptible);
        }

        // ---------- C. Clear ----------

        [Test]
        public void Clear_ResetsAllFields_ToTheirDefaults()
        {
            // Arrange — non-default state in place.
            var sm = new BrawlerActionStateMachine();
            sm.Enter(
                BrawlerActionStateType.Dead,
                currentTick: 999,
                durationTicks: 90,
                allowMovement: false,
                allowActionInput: false,
                isInterruptible: false);

            // Act
            sm.Clear();

            // Assert — Clear is idempotent and gets us back to a brand-new
            // state machine's exact initial layout.
            Assert.AreEqual(BrawlerActionStateType.None, sm.Current.StateType);
            Assert.AreEqual(0u, sm.Current.StartTick);
            Assert.AreEqual(0u, sm.Current.LockUntilTick);
            Assert.IsTrue(sm.Current.AllowMovement);
            Assert.IsTrue(sm.Current.AllowActionInput);
            Assert.IsTrue(sm.Current.IsInterruptible);
        }

        // ---------- D. UpdateExpiry ----------

        [Test]
        public void UpdateExpiry_IsNoOp_WhenStateIsNone()
        {
            // Arrange
            var sm = new BrawlerActionStateMachine();

            // Act
            sm.UpdateExpiry(uint.MaxValue);

            // Assert — Clear was already in effect, so the field shape is
            // unchanged regardless of how huge a tick we throw at it.
            Assert.AreEqual(BrawlerActionStateType.None, sm.Current.StateType);
            Assert.AreEqual(0u, sm.Current.LockUntilTick);
        }

        [Test]
        public void UpdateExpiry_IsNoOp_WhenStateIsStillActive()
        {
            // Arrange — cast at tick 100, lasts 30 ticks => active until 129.
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, currentTick: 100, durationTicks: 30);

            // Act
            sm.UpdateExpiry(120);

            // Assert
            Assert.AreEqual(BrawlerActionStateType.CastingMainAttack, sm.Current.StateType,
                "Calling UpdateExpiry while still in lock window must NOT clear.");
        }

        [Test]
        public void UpdateExpiry_ClearsState_WhenLockHasExpired()
        {
            // Arrange — cast at tick 100, 30 ticks long => LockUntilTick = 130.
            // At tick 130 the state has expired (strict less-than in IsActive).
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, currentTick: 100, durationTicks: 30);

            // Act
            sm.UpdateExpiry(130);

            // Assert
            Assert.AreEqual(BrawlerActionStateType.None, sm.Current.StateType);
        }

        [Test]
        public void UpdateExpiry_ClearsState_EvenWhenStateWasNonInterruptible()
        {
            // Arrange — UpdateExpiry is the ONLY path that clears a non-
            // interruptible state. TryInterrupt would have returned false.
            var sm = new BrawlerActionStateMachine();
            sm.Enter(
                BrawlerActionStateType.Stunned,
                currentTick: 50,
                durationTicks: 30,
                allowMovement: false,
                allowActionInput: false,
                isInterruptible: false);

            // Act — well past LockUntilTick (50 + 30 = 80).
            sm.UpdateExpiry(200);

            // Assert
            Assert.AreEqual(BrawlerActionStateType.None, sm.Current.StateType,
                "UpdateExpiry must time-out non-interruptible states; otherwise " +
                "stuns would be permanent until something explicitly Cleared.");
        }

        // ---------- E. IsActive — boundary pair ----------

        // CROWN JEWEL #2 — strict less-than at LockUntilTick.
        [Test]
        public void IsActive_BoundaryAtLockUntilTick_StrictLessThan()
        {
            // Arrange — cast at tick 100, 30 ticks => LockUntilTick = 130.
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, currentTick: 100, durationTicks: 30);

            // Assert — both sides of the boundary, in one test, so any future
            // refactor that flips < to <= here goes red on the right line.
            Assert.IsTrue(sm.IsActive(129),
                "Tick 129 is one tick before LockUntilTick — must still be active.");
            Assert.IsFalse(sm.IsActive(130),
                "Tick 130 == LockUntilTick — strict less-than means EXPIRED. " +
                "If this flips to <=, every cast lasts one tick longer than " +
                "the designer authored.");
            Assert.IsFalse(sm.IsActive(131),
                "Past LockUntilTick — definitely expired.");
        }

        [Test]
        public void IsActive_ReturnsTrue_DuringLockWindow()
        {
            // Arrange
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, currentTick: 100, durationTicks: 30);

            // Assert — sample within the window. Boundary covered separately.
            Assert.IsTrue(sm.IsActive(100), "At StartTick — active.");
            Assert.IsTrue(sm.IsActive(115), "Mid-window — active.");
        }

        // ---------- F. IsInState ----------

        [Test]
        public void IsInState_ReturnsTrue_WhenTypeMatchesAndStateIsActive()
        {
            // Arrange
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, currentTick: 100, durationTicks: 30,
                type: BrawlerActionStateType.CastingSuper);

            // Assert
            Assert.IsTrue(sm.IsInState(BrawlerActionStateType.CastingSuper, 110));
        }

        [Test]
        public void IsInState_ReturnsFalse_WhenTypeMatchesButLockHasExpired()
        {
            // Arrange — same setup, ask after the lock window.
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, currentTick: 100, durationTicks: 30,
                type: BrawlerActionStateType.CastingSuper);

            // Assert — type still matches the underlying struct (UpdateExpiry
            // hasn't been called yet) but IsInState honours the tick check.
            Assert.IsFalse(sm.IsInState(BrawlerActionStateType.CastingSuper, 130),
                "IsInState combines type-match AND active-window. An expired " +
                "state — even if not yet swept by UpdateExpiry — must report false.");
        }

        [Test]
        public void IsInState_ReturnsFalse_WhenTypeDoesNotMatch()
        {
            // Arrange
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, currentTick: 100, durationTicks: 30,
                type: BrawlerActionStateType.CastingSuper);

            // Assert
            Assert.IsFalse(sm.IsInState(BrawlerActionStateType.Stunned, 110));
            Assert.IsFalse(sm.IsInState(BrawlerActionStateType.None, 110));
        }

        // ---------- G. TryInterrupt — the small truth table ----------

        [Test]
        public void TryInterrupt_ReturnsTrue_WhenStateIsNone()
        {
            // Arrange
            var sm = new BrawlerActionStateMachine();

            // Act
            bool result = sm.TryInterrupt();

            // Assert — "nothing to interrupt" trivially succeeds.
            Assert.IsTrue(result);
            Assert.AreEqual(BrawlerActionStateType.None, sm.Current.StateType);
        }

        // [TestCase] parameterization — the state-machine table gets one row
        // per interruptible cast type. Adding a new interruptible state in
        // production = adding one row here, no copy-paste.
        [TestCase(BrawlerActionStateType.CastingMainAttack)]
        [TestCase(BrawlerActionStateType.CastingSuper)]
        [TestCase(BrawlerActionStateType.CastingGadget)]
        public void TryInterrupt_ClearsState_AndReturnsTrue_WhenInterruptibleCastIsActive(
            BrawlerActionStateType castType)
        {
            // Arrange
            var sm = new BrawlerActionStateMachine();
            EnterCast(sm, type: castType, isInterruptible: true);

            // Act
            bool result = sm.TryInterrupt();

            // Assert
            Assert.IsTrue(result, "Interruptible cast should report success.");
            Assert.AreEqual(BrawlerActionStateType.None, sm.Current.StateType,
                "Successful interrupt must leave the SM in a clean None state.");
        }

        // [TestCase] for the negative side — the standard "hard CC" / dead
        // states are non-interruptible. A row per state keeps the truth
        // table visible at a glance.
        [TestCase(BrawlerActionStateType.Stunned)]
        [TestCase(BrawlerActionStateType.Dead)]
        [TestCase(BrawlerActionStateType.Respawning)]
        public void TryInterrupt_ReturnsFalse_AndKeepsState_WhenNonInterruptible(
            BrawlerActionStateType nonInterruptibleType)
        {
            // Arrange
            var sm = new BrawlerActionStateMachine();
            sm.Enter(
                nonInterruptibleType,
                currentTick: 100,
                durationTicks: 60,
                allowMovement: false,
                allowActionInput: false,
                isInterruptible: false);

            // Act
            bool result = sm.TryInterrupt();

            // Assert
            Assert.IsFalse(result, "Non-interruptible states must reject interrupt.");
            Assert.AreEqual(nonInterruptibleType, sm.Current.StateType,
                "Failed TryInterrupt must NOT clear the state.");
            Assert.AreEqual(160u, sm.Current.LockUntilTick,
                "And must NOT touch the lock window either.");
        }

        // CROWN JEWEL #3 — TryInterrupt is tick-blind.
        [Test]
        public void TryInterrupt_DoesNotConsultTick_NonInterruptibleExpired_StillReturnsFalse()
        {
            // Arrange — non-interruptible state that has run past its lock
            // tick but UpdateExpiry has NOT been called yet. (This is the
            // micro-window between simulation phases.)
            var sm = new BrawlerActionStateMachine();
            sm.Enter(
                BrawlerActionStateType.Stunned,
                currentTick: 100,
                durationTicks: 30,
                allowMovement: false,
                allowActionInput: false,
                isInterruptible: false);
            // Note: NOT calling sm.UpdateExpiry(200). The state is "stale".

            // Act — TryInterrupt at a tick well past LockUntilTick.
            bool result = sm.TryInterrupt();

            // Assert
            Assert.IsFalse(result,
                "TryInterrupt does NOT consult the current tick. A non-" +
                "interruptible expired state still returns false. UpdateExpiry " +
                "is the only path that clears non-interruptible states. If a " +
                "refactor adds a 'just expire it' shortcut here, it changes " +
                "interrupt semantics — callers would suddenly succeed where " +
                "they used to fail mid-tick.");
            Assert.AreEqual(BrawlerActionStateType.Stunned, sm.Current.StateType,
                "And the underlying state must still be visible until UpdateExpiry runs.");
        }
    }
}
