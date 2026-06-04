using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AITeamPlaybookDirectorTests
    {
        [Test]
        public void Resolve_EscortsAllyCarrier_WhenTeamShouldHold()
        {
            AITeamPlaybookContext context = BaseContext(101, AIGameModeMacroCall.Hold);
            context.HasAllyCarrier = true;
            context.AllyCarrierEntityId = 500;
            context.AllyCarrierGemCount = 5;
            context.AllyCarrierPosition = new Vector3(4f, 0f, 2f);

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.EscortCarrier, state.Call);
            Assert.AreEqual(AITeamLaneAssignment.Escort, state.Lane);
            Assert.AreEqual(AITeamEscortFormationRole.Shadow, state.EscortRole);
            Assert.AreEqual(2, state.EscortSlot);
            Assert.AreEqual(500, state.CarrierEntityId);
            Assert.IsTrue(state.HasEscortTargetPoint);
        }

        [Test]
        public void Resolve_AnchorsSelfCarrier_WhenProtectingOwnGems()
        {
            AITeamPlaybookContext context = BaseContext(102, AIGameModeMacroCall.Hold);
            context.SelfIsCarrier = true;
            context.SelfCarriedGems = 4;

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.EscortCarrier, state.Call);
            Assert.AreEqual(AITeamLaneAssignment.Anchor, state.Lane);
            Assert.AreEqual(AITeamEscortFormationRole.CarrierAnchor, state.EscortRole);
            Assert.AreEqual(-1, state.EscortSlot);
            Assert.AreEqual(102, state.CarrierEntityId);
        }

        [Test]
        public void Resolve_ScreensCarrier_WhenFrontlineEscortSeesThreatPressure()
        {
            AITeamPlaybookContext context = BaseContext(32, AIGameModeMacroCall.Hold);
            context.Archetype = BrawlerArchetype.Tank;
            context.HasAllyCarrier = true;
            context.AllyCarrierEntityId = 500;
            context.AllyCarrierGemCount = 8;
            context.AllyCarrierPosition = new Vector3(4f, 0f, 2f);
            context.HasThreatCenter = true;
            context.ThreatCenterPosition = new Vector3(8f, 0f, 2f);
            context.ThreatCenterPressure = 2f;

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.EscortCarrier, state.Call);
            Assert.AreEqual(AITeamEscortFormationRole.Screen, state.EscortRole);
            Assert.AreEqual(1, state.EscortSlot);
            Assert.AreEqual(AITeamLaneAssignment.Escort, state.Lane);
            Assert.Greater(state.EscortTargetPoint.x, context.AllyCarrierPosition.x);
            Assert.AreEqual(context.AllyCarrierPosition.z, state.EscortTargetPoint.z, 0.01f);
        }

        [Test]
        public void Resolve_FlanksFromCarrier_WhenBacklineEscortSeesThreatPressure()
        {
            AITeamPlaybookContext context = BaseContext(30, AIGameModeMacroCall.Hold);
            context.Archetype = BrawlerArchetype.Sniper;
            context.HasAllyCarrier = true;
            context.AllyCarrierEntityId = 500;
            context.AllyCarrierGemCount = 8;
            context.AllyCarrierPosition = new Vector3(4f, 0f, 2f);
            context.HasEnemyHotspot = true;
            context.EnemyHotspotPosition = new Vector3(8f, 0f, 2f);
            context.EnemyHotspotPressure = 1.5f;

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.EscortCarrier, state.Call);
            Assert.AreEqual(AITeamEscortFormationRole.PressureFlank, state.EscortRole);
            Assert.AreEqual(2, state.EscortSlot);
            Assert.AreEqual(AITeamLaneAssignment.Flank, state.Lane);
            Assert.Greater(state.EscortTargetPoint.x, context.AllyCarrierPosition.x);
            Assert.AreNotEqual(context.AllyCarrierPosition.z, state.EscortTargetPoint.z);
        }

        [Test]
        public void Resolve_KeepsExtraFrontlineEscortAsShadow_WhenScreenSlotIsOccupiedByBudget()
        {
            AITeamPlaybookContext context = BaseContext(30, AIGameModeMacroCall.Hold);
            context.Archetype = BrawlerArchetype.Tank;
            context.HasAllyCarrier = true;
            context.AllyCarrierEntityId = 500;
            context.AllyCarrierGemCount = 8;
            context.AllyCarrierPosition = new Vector3(4f, 0f, 2f);
            context.HasThreatCenter = true;
            context.ThreatCenterPosition = new Vector3(8f, 0f, 2f);
            context.ThreatCenterPressure = 2f;

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(2, state.EscortSlot);
            Assert.AreEqual(AITeamEscortFormationRole.Shadow, state.EscortRole);
            Assert.AreEqual(AITeamLaneAssignment.Escort, state.Lane);
        }

        [Test]
        public void Resolve_ResetOverridesCarrierProtection_WhenEnemyCountdownIsActive()
        {
            AITeamPlaybookContext context = BaseContext(
                103,
                new AIGameModeMacroState(
                    AIGameModeMacroCall.Reset,
                    AIGameModeObjectivePhase.Countdown,
                    7,
                    10,
                    10,
                    6f,
                    80f,
                    false,
                    true,
                    false,
                    true));
            context.HasAllyCarrier = true;
            context.AllyCarrierEntityId = 500;
            context.AllyCarrierGemCount = 5;

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.Reset, state.Call);
            Assert.AreEqual(AITeamLaneAssignment.Mid, state.Lane);
        }

        [Test]
        public void Resolve_KnockoutResetStabilizesInsteadOfOverPushing()
        {
            AITeamPlaybookContext context = BaseContext(
                106,
                new AIGameModeMacroState(
                    GameModeId.Knockout,
                    AIGameModeMacroCall.Reset,
                    AIGameModeObjectivePhase.Contest,
                    0,
                    0,
                    2,
                    0f,
                    80f,
                    false,
                    false,
                    false,
                    false,
                    "down_players"));

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.Hold, state.Call);
            Assert.AreEqual("knockout_stabilize", state.Reason);
        }

        [Test]
        public void Resolve_CallsPinchPressure_WhenPushHasFocusTarget()
        {
            AITeamPlaybookContext context = BaseContext(104, AIGameModeMacroCall.Push);
            context.HasFocusTarget = true;
            context.FocusTargetEntityId = 900;
            context.FocusTargetPosition = new Vector3(8f, 0f, 0f);
            context.HasEnemyHotspot = true;
            context.EnemyHotspotPosition = new Vector3(8f, 0f, 0f);
            context.EnemyHotspotPressure = 1.5f;

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.PinchPressure, state.Call);
            Assert.AreEqual(AITeamLaneAssignment.Flank, state.Lane);
            Assert.AreEqual(900, state.FocusTargetEntityId);
            Assert.IsTrue(state.HasPressurePoint);
        }

        [Test]
        public void Resolve_CallsBaitAndCollapse_WhenSelfIsThreatenedAlly()
        {
            AITeamPlaybookContext context = BaseContext(105, AIGameModeMacroCall.Neutral);
            context.HasAllyUnderThreat = true;
            context.SelfIsThreatenedAlly = true;
            context.ThreatenedAllyEntityId = 105;
            context.ThreatenedAllyPosition = new Vector3(1f, 0f, 1f);
            context.HasThreatCenter = true;
            context.ThreatCenterPosition = new Vector3(2f, 0f, 1f);
            context.ThreatCenterPressure = 2f;

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.BaitAndCollapse, state.Call);
            Assert.AreEqual(AITeamLaneAssignment.Bait, state.Lane);
            Assert.IsTrue(state.HasAnchorPoint);
            Assert.IsTrue(state.HasPressurePoint);
        }

        [Test]
        public void Resolve_DistributesPushLaneAssignments_ByStableEntityId()
        {
            Assert.AreEqual(
                AITeamLaneAssignment.Left,
                AITeamPlaybookDirector.Resolve(BaseContext(30, AIGameModeMacroCall.Push)).Lane);
            Assert.AreEqual(
                AITeamLaneAssignment.Mid,
                AITeamPlaybookDirector.Resolve(BaseContext(31, AIGameModeMacroCall.Push)).Lane);
            Assert.AreEqual(
                AITeamLaneAssignment.Right,
                AITeamPlaybookDirector.Resolve(BaseContext(32, AIGameModeMacroCall.Push)).Lane);
        }

        [Test]
        public void Resolve_UsesLaneOwnershipRecommendation_ForPushLane()
        {
            AITeamPlaybookContext context = BaseContext(31, AIGameModeMacroCall.Push);
            context.HasLaneOwnership = true;
            context.LaneOwnership = new AITeamLaneOwnershipSnapshot(
                31,
                100u,
                AITeamLaneAssignment.Mid,
                AITeamLaneAssignment.Mid,
                AITeamLaneAssignment.Right,
                AITeamLaneAssignment.Right,
                AITeamLaneAssignment.Mid,
                1,
                2,
                0,
                false,
                true,
                true,
                false,
                10u,
                0u,
                "rebalance_underowned");

            AITeamPlaybookState state = AITeamPlaybookDirector.Resolve(context);

            Assert.AreEqual(AITeamPlaybookCall.Push, state.Call);
            Assert.AreEqual(AITeamLaneAssignment.Right, state.Lane);
        }

        private static AITeamPlaybookContext BaseContext(
            int botEntityId,
            AIGameModeMacroCall macroCall)
        {
            return BaseContext(
                botEntityId,
                new AIGameModeMacroState(
                    macroCall,
                    AIGameModeObjectivePhase.Contest,
                    4,
                    4,
                    10,
                    0f,
                    90f,
                    false,
                    false,
                    macroCall == AIGameModeMacroCall.Hold,
                    false));
        }

        private static AITeamPlaybookContext BaseContext(
            int botEntityId,
            AIGameModeMacroState macroState)
        {
            return new AITeamPlaybookContext
            {
                BotEntityId = botEntityId,
                Tick = 100u,
                SelfPosition = Vector3.zero,
                Archetype = BrawlerArchetype.Fighter,
                HealthRatio = 1f,
                MacroState = macroState
            };
        }
    }
}
