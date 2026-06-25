using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIWinConditionUtilityTests
    {
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
            AIGameModeObjectivePhase phase)
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
                false,
                false,
                false,
                "test");
        }
    }
}
