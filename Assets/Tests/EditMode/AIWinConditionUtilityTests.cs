using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIWinConditionUtilityTests
    {
        [Test]
        public void EvaluateTarget_EnemyCountdownCarrierBecomesCollapseTarget()
        {
            AIWinConditionTargetEvaluation evaluation =
                AIWinConditionUtility.EvaluateTarget(
                    new AIWinConditionTargetContext(
                        GemGrabMacro(
                            AIGameModeMacroCall.Reset,
                            ownGems: 7,
                            enemyGems: 10,
                            enemyCountdown: true,
                            timer: 2f),
                        selfCarriedGems: 0,
                        targetCarriedGems: 5,
                        targetHealthRatio: 0.70f,
                        distance: 6f,
                        isCurrentTarget: false,
                        isTeamFocusTarget: true,
                        alliedFocusCount: 1));

            Assert.IsTrue(evaluation.IsHighValueTarget);
            Assert.IsTrue(evaluation.ShouldCollapse);
            Assert.GreaterOrEqual(evaluation.ScoreDelta, 90f);
            StringAssert.Contains("break_countdown", evaluation.Reason);
        }

        [Test]
        public void EvaluateTarget_GemCarrierOutranksPlainLowHealthTarget()
        {
            AIGameModeMacroState macro = GemGrabMacro(
                AIGameModeMacroCall.Push,
                ownGems: 5,
                enemyGems: 8);

            AIWinConditionTargetEvaluation lowHealth =
                AIWinConditionUtility.EvaluateTarget(
                    new AIWinConditionTargetContext(
                        macro,
                        selfCarriedGems: 0,
                        targetCarriedGems: 0,
                        targetHealthRatio: 0.18f,
                        distance: 5f,
                        isCurrentTarget: false,
                        isTeamFocusTarget: false,
                        alliedFocusCount: 0));

            AIWinConditionTargetEvaluation carrier =
                AIWinConditionUtility.EvaluateTarget(
                    new AIWinConditionTargetContext(
                        macro,
                        selfCarriedGems: 0,
                        targetCarriedGems: 3,
                        targetHealthRatio: 0.18f,
                        distance: 5f,
                        isCurrentTarget: false,
                        isTeamFocusTarget: false,
                        alliedFocusCount: 0));

            Assert.IsTrue(lowHealth.IsHighValueTarget);
            Assert.IsTrue(carrier.ShouldCollapse);
            Assert.Greater(carrier.ScoreDelta, lowHealth.ScoreDelta + 30f);
        }

        [Test]
        public void EvaluateTarget_GemCarrierSwingTargetBecomesPriority()
        {
            AIWinConditionTargetEvaluation evaluation =
                AIWinConditionUtility.EvaluateTarget(
                    new AIWinConditionTargetContext(
                        GemGrabMacro(
                            AIGameModeMacroCall.Push,
                            ownGems: 7,
                            enemyGems: 9),
                        selfCarriedGems: 0,
                        targetCarriedGems: 2,
                        targetHealthRatio: 0.60f,
                        distance: 5.5f,
                        isCurrentTarget: false,
                        isTeamFocusTarget: false,
                        alliedFocusCount: 0));

            Assert.IsTrue(evaluation.IsHighValueTarget);
            Assert.IsTrue(evaluation.ShouldCollapse);
            StringAssert.Contains("lead_swing_target", evaluation.Reason);
            StringAssert.Contains("reachable_target", evaluation.Reason);
        }

        [Test]
        public void EvaluateTarget_FarNonCountdownCarrierDoesNotForceCollapse()
        {
            AIWinConditionTargetEvaluation evaluation =
                AIWinConditionUtility.EvaluateTarget(
                    new AIWinConditionTargetContext(
                        GemGrabMacro(
                            AIGameModeMacroCall.Push,
                            ownGems: 5,
                            enemyGems: 5),
                        selfCarriedGems: 0,
                        targetCarriedGems: 2,
                        targetHealthRatio: 0.90f,
                        distance: 13f,
                        isCurrentTarget: false,
                        isTeamFocusTarget: false,
                        alliedFocusCount: 0));

            Assert.IsTrue(evaluation.IsHighValueTarget);
            Assert.IsFalse(evaluation.ShouldCollapse);
            StringAssert.Contains("far_target", evaluation.Reason);
        }

        [Test]
        public void EvaluateAction_OwnCountdownCarrierPrioritizesSafety()
        {
            var context = new AIWinConditionActionContext(
                GemGrabMacro(
                    AIGameModeMacroCall.Hold,
                    ownGems: 10,
                    enemyGems: 6,
                    ownCountdown: true,
                    timer: 4f),
                selfCarriedGems: 4,
                targetCarriedGems: 0,
                targetHealthRatio: 1f,
                hasLiveTarget: false);

            AIWinConditionActionEvaluation retreat =
                AIWinConditionUtility.EvaluateAction(AIActionType.Retreat, context);
            AIWinConditionActionEvaluation objective =
                AIWinConditionUtility.EvaluateAction(AIActionType.Objective, context);

            Assert.AreEqual(24f, retreat.Delta);
            Assert.AreEqual("win_hold_carrier", retreat.Reason);
            Assert.AreEqual(-16f, objective.Delta);
        }

        [Test]
        public void EvaluateAction_NoTargetContestBoostsGemMineControl()
        {
            var context = new AIWinConditionActionContext(
                GemGrabMacro(
                    AIGameModeMacroCall.Push,
                    ownGems: 5,
                    enemyGems: 7),
                selfCarriedGems: 0,
                targetCarriedGems: 0,
                targetHealthRatio: 1f,
                hasLiveTarget: false);

            AIWinConditionActionEvaluation objective =
                AIWinConditionUtility.EvaluateAction(AIActionType.Objective, context);
            AIWinConditionActionEvaluation search =
                AIWinConditionUtility.EvaluateAction(AIActionType.Search, context);
            AIWinConditionActionEvaluation wander =
                AIWinConditionUtility.EvaluateAction(AIActionType.Wander, context);

            Assert.AreEqual(14f, objective.Delta);
            Assert.AreEqual(9f, search.Delta);
            Assert.AreEqual(-8f, wander.Delta);
            Assert.AreEqual("gem_mine_control", objective.Reason);
        }

        [Test]
        public void EvaluateAction_KnockoutLowHealthTargetEncouragesConfirm()
        {
            var context = new AIWinConditionActionContext(
                Macro(
                    GameModeId.Knockout,
                    AIGameModeMacroCall.Push,
                    AIGameModeObjectivePhase.Contest),
                selfCarriedGems: 0,
                targetCarriedGems: 0,
                targetHealthRatio: 0.22f,
                hasLiveTarget: true);

            AIWinConditionActionEvaluation approach =
                AIWinConditionUtility.EvaluateAction(AIActionType.Approach, context);
            AIWinConditionActionEvaluation useSuper =
                AIWinConditionUtility.EvaluateAction(AIActionType.UseSuper, context);

            Assert.AreEqual(16f, approach.Delta);
            Assert.AreEqual(20f, useSuper.Delta);
            Assert.AreEqual("knockout_confirm", useSuper.Reason);
        }

        [Test]
        public void EvaluateTarget_KnockoutCloseLowHealthTargetCoordinatesCollapse()
        {
            AIWinConditionTargetEvaluation evaluation =
                AIWinConditionUtility.EvaluateTarget(
                    new AIWinConditionTargetContext(
                        Macro(
                            GameModeId.Knockout,
                            AIGameModeMacroCall.Push,
                            AIGameModeObjectivePhase.Contest,
                            isBehind: true),
                        selfCarriedGems: 0,
                        targetCarriedGems: 0,
                        targetHealthRatio: 0.24f,
                        distance: 4f,
                        isCurrentTarget: false,
                        isTeamFocusTarget: true,
                        alliedFocusCount: 1));

            Assert.IsTrue(evaluation.IsHighValueTarget);
            Assert.IsTrue(evaluation.ShouldCollapse);
            StringAssert.Contains("confirm_window", evaluation.Reason);
            StringAssert.Contains("collapse_ready", evaluation.Reason);
            StringAssert.Contains("team_focus", evaluation.Reason);
        }

        private static AIGameModeMacroState GemGrabMacro(
            AIGameModeMacroCall call,
            int ownGems,
            int enemyGems,
            bool ownCountdown = false,
            bool enemyCountdown = false,
            float timer = 0f)
        {
            return new AIGameModeMacroState(
                GameModeId.GemGrab,
                call,
                AIGameModeObjectivePhase.Contest,
                ownGems,
                enemyGems,
                10,
                timer,
                80f,
                ownGems > enemyGems,
                enemyGems > ownGems,
                ownCountdown,
                enemyCountdown,
                "test");
        }

        private static AIGameModeMacroState Macro(
            GameModeId mode,
            AIGameModeMacroCall call,
            AIGameModeObjectivePhase phase,
            bool isBehind = false)
        {
            return new AIGameModeMacroState(
                mode,
                call,
                phase,
                0,
                0,
                2,
                0f,
                80f,
                false,
                isBehind,
                false,
                false,
                "test");
        }
    }
}
