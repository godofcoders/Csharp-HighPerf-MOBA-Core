using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using System.Collections.Generic;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIUtilityScorer
    {
        private readonly BrawlerController _self;
        private readonly BrawlerAIProfile _profile;
        private readonly AIObjectiveMemory _objectiveMemory;
        private readonly AITeamCoordinator _teamCoordinator;
        private readonly AIReactiveMemory _reactiveMemory;
        private readonly AIDangerMemory _dangerMemory;
        private readonly AIChaseDisengageMemory _chaseDisengageMemory =
            new AIChaseDisengageMemory();

        private readonly uint _threatForgetTicks = 240;

        private bool IsSniper => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Sniper;
        private bool IsTank => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Tank;
        private bool IsAssassin => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Assassin;
        private bool IsSupport => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Support;
        private bool IsFighter => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Fighter;
        private bool IsController => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Controller;
        private bool IsArtillery => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Artillery;

        private const float MinActionScore = 0f;
        private const float MaxNormalActionScore = 100f;
        private const float MaxEmergencyActionScore = 120f;
        private readonly List<AIActionScore> _scoreBuffer = new List<AIActionScore>(16);
        private readonly List<ISpatialEntity> _nearbyAllyBuffer = new List<ISpatialEntity>(16);

        private float _lastObjectiveAllyPressure;
        private float _lastObjectiveCrowdingPenalty;
        private float _lastObjectiveRawScore;
        private float _lastObjectiveFinalScore;
        private string _lastObjectiveScoreReason;
        private string _lastTeamRoleDebug = "RoleCoord=None";
        private string _lastMacroDebug = "Macro=None";
        private string _lastPlaybookDebug = "Playbook=None";
        private string _lastChaseDebug = "Chase=None";
        private string _lastGemPickupDebug = "GemPickup=None";
        private string _lastObjectiveIntentDebug = "ObjIntent=None";
        private string _lastWinConditionDebug = "Win=None";
        private uint _lastLaneEvaluationTick;
        private bool _hasLaneEvaluation;
        private bool _lastCanHoldLane;
        private string _lastLaneHoldReason = "lane_not_evaluated";
        private bool _hasGemPickupDecisionCache;
        private uint _lastGemPickupDecisionTick;
        private AIGemPickupDecision _lastGemPickupDecision;
        private bool _lastGemPickupShouldPickup;

        public float LastObjectiveAllyPressure => _lastObjectiveAllyPressure;
        public float LastObjectiveCrowdingPenalty => _lastObjectiveCrowdingPenalty;
        public float LastObjectiveRawScore => _lastObjectiveRawScore;
        public float LastObjectiveFinalScore => _lastObjectiveFinalScore;
        public string LastObjectiveScoreReason => _lastObjectiveScoreReason;
        public string LastTeamRoleDebug => _lastTeamRoleDebug;
        public string LastMacroDebug => _lastMacroDebug;
        public string LastPlaybookDebug => _lastPlaybookDebug;
        public string LastChaseDebug => _lastChaseDebug;
        public string LastGemPickupDebug => _lastGemPickupDebug;
        public string LastObjectiveIntentDebug => _lastObjectiveIntentDebug;
        public string LastWinConditionDebug => _lastWinConditionDebug;

        public AIUtilityScorer(
            BrawlerController self,
            BrawlerAIProfile profile,
            AIObjectiveMemory objectiveMemory,
            AITeamCoordinator teamCoordinator,
            AIReactiveMemory reactiveMemory = null,
            AIDangerMemory dangerMemory = null)
        {
            _self = self;
            _profile = profile;
            _objectiveMemory = objectiveMemory;
            _teamCoordinator = teamCoordinator;
            _reactiveMemory = reactiveMemory;
            _dangerMemory = dangerMemory;
        }

        public AIActionScore ScoreBestAction(AITargetInfo targetInfo, uint currentTick)
        {
            CollectActionScores(targetInfo, currentTick, _scoreBuffer);

            AIActionScore best = new AIActionScore(AIActionType.Wander, 0f);
            for (int i = 0; i < _scoreBuffer.Count; i++)
            {
                ScoreAndReplace(ref best, _scoreBuffer[i]);
            }

            return best;
        }

        public void CollectActionScores(AITargetInfo targetInfo, uint currentTick, List<AIActionScore> results)
        {
            results.Clear();

            AIGameModeMacroState macroState = ResolveMacroState();
            AITeamPlaybookState playbookState = ResolvePlaybookState(
                targetInfo,
                currentTick,
                macroState);

            results.Add(ScoreEvade());
            results.Add(ScoreRetreat(targetInfo, currentTick, macroState));
            results.Add(ScoreUseSuper(targetInfo, macroState));
            results.Add(ScoreHoldRange(targetInfo));
            results.Add(ScoreReposition(targetInfo, currentTick, macroState));
            results.Add(ScoreApproach(targetInfo, currentTick, macroState));
            results.Add(ScorePeel(currentTick, macroState));
            results.Add(ScoreRegroup(targetInfo, currentTick, macroState));
            results.Add(ScoreSearch(targetInfo, currentTick, macroState));
            results.Add(ScoreWander());
            results.Add(ScoreObjective(targetInfo, currentTick, macroState));

            ApplyObjectiveIntentArbitration(
                targetInfo,
                currentTick,
                macroState,
                playbookState,
                results);
            ApplyWinConditionPressure(targetInfo, macroState, results);
            ApplyTeamRoleCoordination(targetInfo, currentTick, results);
            ApplyPlaybookCoordination(targetInfo, playbookState, results);
        }

        private AIGameModeMacroState ResolveMacroState()
        {
            AIGameModeMacroState state =
                AIGameModeMacroStrategy.ResolveCurrentMode(_self.Team);

            _lastMacroDebug = state.GetDebugSummary();
            return state;
        }

        private AITeamPlaybookState ResolvePlaybookState(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (_teamCoordinator == null)
            {
                _lastPlaybookDebug = "Playbook=None";
                return AITeamPlaybookState.None(currentTick);
            }

            AITeamPlaybookState state = _teamCoordinator.UpdatePlaybook(
                targetInfo,
                macroState,
                currentTick);

            _lastPlaybookDebug = _teamCoordinator.LastPlaybookDebug;
            return state;
        }

        private void ScoreAndReplace(ref AIActionScore best, AIActionScore candidate)
        {
            if (candidate.Score > best.Score)
                best = candidate;
        }

        private AIActionScore MakeScore(
    AIActionType actionType,
    float rawScore,
    float weight = 1f,
    bool allowEmergencyScore = false)
        {
            float weightedScore = rawScore * Mathf.Max(0f, weight);

            float maxScore = allowEmergencyScore
                ? MaxEmergencyActionScore
                : MaxNormalActionScore;

            return new AIActionScore(
                actionType,
                Mathf.Clamp(weightedScore, MinActionScore, maxScore));
        }

        private void ApplyTeamRoleCoordination(AITargetInfo targetInfo, uint currentTick, List<AIActionScore> results)
        {
            _lastTeamRoleDebug = "RoleCoord=Off";

            if (_profile == null ||
                !_profile.UseTeamRoleCoordination ||
                _teamCoordinator == null ||
                results == null ||
                results.Count == 0)
            {
                return;
            }

            int approachAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Approach, currentTick);
            int holdAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.HoldRange, currentTick);
            int repositionAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Reposition, currentTick);
            int peelAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Peel, currentTick);
            int regroupAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Regroup, currentTick);
            int objectiveAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Objective, currentTick);
            int searchAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Search, currentTick);

            string deltaDebug = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateTeamRoleDelta(
                    actionScore,
                    targetInfo,
                    approachAllies,
                    peelAllies,
                    regroupAllies,
                    objectiveAllies,
                    searchAllies);

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);

                results[i] = new AIActionScore(actionScore.ActionType, adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{delta:+0.0;-0.0}");
            }

            _lastTeamRoleDebug =
                $"A={approachAllies} H={holdAllies} R={repositionAllies} " +
                $"P={peelAllies} G={regroupAllies} O={objectiveAllies} S={searchAllies}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastTeamRoleDebug += $" Delta={deltaDebug}";
        }

        private void ApplyPlaybookCoordination(
            AITargetInfo targetInfo,
            AITeamPlaybookState playbookState,
            List<AIActionScore> results)
        {
            if (!playbookState.IsActive || results == null || results.Count == 0)
                return;

            float weight = GetPlaybookWeight();
            if (weight <= 0f)
                return;

            string deltaDebug = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculatePlaybookDelta(actionScore.ActionType, playbookState);

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreatePlaybookScore(actionScore.ActionType, targetInfo, playbookState))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta * weight * playbookState.Urgency);

                results[i] = new AIActionScore(actionScore.ActionType, adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{adjustedScore - actionScore.Score:+0.0;-0.0}");
            }

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastPlaybookDebug += $" Delta={deltaDebug}";
        }

        private void ApplyObjectiveIntentArbitration(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState,
            AITeamPlaybookState playbookState,
            List<AIActionScore> results)
        {
            _lastObjectiveIntentDebug = "ObjIntent=None";

            if (results == null || results.Count == 0)
                return;

            bool hasLiveTarget = targetInfo != null && targetInfo.HasLiveTarget;
            bool hasGemPickup = false;
            bool shouldPickupGem = false;
            float gemPickupScore = 0f;

            if (!hasLiveTarget &&
                TryResolveGemPickupDecision(
                    macroState,
                    currentTick,
                    out AIGemPickupDecision gemDecision))
            {
                hasGemPickup = gemDecision.HasPickup;
                shouldPickupGem = gemDecision.ShouldPickup;
                gemPickupScore = gemDecision.Score;
            }

            bool hasLaneHold = CanHoldAssignedLane(
                currentTick,
                out _);
            bool isCarrierPlaybook =
                playbookState.IsActive &&
                playbookState.Call == AITeamPlaybookCall.EscortCarrier;
            bool selfIsCarrierAnchor =
                _self != null &&
                playbookState.CarrierEntityId == _self.EntityID &&
                playbookState.EscortRole == AITeamEscortFormationRole.CarrierAnchor;

            var context = new AIObjectiveIntentContext(
                macroState,
                _self != null && _self.State != null
                    ? _self.State.CarriedGemCount
                    : 0,
                hasLiveTarget,
                hasLaneHold,
                hasGemPickup,
                shouldPickupGem,
                gemPickupScore,
                isCarrierPlaybook,
                selfIsCarrierAnchor);

            string deltaDebug = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                AIObjectiveIntentArbitrationResult intent =
                    AIObjectiveIntentArbitrationUtility.Evaluate(
                        actionScore.ActionType,
                        context);

                if (!intent.HasDelta)
                    continue;

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + intent.Delta);

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{adjustedScore - actionScore.Score:+0.0;-0.0}_{intent.Reason}");
            }

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastObjectiveIntentDebug = $"ObjIntent={deltaDebug}";
        }

        private void ApplyWinConditionPressure(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState,
            List<AIActionScore> results)
        {
            _lastWinConditionDebug = "Win=None";

            if (results == null || results.Count == 0)
                return;

            bool hasLiveTarget = targetInfo != null && targetInfo.HasLiveTarget;
            int selfCarriedGems = _self != null && _self.State != null
                ? _self.State.CarriedGemCount
                : 0;
            int targetCarriedGems = 0;
            float targetHealthRatio = 1f;

            if (hasLiveTarget &&
                targetInfo.Target is BrawlerController targetBrawler &&
                targetBrawler.State != null)
            {
                targetCarriedGems = targetBrawler.State.CarriedGemCount;
                targetHealthRatio = Mathf.Clamp01(
                    targetBrawler.State.CurrentHealth /
                    Mathf.Max(1f, targetBrawler.State.MaxHealth.Value));
            }

            var context = new AIWinConditionActionContext(
                macroState,
                selfCarriedGems,
                targetCarriedGems,
                targetHealthRatio,
                hasLiveTarget);

            string deltaDebug = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                AIWinConditionActionEvaluation evaluation =
                    AIWinConditionUtility.EvaluateAction(
                        actionScore.ActionType,
                        context);

                if (!evaluation.HasDelta)
                    continue;

                if (actionScore.Score <= 0f &&
                    evaluation.Delta > 0f &&
                    !CanCreateWinConditionScore(
                        actionScore.ActionType,
                        context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + evaluation.Delta);

                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}_{evaluation.Reason}");
            }

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastWinConditionDebug = $"Win={deltaDebug}";
        }

        private bool CanCreateWinConditionScore(
            AIActionType actionType,
            AIWinConditionActionContext context)
        {
            switch (actionType)
            {
                case AIActionType.Search:
                case AIActionType.Objective:
                    return !context.HasLiveTarget;

                case AIActionType.Retreat:
                case AIActionType.Regroup:
                    return context.MacroState.OwnTeamHasCountdown &&
                           context.SelfCarriedGems > 0;

                default:
                    return false;
            }
        }

        private float CalculatePlaybookDelta(
            AIActionType actionType,
            AITeamPlaybookState playbookState)
        {
            bool selfCarrier =
                _self != null &&
                _self.State != null &&
                _self.State.CarriedGemCount > 0 &&
                playbookState.CarrierEntityId == _self.EntityID;

            switch (playbookState.Call)
            {
                case AITeamPlaybookCall.Push:
                    return GetPushPlaybookDelta(actionType);

                case AITeamPlaybookCall.Hold:
                    return GetHoldPlaybookDelta(actionType, selfCarrier);

                case AITeamPlaybookCall.Reset:
                    return GetResetPlaybookDelta(actionType, selfCarrier);

                case AITeamPlaybookCall.EscortCarrier:
                    return GetEscortCarrierPlaybookDelta(
                        actionType,
                        playbookState.Lane,
                        playbookState.EscortRole,
                        selfCarrier);

                case AITeamPlaybookCall.PinchPressure:
                    return GetPinchPressurePlaybookDelta(actionType, playbookState.Lane);

                case AITeamPlaybookCall.BaitAndCollapse:
                    return GetBaitAndCollapsePlaybookDelta(actionType, playbookState.Lane);

                default:
                    return 0f;
            }
        }

        private float GetPushPlaybookDelta(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return 10f;
                case AIActionType.Reposition:
                    return 6f;
                case AIActionType.Objective:
                    return 8f;
                case AIActionType.Search:
                    return 6f;
                case AIActionType.Retreat:
                    return -6f;
                case AIActionType.Regroup:
                    return -4f;
                default:
                    return 0f;
            }
        }

        private float GetHoldPlaybookDelta(AIActionType actionType, bool selfCarrier)
        {
            switch (actionType)
            {
                case AIActionType.HoldRange:
                    return 12f;
                case AIActionType.Reposition:
                    return 4f;
                case AIActionType.Peel:
                    return 8f;
                case AIActionType.Regroup:
                    return selfCarrier ? 10f : 5f;
                case AIActionType.Retreat:
                    return selfCarrier ? 8f : 0f;
                case AIActionType.Approach:
                    return selfCarrier ? -16f : -6f;
                case AIActionType.Search:
                    return -6f;
                default:
                    return 0f;
            }
        }

        private float GetResetPlaybookDelta(AIActionType actionType, bool selfCarrier)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return selfCarrier ? -8f : 12f;
                case AIActionType.Reposition:
                    return 8f;
                case AIActionType.UseSuper:
                    return 6f;
                case AIActionType.Search:
                    return 8f;
                case AIActionType.Objective:
                    return 8f;
                case AIActionType.Regroup:
                    return selfCarrier ? 8f : -6f;
                default:
                    return 0f;
            }
        }

        private float GetEscortCarrierPlaybookDelta(
            AIActionType actionType,
            AITeamLaneAssignment lane,
            AITeamEscortFormationRole escortRole,
            bool selfCarrier)
        {
            if (selfCarrier || lane == AITeamLaneAssignment.Anchor)
            {
                switch (actionType)
                {
                    case AIActionType.Retreat:
                        return 14f;
                    case AIActionType.HoldRange:
                        return 12f;
                    case AIActionType.Regroup:
                        return 10f;
                    case AIActionType.Reposition:
                        return 6f;
                    case AIActionType.Approach:
                        return -18f;
                    case AIActionType.Objective:
                    case AIActionType.Search:
                        return -10f;
                    default:
                        return 0f;
                }
            }

            if (escortRole == AITeamEscortFormationRole.Screen)
            {
                switch (actionType)
                {
                    case AIActionType.Peel:
                        return 20f;
                    case AIActionType.HoldRange:
                        return 14f;
                    case AIActionType.Reposition:
                        return 12f;
                    case AIActionType.Approach:
                        return 4f;
                    case AIActionType.Regroup:
                        return 4f;
                    case AIActionType.Search:
                        return -10f;
                    default:
                        return 0f;
                }
            }

            if (escortRole == AITeamEscortFormationRole.PressureFlank)
            {
                switch (actionType)
                {
                    case AIActionType.Reposition:
                        return 16f;
                    case AIActionType.HoldRange:
                        return 12f;
                    case AIActionType.Peel:
                        return 10f;
                    case AIActionType.Approach:
                        return 6f;
                    case AIActionType.UseSuper:
                        return 4f;
                    case AIActionType.Search:
                        return -8f;
                    default:
                        return 0f;
                }
            }

            switch (actionType)
            {
                case AIActionType.Peel:
                    return 16f;
                case AIActionType.HoldRange:
                    return 10f;
                case AIActionType.Reposition:
                    return 8f;
                case AIActionType.Regroup:
                    return 6f;
                case AIActionType.Approach:
                    return -4f;
                case AIActionType.Search:
                    return -8f;
                default:
                    return 0f;
            }
        }

        private float GetPinchPressurePlaybookDelta(
            AIActionType actionType,
            AITeamLaneAssignment lane)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return lane == AITeamLaneAssignment.Flank ? 10f : 5f;
                case AIActionType.Reposition:
                    return lane == AITeamLaneAssignment.Flank ? 16f : 8f;
                case AIActionType.HoldRange:
                    return lane == AITeamLaneAssignment.Anchor ? 12f : 5f;
                case AIActionType.UseSuper:
                    return 4f;
                case AIActionType.Search:
                    return 8f;
                case AIActionType.Retreat:
                    return -6f;
                default:
                    return 0f;
            }
        }

        private float GetBaitAndCollapsePlaybookDelta(
            AIActionType actionType,
            AITeamLaneAssignment lane)
        {
            if (lane == AITeamLaneAssignment.Bait)
            {
                switch (actionType)
                {
                    case AIActionType.Retreat:
                        return 12f;
                    case AIActionType.HoldRange:
                        return 10f;
                    case AIActionType.Reposition:
                        return 8f;
                    case AIActionType.Approach:
                        return -12f;
                    default:
                        return 0f;
                }
            }

            switch (actionType)
            {
                case AIActionType.Approach:
                    return 12f;
                case AIActionType.Peel:
                    return 14f;
                case AIActionType.Reposition:
                    return 10f;
                case AIActionType.UseSuper:
                    return 6f;
                case AIActionType.Retreat:
                    return -6f;
                default:
                    return 0f;
            }
        }

        private bool CanCreatePlaybookScore(
            AIActionType actionType,
            AITargetInfo targetInfo,
            AITeamPlaybookState playbookState)
        {
            switch (actionType)
            {
                case AIActionType.Search:
                    return playbookState.HasPressurePoint &&
                           (targetInfo == null || !targetInfo.HasLiveTarget);

                case AIActionType.Peel:
                    return playbookState.Call == AITeamPlaybookCall.BaitAndCollapse ||
                           (playbookState.Call == AITeamPlaybookCall.EscortCarrier &&
                            playbookState.HasEscortTargetPoint);

                default:
                    return targetInfo != null &&
                           targetInfo.HasLiveTarget &&
                           (actionType == AIActionType.Approach ||
                            actionType == AIActionType.HoldRange ||
                            actionType == AIActionType.Reposition);
            }
        }

        private float GetPlaybookWeight()
        {
            if (_profile == null || !_profile.UseTeamRoleCoordination)
                return 0f;

            float teamplay = _self != null && _self.Definition != null
                ? _self.Definition.TeamplayWeight
                : 1f;

            return Mathf.Clamp(
                teamplay * Mathf.Max(0.35f, _profile.TeamRoleCoordinationWeight),
                0.25f,
                1.50f);
        }

        private float CalculateTeamRoleDelta(
            AIActionScore actionScore,
            AITargetInfo targetInfo,
            int approachAllies,
            int peelAllies,
            int regroupAllies,
            int objectiveAllies,
            int searchAllies)
        {
            if (actionScore.Score <= 0f)
                return 0f;

            float weight = Mathf.Max(0f, _profile.TeamRoleCoordinationWeight);
            if (weight <= 0f)
                return 0f;

            float delta = 0f;
            int capacity = GetTeamActionCapacity(actionScore.ActionType);
            int alliedSameAction = GetKnownAlliedActionCount(
                actionScore.ActionType,
                approachAllies,
                peelAllies,
                regroupAllies,
                objectiveAllies,
                searchAllies);

            if (capacity >= 0)
            {
                int excess = (alliedSameAction + 1) - capacity;
                if (excess > 0)
                    delta -= excess * Mathf.Max(0f, _profile.TeamActionCrowdingPenalty);
            }

            if (targetInfo.HasLiveTarget)
            {
                if (actionScore.ActionType == AIActionType.Approach &&
                    CanServeFrontline() &&
                    approachAllies == 0)
                {
                    delta += _profile.TeamFrontlineNeedBonus;
                }

                if ((actionScore.ActionType == AIActionType.HoldRange ||
                     actionScore.ActionType == AIActionType.Reposition) &&
                    IsBacklineRole() &&
                    approachAllies > 0)
                {
                    delta += _profile.TeamBacklineAnchorBonus;
                }
            }

            return delta * weight;
        }

        private int GetTeamActionCapacity(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return Mathf.Max(0, _profile.MaxTeamApproachers);

                case AIActionType.Peel:
                    return Mathf.Max(0, _profile.MaxTeamPeelResponders);

                case AIActionType.Regroup:
                    return Mathf.Max(0, _profile.MaxTeamRegroupResponders);

                case AIActionType.Objective:
                    return Mathf.Max(0, _profile.MaxTeamObjectiveMovers);

                case AIActionType.Search:
                    return 1;

                default:
                    return -1;
            }
        }

        private int GetKnownAlliedActionCount(
            AIActionType actionType,
            int approachAllies,
            int peelAllies,
            int regroupAllies,
            int objectiveAllies,
            int searchAllies)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return approachAllies;

                case AIActionType.Peel:
                    return peelAllies;

                case AIActionType.Regroup:
                    return regroupAllies;

                case AIActionType.Objective:
                    return objectiveAllies;

                case AIActionType.Search:
                    return searchAllies;

                default:
                    return 0;
            }
        }

        private float ClampActionScore(AIActionType actionType, float score)
        {
            float maxScore = IsEmergencyAction(actionType)
                ? MaxEmergencyActionScore
                : MaxNormalActionScore;

            return Mathf.Clamp(score, MinActionScore, maxScore);
        }

        private bool IsEmergencyAction(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Retreat:
                case AIActionType.Evade:
                case AIActionType.UseSuper:
                case AIActionType.Peel:
                    return true;

                default:
                    return false;
            }
        }

        private bool CanServeFrontline()
        {
            if (!(IsTank || IsFighter || IsAssassin))
                return false;

            if (_self.State == null)
                return true;

            float healthRatio = _self.State.CurrentHealth /
                                Mathf.Max(1f, _self.State.MaxHealth.Value);

            return healthRatio >= Mathf.Max(0.35f, _profile.LowHealthRetreatRatio + 0.10f);
        }

        private bool IsBacklineRole()
        {
            return IsSniper || IsSupport || IsController || IsArtillery;
        }

        private string AppendRoleDebug(string current, string value)
        {
            return string.IsNullOrEmpty(current)
                ? value
                : $"{current},{value}";
        }

        private float AddArchetypeBias(
            float score,
            float sniper = 0f,
            float tank = 0f,
            float assassin = 0f,
            float support = 0f,
            float fighter = 0f,
            float controller = 0f,
            float artillery = 0f)
        {
            if (IsSniper) score += sniper;
            if (IsTank) score += tank;
            if (IsAssassin) score += assassin;
            if (IsSupport) score += support;
            if (IsFighter) score += fighter;
            if (IsController) score += controller;
            if (IsArtillery) score += artillery;

            return score;
        }

        private float GetMacroActionDelta(
            AIActionType actionType,
            AIGameModeMacroState macroState,
            int selfCarriedGems,
            int targetCarriedGems,
            int allyCarriedGems)
        {
            return GetMacroActionDelta(
                actionType,
                macroState,
                selfCarriedGems,
                targetCarriedGems,
                allyCarriedGems,
                out _);
        }

        private float GetMacroActionDelta(
            AIActionType actionType,
            AIGameModeMacroState macroState,
            int selfCarriedGems,
            int targetCarriedGems,
            int allyCarriedGems,
            out string reason)
        {
            reason = "macro_none";

            if (_profile == null || _profile.MacroActionBiasWeight <= 0f)
                return 0f;

            var context = new AIMacroActionContext(
                macroState,
                selfCarriedGems,
                targetCarriedGems,
                allyCarriedGems);

            AIMacroActionPolicyResult result =
                AIMacroActionPolicy.Evaluate(actionType, context);

            reason = result.Reason;
            if (!result.HasDelta)
                return 0f;

            return result.Delta * Mathf.Max(0f, _profile.MacroActionBiasWeight);
        }

        private AIActionScore ScoreRetreat(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (_self.State == null)
                return new AIActionScore(AIActionType.Retreat, 0f);

            float score = 0f;
            bool hasRetreatReason = false;
            float healthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);

            if (healthRatio <= _profile.LowHealthRetreatRatio)
            {
                score += 70f;
                hasRetreatReason = true;
            }

            if (_self.State.HasStatus(StatusEffectType.Burn))
            {
                score += 25f;
                hasRetreatReason = true;
            }

            if (_self.State.HasStatus(StatusEffectType.Stun))
                return new AIActionScore(AIActionType.Retreat, 0f);

            if (targetInfo.HasLiveTarget)
            {
                float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);
                if (dist <= _profile.GetTooCloseDistance(GetAbilityIdealRange()))
                {
                    score += 20f;
                    hasRetreatReason = true;
                }
            }

            float reactivePressure = GetReactiveDamagePressure(currentTick);
            if (reactivePressure > 0f)
            {
                score += reactivePressure * _profile.ReactiveRetreatPressureBonus;
                hasRetreatReason = true;

                if (healthRatio <= _profile.ReactiveEmergencyHealthRatio)
                    score += reactivePressure * 30f;
            }

            int selfCarriedGems = _self.State.CarriedGemCount;
            if (selfCarriedGems > 0)
            {
                score += 6f * selfCarriedGems;
                hasRetreatReason = true;
            }

            float macroDelta = GetMacroActionDelta(
                AIActionType.Retreat,
                macroState,
                selfCarriedGems,
                0,
                0);
            if (Mathf.Abs(macroDelta) > 0.01f)
            {
                score += macroDelta;
                if (macroDelta > 0f)
                    hasRetreatReason = true;
            }

            if (!hasRetreatReason)
                return new AIActionScore(AIActionType.Retreat, 0f);

            float roleSurvival = _self.Definition != null ? _self.Definition.SurvivalInstinct : 1f;
            score *= roleSurvival;

            if (IsSniper) score += 10f;
            if (IsSupport) score += 8f;
            if (IsTank) score -= 10f;
            if (IsAssassin) score -= 6f;
            if (IsController) score += 5f;
            if (IsArtillery) score += 12f;

            return MakeScore(
    AIActionType.Retreat,
    score,
    _profile.RetreatWeight,
    allowEmergencyScore: true);
        }

        private AIActionScore ScoreEvade()
        {
            if (_dangerMemory == null ||
                !_dangerMemory.HasDanger ||
                _dangerMemory.Pressure < _profile.DangerEvadePressureThreshold)
            {
                return new AIActionScore(AIActionType.Evade, 0f);
            }

            float score = 35f + (_dangerMemory.Pressure * _profile.DangerEvadeScoreBonus);

            if (_self.State != null)
            {
                float healthRatio = _self.State.CurrentHealth /
                                    Mathf.Max(1f, _self.State.MaxHealth.Value);

                if (healthRatio <= _profile.LowHealthRetreatRatio)
                    score += 15f;
            }

            score = AddArchetypeBias(
                score,
                sniper: 10f,
                tank: -10f,
                assassin: 4f,
                support: 8f,
                fighter: 0f,
                controller: 5f,
                artillery: 12f);

            return MakeScore(
                AIActionType.Evade,
                score,
                1f,
                allowEmergencyScore: true);
        }

        private AIActionScore ScoreUseSuper(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState)
        {
            if (_self.State == null || !_self.State.SuperCharge.IsReady || !targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.UseSuper, 0f);

            float score = 50f;

            if (targetInfo.Target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                float targetHealthRatio = targetBrawler.State.CurrentHealth /
                                          Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);

                if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                    score += 40f;

                if (targetBrawler.State.HasStatus(StatusEffectType.Slow))
                    score += 20f;

                if (targetHealthRatio <= _profile.SuperLowHealthTargetThreshold)
                    score += 25f;
            }

            if (IsAssassin) score += 15f;
            if (IsTank) score += 10f;
            if (IsSupport) score += 6f;
            if (IsSniper) score += 4f;
            if (IsController) score += 14f;
            if (IsArtillery) score += 8f;

            // Gem Grab: target carrying many gems → strong incentive to
            // burst them down. Killing a 3-gem carrier scatters 3 gems back
            // to the enemy (or your team if you grab them). +5 per gem
            // means a 3-gem carrier gets +15 — same magnitude as the
            // Assassin baseline, so even non-burst archetypes will swing
            // their super at a fat carrier.
            if (targetInfo.Target is BrawlerController carrierTarget &&
                carrierTarget.State != null)
            {
                int targetCarriedGems = carrierTarget.State.CarriedGemCount;
                score += 5f * targetCarriedGems;
                score += GetMacroActionDelta(
                    AIActionType.UseSuper,
                    macroState,
                    0,
                    targetCarriedGems,
                    0);
            }

            return MakeScore(
     AIActionType.UseSuper,
     score,
     _profile.SuperWeight,
     allowEmergencyScore: true);
        }

        private AIActionScore ScoreHoldRange(AITargetInfo targetInfo)
        {
            if (!targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.HoldRange, 0f);

            float attackRange = GetAbilityMaxRange();
            float idealRange = GetAbilityIdealRange();
            float preferredRange = _profile.GetPreferredAttackRange(idealRange);

            float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);
            float score = 0f;

            if (dist <= attackRange && dist >= preferredRange * 0.60f)
                score += 55f;

            if (targetInfo.Target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                    score += 25f;
            }

            if (IsSniper) score += 20f;
            if (IsSupport) score += 10f;
            if (IsTank) score -= 12f;
            if (IsAssassin) score -= 6f;
            if (IsController) score += 12f;
            if (IsArtillery) score += 18f;

            float ammoPressure = GetAmmoPressure();
            if (ammoPressure > 0f)
            {
                float reloadHoldBonus = IsTank ? 5f : 12f;
                score += ammoPressure * reloadHoldBonus;
            }

            return MakeScore(
      AIActionType.HoldRange,
      score,
      _profile.HoldRangeWeight);
        }

        private AIActionScore ScoreReposition(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (!targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.Reposition, 0f);

            float idealRange = GetAbilityIdealRange();
            float tooClose = _profile.GetTooCloseDistance(idealRange);
            float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);

            float score = 0f;
            if (dist < tooClose)
                score += 60f;

            float reactivePressure = GetReactiveDamagePressure(currentTick);
            if (reactivePressure > 0f)
                score += reactivePressure * _profile.ReactiveRepositionPressureBonus;

            if (IsSniper) score += 15f;
            if (IsSupport) score += 10f;
            if (IsAssassin) score += 6f;
            if (IsTank) score -= 8f;
            if (IsController) score += 10f;
            if (IsArtillery) score += 14f;

            float ammoPressure = GetAmmoPressure();
            if (ammoPressure > 0f)
            {
                float reloadRepositionBonus = IsArtillery ? 18f : 14f;
                score += ammoPressure * reloadRepositionBonus;
            }

            CloseRangeMatchupEvaluation closeRange =
                EvaluateCloseRangeMatchup(
                    targetInfo,
                    dist,
                    currentTick,
                    macroState);

            if (closeRange.IsActive)
                score += closeRange.RepositionBonus;

            return MakeScore(
     AIActionType.Reposition,
     score,
     _profile.RepositionWeight);
        }

        private AIActionScore ScoreApproach(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _chaseDisengageMemory.Reset();
                _lastChaseDebug = "Chase=None Reason=no_target";
                return new AIActionScore(AIActionType.Approach, 0f);
            }

            float attackRange = GetAbilityMaxRange();
            float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);

            float score = 0f;
            if (dist > attackRange + _profile.AttackRangeBuffer)
                score += 50f;

            if (targetInfo.Target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                    score += 20f;

                if (targetBrawler.State.HasStatus(StatusEffectType.Slow))
                    score += 15f;
            }

            score += GetChaseDisciplineDelta(
                targetInfo,
                dist,
                currentTick,
                macroState);

            CloseRangeMatchupEvaluation closeRange =
                EvaluateCloseRangeMatchup(
                    targetInfo,
                    dist,
                    currentTick,
                    macroState);

            if (closeRange.IsActive)
            {
                score += closeRange.ApproachDelta;
                _lastChaseDebug = $"{_lastChaseDebug} CloseRole={closeRange.Reason}";
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetFocusTarget(currentTick, out var focusTarget) &&
                SpatialEntityUtility.IsAlive(focusTarget) &&
                targetInfo.Target.EntityID == focusTarget.EntityID)
            {
                float focusBonus = _profile.FocusFireWeight;
                if (closeRange.IsActive && closeRange.ApproachDelta < 0f)
                    focusBonus *= 0.35f;

                score += focusBonus;
            }

            float reactivePressure = GetReactiveDamagePressure(currentTick);
            if (reactivePressure > 0f &&
                _reactiveMemory != null &&
                _reactiveMemory.TryGetRecentAttacker(
                    currentTick,
                    _profile.ReactiveDamageMemoryTicks,
                    out ISpatialEntity recentAttacker) &&
                SpatialEntityUtility.IsSameEntity(targetInfo.Target, recentAttacker))
            {
                float reactiveBonus = _profile.ReactiveAttackerFocusBonus * reactivePressure;
                if (closeRange.IsActive && closeRange.ApproachDelta < 0f)
                    reactiveBonus *= 0.35f;

                score += reactiveBonus;
            }

            float roleAggression = _self.Definition != null ? _self.Definition.Aggression : 1f;
            score *= roleAggression;

            if (IsTank) score += 12f;
            if (IsAssassin) score += 10f;
            if (IsSniper) score -= 8f;
            if (IsSupport) score -= 6f;
            if (IsController) score -= 3f;
            if (IsArtillery) score -= 10f;

            float ammoPressure = GetAmmoPressure();
            if (ammoPressure > 0f)
            {
                float lowAmmoApproachPenalty = IsTank || IsAssassin ? 8f : 18f;
                score -= ammoPressure * lowAmmoApproachPenalty;
            }

            // Gem Grab: behind on gems → push harder. The behind-ness check
            // is null-safe so non-Gem-Grab matches and unit-test contexts
            // skip this branch entirely.
            if (GemGrabMode.Instance != null && GemGrabMode.Instance.IsTeamBehind(_self.Team))
                score += 8f;

            score += GetMacroActionDelta(
                AIActionType.Approach,
                macroState,
                _self.State != null ? _self.State.CarriedGemCount : 0,
                0,
                0);

            return MakeScore(
     AIActionType.Approach,
     score,
     _profile.ApproachWeight);
        }

        private AIActionScore ScoreSearch(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            // NEVER SEARCH DURING ACTIVE COMBAT
            if (targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.Search, 0f);

            float score = 0f;

            if (!targetInfo.HasLiveTarget &&
    targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
            {
                score += 30f;
            }

            if (AITeamMemory.TryGetRecentHotspot(
                _self.Team,
                currentTick,
                _profile.SharedHotspotMemoryTicks,
                out _))
            {
                score += 20f;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetThreatCenter(currentTick, out _, out float threatPressure))
            {
                score += Mathf.Clamp(threatPressure * 5f, 6f, 18f);
            }
            else if (_teamCoordinator != null &&
                     _teamCoordinator.TryGetEnemyHotspot(currentTick, out _, out float hotspotPressure))
            {
                score += Mathf.Clamp(hotspotPressure * 4f, 4f, 14f);
            }

            AIGemPickupDecision gemDecision = AIGemPickupDecision.None("not_checked");
            bool hasGemPickupDecision = TryResolveGemPickupDecision(
                    macroState,
                    currentTick,
                    out gemDecision);

            if (hasGemPickupDecision)
            {
                score += gemDecision.ShouldPickup
                    ? gemDecision.Score
                    : Mathf.Max(0f, gemDecision.Score * 0.20f);
            }

            AIGemMineControlEvaluation mineControl =
                AIGemGrabObjectiveUtility.EvaluateMineControl(
                    AIActionType.Search,
                    BuildGemMineControlContext(
                        macroState,
                        hasLiveTarget: false,
                        allyPressure: 0f,
                        gemDecision));
            if (mineControl.HasDelta)
            {
                score += mineControl.Delta;
                _lastGemPickupDebug += $" Mine={mineControl.Reason}_{mineControl.Delta:+0.0;-0.0}";
            }

            float laneSearchBonus = GetLaneDisciplineScoreBonus(
                currentTick,
                _profile.LaneHoldSearchScore,
                out _);
            if (laneSearchBonus > 0.01f)
            {
                score += laneSearchBonus;
            }

            score += GetMacroActionDelta(
                AIActionType.Search,
                macroState,
                0,
                0,
                0);

            return MakeScore(
      AIActionType.Search,
      score,
      _profile.SearchWeight);
        }

        private bool TryResolveGemPickupDecision(
            AIGameModeMacroState macroState,
            uint currentTick,
            out AIGemPickupDecision decision)
        {
            if (_hasGemPickupDecisionCache &&
                _lastGemPickupDecisionTick == currentTick)
            {
                decision = _lastGemPickupDecision;
                return decision.HasPickup &&
                       (_lastGemPickupShouldPickup || decision.Score > 0f);
            }

            bool hasThreatCenter = false;
            Vector3 threatCenter = Vector3.zero;
            float threatPressure = 0f;
            bool hasEnemyHotspot = false;
            Vector3 enemyHotspot = Vector3.zero;
            float hotspotPressure = 0f;

            if (_teamCoordinator != null)
            {
                hasThreatCenter = _teamCoordinator.TryGetThreatCenter(
                    currentTick,
                    out threatCenter,
                    out threatPressure);
                hasEnemyHotspot = _teamCoordinator.TryGetEnemyHotspot(
                    currentTick,
                    out enemyHotspot,
                    out hotspotPressure);
            }

            bool shouldPickup = AIGemGrabObjectiveUtility.TryFindBestPickup(
                _self,
                _profile,
                macroState,
                hasThreatCenter,
                threatCenter,
                threatPressure,
                hasEnemyHotspot,
                enemyHotspot,
                hotspotPressure,
                out decision);

            _lastGemPickupDebug = decision.GetDebugSummary();
            _hasGemPickupDecisionCache = true;
            _lastGemPickupDecisionTick = currentTick;
            _lastGemPickupDecision = decision;
            _lastGemPickupShouldPickup = shouldPickup;

            return decision.HasPickup && (shouldPickup || decision.Score > 0f);
        }

        private AIActionScore ScoreWander()
        {
            return new AIActionScore(AIActionType.Wander, 5f * _profile.WanderWeight);
        }

        private AIActionScore ScoreObjective(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            _lastObjectiveAllyPressure = 0f;
            _lastObjectiveCrowdingPenalty = 0f;
            _lastObjectiveRawScore = 0f;
            _lastObjectiveFinalScore = 0f;
            _lastObjectiveScoreReason = "not_evaluated";

            if (_objectiveMemory == null ||
                !_objectiveMemory.TryGetBestObjective(
                    _self.Position,
                    _profile.PreferredObjective,
                    _self.Team,
                    out AIObjectiveCandidate objective))
            {
                _lastObjectiveScoreReason = "no_objectives";
                return new AIActionScore(AIActionType.Objective, 0f);
            }

            bool isBrawlBallObjective =
                macroState.Mode == GameModeId.BrawlBall &&
                objective.IsRuntime &&
                objective.ObjectiveType == AIObjectiveType.Ball;

            // Combat usually owns the moment, but Brawl Ball is possession-led:
            // a visible enemy should not make bots forget the loose ball,
            // friendly carrier, or enemy carrier.
            if (targetInfo.HasLiveTarget && !isBrawlBallObjective)
            {
                _lastObjectiveScoreReason = "combat_target_exists";
                return new AIActionScore(AIActionType.Objective, 0f);
            }

            Vector3 objectivePosition = objective.Position;
            float objectiveRadius = Mathf.Max(0.5f, objective.Radius);

            float dist = Vector3.Distance(
                _self.Position,
                objectivePosition);

            float score = 45f;
            _lastObjectiveScoreReason =
                $"base_45|type_{objective.ObjectiveType}|radius_{objectiveRadius:0.0}";

            if (objective.IsRuntime)
                _lastObjectiveScoreReason += "|runtime";

            if (isBrawlBallObjective)
            {
                float ballDelta = macroState.Call == AIGameModeMacroCall.Reset
                    ? 54f
                    : macroState.Call == AIGameModeMacroCall.Push
                        ? 48f
                        : 60f;
                score += ballDelta;
                _lastObjectiveScoreReason += $"|ball_focus_+{ballDelta:0.0}";
            }

            float weightDelta = Mathf.Clamp((objective.Weight - 50f) * 0.15f, -10f, 18f);
            if (Mathf.Abs(weightDelta) > 0.01f)
            {
                score += weightDelta;
                _lastObjectiveScoreReason += $"|weight_{weightDelta:+0.0;-0.0}";
            }

            float controlDelta = AIObjectiveControlUtility.GetUtilityScoreDelta(
                objective.ControlState);
            if (Mathf.Abs(controlDelta) > 0.01f)
            {
                score += controlDelta;
                _lastObjectiveScoreReason +=
                    $"|control_{objective.ControlState}_{controlDelta:+0.0;-0.0}";
            }

            float presenceDelta = AIObjectiveControlUtility.GetUtilityPresenceDelta(
                objective.FriendlyPresence,
                objective.EnemyPresence);
            if (Mathf.Abs(presenceDelta) > 0.01f)
            {
                score += presenceDelta;
                _lastObjectiveScoreReason +=
                    $"|presence_{objective.FriendlyPresence}:{objective.EnemyPresence}_{presenceDelta:+0.0;-0.0}";
            }

            // Far from objective: moving toward it is useful.
            float farDistance = Mathf.Max(4f, objectiveRadius + 1.5f);
            if (dist > farDistance)
            {
                score += 20f;
                _lastObjectiveScoreReason += "|far_+20";
            }

            // Already near objective: no need to over-prioritize objective movement.
            float nearDistance = Mathf.Max(1.5f, objectiveRadius * 0.75f);
            if (dist < nearDistance)
            {
                score -= 20f;
                _lastObjectiveScoreReason += "|near_-20";
            }

            // Decision-layer anti-clumping:
            // If allies are already near the objective, reduce desire to also go there.
            float allyPressure = CalculateNearbyAllyPressure(
                objectivePosition,
                Mathf.Max(4.5f, objectiveRadius + 1.5f));

            float crowdingPenalty = allyPressure * GetObjectiveCrowdingPenalty();
            if (isBrawlBallObjective)
                crowdingPenalty *= 0.35f;

            _lastObjectiveAllyPressure = allyPressure;
            _lastObjectiveCrowdingPenalty = crowdingPenalty;

            score -= crowdingPenalty;

            if (crowdingPenalty > 0.01f)
                _lastObjectiveScoreReason += $"|crowding_-{crowdingPenalty:0.0}";

            float beforeArchetype = score;

            // Archetype shaping.
            // Tanks/controllers/artillery like objective pressure more.
            // Assassins prefer side pressure instead of sitting center.
            score = AddArchetypeBias(
                score,
                sniper: -5f,
                tank: 10f,
                assassin: -8f,
                support: 0f,
                fighter: 2f,
                controller: 8f,
                artillery: 5f);

            float archetypeDelta = score - beforeArchetype;
            if (Mathf.Abs(archetypeDelta) > 0.01f)
                _lastObjectiveScoreReason += $"|archetype_{archetypeDelta:+0.0;-0.0}";

            // Gem Grab: if behind, encourage objective/map pressure slightly.
            if (GemGrabMode.Instance != null && GemGrabMode.Instance.IsTeamBehind(_self.Team))
            {
                score += 8f;
                _lastObjectiveScoreReason += "|behind_+8";
            }

            float objectiveMacroDelta = GetMacroActionDelta(
                AIActionType.Objective,
                macroState,
                _self.State != null ? _self.State.CarriedGemCount : 0,
                0,
                0,
                out string objectiveMacroReason);
            if (Mathf.Abs(objectiveMacroDelta) > 0.01f)
            {
                score += objectiveMacroDelta;
                _lastObjectiveScoreReason += $"|{objectiveMacroReason}_{objectiveMacroDelta:+0.0;-0.0}";
            }

            TryResolveGemPickupDecision(
                macroState,
                currentTick,
                out AIGemPickupDecision objectiveGemDecision);
            AIGemMineControlEvaluation objectiveMineControl =
                AIGemGrabObjectiveUtility.EvaluateMineControl(
                    AIActionType.Objective,
                    BuildGemMineControlContext(
                        macroState,
                        hasLiveTarget: false,
                        allyPressure,
                        objectiveGemDecision));
            if (objectiveMineControl.HasDelta)
            {
                score += objectiveMineControl.Delta;
                _lastObjectiveScoreReason +=
                    $"|mine_{objectiveMineControl.Reason}_{objectiveMineControl.Delta:+0.0;-0.0}";
            }

            float opponentObjectiveNeglect = AIOpponentModel.GetMaxObjectiveNeglect(
                _self.Team,
                currentTick,
                360u);

            if (opponentObjectiveNeglect > 0.20f)
            {
                float neglectBonus = opponentObjectiveNeglect * 14f;
                score += neglectBonus;
                _lastObjectiveScoreReason += $"|opp_neglect_+{neglectBonus:0.0}";
            }

            float laneBonus = GetLaneDisciplineScoreBonus(
                currentTick,
                _profile.LaneHoldObjectiveBonus,
                out string laneReason);
            if (laneBonus > 0.01f)
            {
                score += laneBonus;
                _lastObjectiveScoreReason += $"|lane_{laneBonus:+0.0;-0.0}_{laneReason}";
            }

            _lastObjectiveRawScore = score;

            AIActionScore finalScore = MakeScore(
                AIActionType.Objective,
                score,
                _profile.ObjectiveWeight);

            _lastObjectiveFinalScore = finalScore.Score;

            return finalScore;
        }

        private AIGemMineControlContext BuildGemMineControlContext(
            AIGameModeMacroState macroState,
            bool hasLiveTarget,
            float allyPressure,
            AIGemPickupDecision gemDecision)
        {
            float healthRatio = _self != null && _self.State != null
                ? _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value)
                : 1f;

            return new AIGemMineControlContext(
                macroState,
                _self != null && _self.State != null
                    ? _self.State.CarriedGemCount
                    : 0,
                healthRatio,
                allyPressure,
                hasLiveTarget,
                gemDecision.HasPickup,
                gemDecision.ShouldPickup,
                gemDecision.Score);
        }

        private AIActionScore ScoreRegroup(
     AITargetInfo targetInfo,
     uint currentTick,
     AIGameModeMacroState macroState)
        {
            if (_teamCoordinator == null)
                return new AIActionScore(AIActionType.Regroup, 0f);

            // Never regroup during active combat.
            if (targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.Regroup, 0f);

            if (!_teamCoordinator.TryGetRegroupPoint(currentTick, out _))
                return new AIActionScore(AIActionType.Regroup, 0f);

            float score = 45f;

            float teamplay = _self.Definition != null
                ? _self.Definition.TeamplayWeight
                : 1f;

            score *= teamplay;

            score = AddArchetypeBias(
                score,
                sniper: 8f,
                tank: -8f,
                assassin: -12f,
                support: 10f,
                fighter: 0f,
                controller: 6f,
                artillery: 8f);

            if (_self.State != null)
            {
                float healthRatio = _self.State.CurrentHealth /
                                    Mathf.Max(1f, _self.State.MaxHealth.Value);

                if (healthRatio <= _profile.RegroupHealthThreshold)
                    score += 20f;

                // Gem carriers should regroup more safely.
                score += 5f * _self.State.CarriedGemCount;

                score += GetMacroActionDelta(
                    AIActionType.Regroup,
                    macroState,
                    _self.State.CarriedGemCount,
                    0,
                    0);
            }

            return MakeScore(
                AIActionType.Regroup,
                score,
                _profile.RegroupWeight);
        }


        private AIActionScore ScorePeel(
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (_teamCoordinator == null ||
                !_teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) ||
                ally == null)
            {
                return new AIActionScore(AIActionType.Peel, 0f);
            }

            float score = 40f;

            int allyCarriedGems = ally.State != null
                ? ally.State.CarriedGemCount
                : 0;

            if (allyCarriedGems > 0)
                score += 8f * allyCarriedGems;

            score += GetMacroActionDelta(
                AIActionType.Peel,
                macroState,
                0,
                0,
                allyCarriedGems);

            float teamplay = _self.Definition != null ? _self.Definition.TeamplayWeight : 1f;
            score *= teamplay;

            if (IsSupport) score += 20f;
            if (IsTank) score += 12f;
            if (IsAssassin) score -= 8f;
            if (IsController) score += 15f;
            if (IsArtillery) score += 10f;

            return MakeScore(
                AIActionType.Peel,
                score,
                _profile.PeelWeight,
                allowEmergencyScore: true);
        }

        private float GetAbilityIdealRange()
        {
            var attack = _self.State != null
                ? _self.State.GetCurrentMainAttackDefinition()
                : _self.Definition?.MainAttack;
            return attack != null ? attack.GetAIIdealRange() : 6f;
        }

        private float GetAbilityMaxRange()
        {
            var attack = _self.State != null
                ? _self.State.GetCurrentMainAttackDefinition()
                : _self.Definition?.MainAttack;
            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private float GetReactiveDamagePressure(uint currentTick)
        {
            return _reactiveMemory != null
                ? _reactiveMemory.GetDamagePressure(currentTick, _profile.ReactiveDamageMemoryTicks)
                : 0f;
        }

        private float GetAmmoPressure()
        {
            return AICombatMicroUtility.GetAmmoPressure(
                GetAvailableAmmo(),
                GetMaxAmmo(),
                GetCurrentAmmo());
        }

        private int GetAvailableAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.AvailableBars
                : 3;
        }

        private int GetMaxAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.MaxAmmo
                : 3;
        }

        private float GetCurrentAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.CurrentAmmo
                : GetAvailableAmmo();
        }

        private float CalculateNearbyAllyPressure(Vector3 position, float radius)
        {
            if (SimulationClock.Grid == null)
                return 0f;

            _nearbyAllyBuffer.Clear();

            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                position,
                radius,
                _nearbyAllyBuffer);

            float pressure = 0f;

            for (int i = 0; i < _nearbyAllyBuffer.Count; i++)
            {
                ISpatialEntity entity = _nearbyAllyBuffer[i];

                if (!SpatialEntityUtility.IsAlive(entity))
                    continue;

                if (entity == _self)
                    continue;

                if (entity.Team != _self.Team)
                    continue;

                if (!(entity is BrawlerController other))
                    continue;

                if (other.State == null || other.State.IsDead)
                    continue;

                float dist = Vector3.Distance(position, other.Position);
                float closeness = 1f - Mathf.Clamp01(dist / radius);

                pressure += closeness;
            }

            return pressure;
        }

        private bool CanHoldAssignedLane(uint currentTick, out string reason)
        {
            if (_hasLaneEvaluation && _lastLaneEvaluationTick == currentTick)
            {
                reason = _lastLaneHoldReason;
                return _lastCanHoldLane;
            }

            _hasLaneEvaluation = true;
            _lastLaneEvaluationTick = currentTick;
            _lastCanHoldLane = false;
            _lastLaneHoldReason = "lane_disabled";

            if (_profile == null || !_profile.UseLaneDiscipline)
            {
                reason = _lastLaneHoldReason;
                return false;
            }

            Vector3 anchorPoint = Vector3.zero;
            bool hasAnchor = false;
            AITeamLaneAssignment lane = AILaneDisciplineUtility.ResolveAssignedLane(_self.EntityID);

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetLaneOwnership(
                    currentTick,
                    out AITeamLaneOwnershipSnapshot laneOwnership) &&
                laneOwnership.HasRecommendedLane)
            {
                lane = laneOwnership.RecommendedLane;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetPlaybookState(currentTick, out AITeamPlaybookState playbookState))
            {
                if (playbookState.Lane != AITeamLaneAssignment.None)
                    lane = playbookState.Lane;

                if (playbookState.HasAnchorPoint)
                {
                    anchorPoint = playbookState.AnchorPoint;
                    hasAnchor = true;
                }
                else if (playbookState.HasPressurePoint)
                {
                    anchorPoint = playbookState.PressurePoint;
                    hasAnchor = true;
                }
            }

            if (!hasAnchor &&
                _objectiveMemory != null &&
                _objectiveMemory.TryGetBestObjective(
                    _self.Position,
                    _profile.PreferredObjective,
                    _self.Team,
                    out AIObjectiveCandidate objective))
            {
                anchorPoint = objective.Position;
                hasAnchor = true;
            }

            if (!hasAnchor)
            {
                _lastLaneHoldReason = "lane_no_anchor";
                reason = _lastLaneHoldReason;
                return false;
            }

            _lastCanHoldLane = AILaneDisciplineUtility.TryResolveLaneHoldPoint(
                _self,
                _profile,
                lane,
                anchorPoint,
                out _,
                out _lastLaneHoldReason);

            reason = _lastLaneHoldReason;
            return _lastCanHoldLane;
        }

        private float GetLaneDisciplineScoreBonus(
            uint currentTick,
            float baseBonus,
            out string reason)
        {
            reason = "lane_none";

            if (!CanHoldAssignedLane(currentTick, out string laneReason))
            {
                reason = laneReason;
                return 0f;
            }

            float laneWeight = Mathf.Max(0f, _profile.LaneDisciplineWeight);
            if (laneWeight <= 0f || baseBonus <= 0f)
            {
                reason = laneReason;
                return 0f;
            }

            float multiplier = 1f;
            string stateReason = "hold";

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetLaneOwnership(
                    currentTick,
                    out AITeamLaneOwnershipSnapshot laneOwnership))
            {
                if (laneOwnership.AssignedLaneAbandoned)
                {
                    multiplier += 0.90f;
                    stateReason = "recover";
                }
                else if (laneOwnership.ShouldRotate)
                {
                    multiplier += 1.10f;
                    stateReason = "rotate";
                }
                else if (laneOwnership.RotationPending)
                {
                    multiplier += 0.35f;
                    stateReason = "pending";
                }
                else if (laneOwnership.CurrentLaneOverOwned)
                {
                    multiplier *= 0.70f;
                    stateReason = "overowned_hold";
                }
            }

            reason = $"{stateReason}_{laneReason}";
            return baseBonus * laneWeight * multiplier;
        }

        private CloseRangeMatchupEvaluation EvaluateCloseRangeMatchup(
            AITargetInfo targetInfo,
            float distance,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (_profile == null ||
                targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null ||
                !IsCloseRangePressureRole())
            {
                return CloseRangeMatchupEvaluation.Inactive;
            }

            float ownRange = Mathf.Max(1f, GetAbilityMaxRange());
            float targetRange = Mathf.Max(1f, GetTargetAbilityMaxRange(targetBrawler));
            float rangeGap = targetRange - ownRange;
            float meaningfulGap = Mathf.Max(0.85f, ownRange * 0.30f);

            if (rangeGap <= meaningfulGap)
                return CloseRangeMatchupEvaluation.Inactive;

            float catchDistance = Mathf.Max(
                ownRange + _profile.AttackRangeBuffer,
                ownRange * Mathf.Max(1.25f, _profile.CloseRangeCatchDistanceMultiplier));

            float chaseThreshold = Mathf.Clamp(
                _profile.LowHealthChaseHealthThreshold > 0f
                    ? _profile.LowHealthChaseHealthThreshold
                    : _profile.FinisherHealthThreshold,
                0.05f,
                0.85f);
            float targetHealthRatio = targetBrawler.State.CurrentHealth /
                                      Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
            int targetCarriedGems = targetBrawler.State.CarriedGemCount;
            bool targetLow = targetHealthRatio <= chaseThreshold;
            bool objectiveTarget = targetCarriedGems > 0 ||
                                   IsModeCriticalTarget(targetBrawler, macroState);
            bool superReady = _self.State != null &&
                              _self.State.SuperCharge.IsReady;
            bool teamCollapse = HasTeamCollapseOnTarget(targetBrawler, currentTick);
            bool catchable =
                distance <= catchDistance ||
                targetLow ||
                objectiveTarget ||
                superReady ||
                teamCollapse;

            float overReach = Mathf.Max(0f, distance - catchDistance);

            if (catchable)
            {
                float engageBonus = Mathf.Max(0f, _profile.CloseRangeEvasivePressureBonus);

                if (targetLow)
                    engageBonus += _profile.ChaseCommitScoreBonus * 0.55f;

                if (objectiveTarget)
                    engageBonus += Mathf.Min(18f, 6f + targetCarriedGems * 4f);

                if (superReady)
                    engageBonus += 8f;

                if (teamCollapse)
                    engageBonus += 8f;

                float repositionBonus =
                    Mathf.Max(0f, _profile.CloseRangeEvasivePressureBonus) *
                    (distance > ownRange + _profile.AttackRangeBuffer ? 0.75f : 0.35f);

                return new CloseRangeMatchupEvaluation(
                    true,
                    engageBonus,
                    repositionBonus,
                    $"engage rangeGap={rangeGap:0.0} dist={distance:0.0} catch={catchDistance:0.0}");
            }

            float penalty =
                Mathf.Max(0f, _profile.CloseRangeOutrangedChasePenalty) +
                overReach * 5f +
                rangeGap * 2.5f;

            if (macroState.Call == AIGameModeMacroCall.Hold ||
                macroState.Call == AIGameModeMacroCall.Reset ||
                macroState.OwnTeamHasCountdown)
            {
                penalty += 10f;
            }

            if (_self.State != null)
            {
                float healthRatio = _self.State.CurrentHealth /
                                    Mathf.Max(1f, _self.State.MaxHealth.Value);
                if (healthRatio <= _profile.LowHealthRetreatRatio + 0.20f)
                    penalty += 12f;
            }

            float coverBonus =
                Mathf.Max(0f, _profile.CloseRangeCoverRepositionBonus) +
                Mathf.Min(16f, overReach * 4f);

            return new CloseRangeMatchupEvaluation(
                true,
                -penalty,
                coverBonus,
                $"cover_outmatched rangeGap={rangeGap:0.0} dist={distance:0.0} catch={catchDistance:0.0}");
        }

        private bool IsCloseRangePressureRole()
        {
            if (IsTank || IsAssassin)
                return true;

            return IsFighter && GetAbilityMaxRange() <= 4.75f;
        }

        private static float GetTargetAbilityMaxRange(BrawlerController target)
        {
            if (target == null)
                return 6f;

            AbilityDefinition attack = target.State != null
                ? target.State.GetCurrentMainAttackDefinition()
                : target.Definition?.MainAttack;

            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private bool IsModeCriticalTarget(
            BrawlerController target,
            AIGameModeMacroState macroState)
        {
            if (target == null)
                return false;

            if (macroState.Mode == GameModeId.BrawlBall)
            {
                BrawlBallMode mode = BrawlBallMode.Instance;
                return mode != null &&
                       SpatialEntityUtility.IsSameEntity(mode.BallCarrier, target);
            }

            return macroState.Mode == GameModeId.Knockout &&
                   macroState.Call == AIGameModeMacroCall.Push &&
                   macroState.Phase == AIGameModeObjectivePhase.FinalPressure;
        }

        private bool HasTeamCollapseOnTarget(BrawlerController target, uint currentTick)
        {
            if (_teamCoordinator == null || target == null)
                return false;

            return _teamCoordinator.TryGetFocusDirective(
                       currentTick,
                       out BrawlerController focusTarget,
                       out float urgency,
                       out _) &&
                   urgency >= 1.35f &&
                   SpatialEntityUtility.IsSameEntity(focusTarget, target);
        }

        private float GetChaseDisciplineDelta(
            AITargetInfo targetInfo,
            float distance,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null ||
                _profile == null)
            {
                _chaseDisengageMemory.Reset();
                _lastChaseDebug = "Chase=None Reason=no_target";
                return 0f;
            }

            float threshold = Mathf.Clamp(
                _profile.LowHealthChaseHealthThreshold > 0f
                    ? _profile.LowHealthChaseHealthThreshold
                    : _profile.FinisherHealthThreshold,
                0.05f,
                0.85f);
            float targetHealthRatio = targetBrawler.State.CurrentHealth /
                                      Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
            float maxChaseDistance = Mathf.Max(1f, _profile.LowHealthChaseMaxDistance);
            float laneWeight = Mathf.Max(0f, _profile.LaneDisciplineWeight);
            bool targetIsLow = targetHealthRatio <= threshold;
            int targetCarriedGems = targetBrawler.State.CarriedGemCount;
            int selfCarriedGems = _self.State != null
                ? _self.State.CarriedGemCount
                : 0;
            bool preserveLaneShape = ShouldPreserveLaneShape(
                macroState,
                targetCarriedGems,
                currentTick);
            bool badChaseMapPosition = IsBadChaseMapPosition(targetBrawler.Position);
            bool secureFinisher = AIChaseDisengageMemory.IsSecureFinisher(
                targetHealthRatio,
                threshold,
                distance,
                maxChaseDistance,
                selfCarriedGems,
                badChaseMapPosition);
            AIChaseDisengageDecision chaseDecision =
                _chaseDisengageMemory.Evaluate(
                    new AIChaseDisengageContext
                    {
                        TargetEntityId = targetBrawler.EntityID,
                        Tick = currentTick,
                        Distance = distance,
                        TargetHealthRatio = targetHealthRatio,
                        ChaseHealthThreshold = threshold,
                        MaxChaseDistance = maxChaseDistance,
                        SelfCarriedGems = selfCarriedGems,
                        TargetCarriedGems = targetCarriedGems,
                        PreserveLaneShape = preserveLaneShape,
                        TargetInBadMapPosition = badChaseMapPosition,
                        CommitTicks = _profile.LowHealthChaseCommitTicks,
                        MaxTicks = _profile.LowHealthChaseMaxTicks,
                        CooldownTicks = _profile.LowHealthChaseCooldownTicks,
                        BreakDistanceMultiplier =
                            _profile.LowHealthChaseBreakDistanceMultiplier,
                        CommitScoreBonus = _profile.ChaseCommitScoreBonus,
                        DisengageScorePenalty =
                            _profile.ChaseDisengageScorePenalty,
                        BadMapPenalty = _profile.BadMapChasePenalty
                    });
            _lastChaseDebug = chaseDecision.GetDebugSummary();

            if (targetIsLow)
            {
                float securePressure = Mathf.Clamp01((threshold - targetHealthRatio) / threshold);
                float delta = _profile.LowHealthChaseApproachBonus * (0.55f + securePressure);

                if (secureFinisher)
                    delta += _profile.ChaseCommitScoreBonus * 0.75f;

                if (targetCarriedGems > 0)
                    delta += targetCarriedGems * 4f;

                if (distance > maxChaseDistance && !secureFinisher)
                {
                    float overDistance = distance - maxChaseDistance;
                    delta -= overDistance * Mathf.Max(0f, _profile.UnsafeChasePenalty) * 0.35f;
                }

                if (preserveLaneShape &&
                    distance > maxChaseDistance * 0.70f &&
                    !secureFinisher)
                {
                    float valuableTargetDiscount = targetCarriedGems > 0 ? 0.55f : 1f;
                    delta -= _profile.UnsafeChasePenalty * valuableTargetDiscount * laneWeight;
                }

                if (badChaseMapPosition &&
                    preserveLaneShape &&
                    targetCarriedGems <= 0 &&
                    !secureFinisher)
                {
                    delta -= _profile.BadMapChasePenalty;
                }

                delta += chaseDecision.ScoreDelta;
                if (chaseDecision.ShouldDisengage)
                {
                    return Mathf.Min(
                        delta,
                        -_profile.ChaseDisengageScorePenalty * 0.55f);
                }

                return Mathf.Clamp(
                    delta,
                    -Mathf.Max(
                        _profile.UnsafeChasePenalty,
                        _profile.ChaseDisengageScorePenalty) * 1.5f,
                    _profile.LowHealthChaseApproachBonus * 1.8f +
                    _profile.ChaseCommitScoreBonus +
                    targetCarriedGems * 4f);
            }

            if (targetCarriedGems > 0)
            {
                float pickupValuePressure = targetCarriedGems * 5f;
                float distancePressure = Mathf.Clamp01(
                    1f - distance / Mathf.Max(1f, maxChaseDistance * 1.25f));
                float delta = pickupValuePressure * (0.65f + distancePressure * 0.55f);

                if (badChaseMapPosition)
                    delta -= _profile.BadMapChasePenalty * 0.35f;

                delta += chaseDecision.ScoreDelta;

                return Mathf.Clamp(
                    delta,
                    -_profile.ChaseDisengageScorePenalty,
                    _profile.LowHealthChaseApproachBonus +
                    _profile.ChaseCommitScoreBonus +
                    targetCarriedGems * 6f);
            }

            if (_profile.UseLaneDiscipline &&
                preserveLaneShape &&
                distance > maxChaseDistance)
            {
                float overDistance = distance - maxChaseDistance;
                float lanePenalty = -Mathf.Min(
                    _profile.UnsafeChasePenalty * laneWeight,
                    overDistance * 6f * laneWeight);

                return lanePenalty + Mathf.Min(0f, chaseDecision.ScoreDelta);
            }

            return Mathf.Min(0f, chaseDecision.ScoreDelta);
        }

        private bool ShouldPreserveLaneShape(
            AIGameModeMacroState macroState,
            int targetCarriedGems,
            uint currentTick)
        {
            if (_self.State == null)
                return false;

            float healthRatio = _self.State.CurrentHealth /
                                Mathf.Max(1f, _self.State.MaxHealth.Value);

            if (healthRatio <= _profile.LowHealthRetreatRatio + 0.15f)
                return true;

            if (_self.State.CarriedGemCount > 0)
                return true;

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetLaneOwnership(
                    currentTick,
                    out AITeamLaneOwnershipSnapshot laneOwnership) &&
                (laneOwnership.ShouldRotate ||
                 laneOwnership.AssignedLaneAbandoned ||
                 laneOwnership.CurrentLaneOverOwned))
            {
                return targetCarriedGems <= 0;
            }

            if (macroState.Call == AIGameModeMacroCall.Reset ||
                macroState.Call == AIGameModeMacroCall.Hold ||
                macroState.OwnTeamHasCountdown)
            {
                return targetCarriedGems <= 0;
            }

            return IsBacklineRole() && targetCarriedGems <= 0;
        }

        private bool IsBadChaseMapPosition(Vector3 targetPosition)
        {
            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
                return false;

            Vector2Int targetCoords = pathfinder.GetGridCoords(targetPosition);
            if (!pathfinder.IsWalkable(targetCoords))
                return true;

            AIMapSemanticCell semantic = pathfinder.GetSemanticCell(targetCoords);
            if (semantic.HasTag(AIMapSemanticTag.DangerCorridor))
                return true;

            if (semantic.HasTag(AIMapSemanticTag.Choke) &&
                !IsTank &&
                !IsAssassin)
            {
                return true;
            }

            int walkableNeighbors = pathfinder.CountWalkableNeighbors(targetCoords);
            return walkableNeighbors <= 2 && !IsTank;
        }

        private readonly struct CloseRangeMatchupEvaluation
        {
            public static readonly CloseRangeMatchupEvaluation Inactive =
                new CloseRangeMatchupEvaluation(false, 0f, 0f, "inactive");

            public readonly bool IsActive;
            public readonly float ApproachDelta;
            public readonly float RepositionBonus;
            public readonly string Reason;

            public CloseRangeMatchupEvaluation(
                bool isActive,
                float approachDelta,
                float repositionBonus,
                string reason)
            {
                IsActive = isActive;
                ApproachDelta = approachDelta;
                RepositionBonus = repositionBonus;
                Reason = reason;
            }
        }

        private float GetObjectiveCrowdingPenalty()
        {
            if (IsTank)
                return 5f;

            if (IsFighter)
                return 7f;

            if (IsController)
                return 8f;

            if (IsSupport)
                return 10f;

            if (IsSniper)
                return 12f;

            if (IsArtillery)
                return 12f;

            if (IsAssassin)
                return 14f;

            return 10f;
        }
    }
}
