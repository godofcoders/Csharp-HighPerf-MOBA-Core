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

        public float LastObjectiveAllyPressure => _lastObjectiveAllyPressure;
        public float LastObjectiveCrowdingPenalty => _lastObjectiveCrowdingPenalty;
        public float LastObjectiveRawScore => _lastObjectiveRawScore;
        public float LastObjectiveFinalScore => _lastObjectiveFinalScore;
        public string LastObjectiveScoreReason => _lastObjectiveScoreReason;
        public string LastTeamRoleDebug => _lastTeamRoleDebug;
        public string LastMacroDebug => _lastMacroDebug;
        public string LastPlaybookDebug => _lastPlaybookDebug;

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
            results.Add(ScoreReposition(targetInfo, currentTick));
            results.Add(ScoreApproach(targetInfo, currentTick, macroState));
            results.Add(ScorePeel(currentTick, macroState));
            results.Add(ScoreRegroup(targetInfo, currentTick, macroState));
            results.Add(ScoreSearch(targetInfo, currentTick, macroState));
            results.Add(ScoreWander());
            results.Add(ScoreObjective(targetInfo, currentTick, macroState));

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

            _lastPlaybookDebug = state.GetDebugSummary();
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
                    return GetEscortCarrierPlaybookDelta(actionType, playbookState.Lane, selfCarrier);

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
            float healthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);

            if (healthRatio <= _profile.LowHealthRetreatRatio)
                score += 70f;

            if (_self.State.HasStatus(StatusEffectType.Burn))
                score += 25f;

            if (_self.State.HasStatus(StatusEffectType.Stun))
                score -= 1000f;

            if (targetInfo.HasLiveTarget)
            {
                float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);
                if (dist <= _profile.GetTooCloseDistance(GetAbilityIdealRange()))
                    score += 20f;
            }

            float reactivePressure = GetReactiveDamagePressure(currentTick);
            if (reactivePressure > 0f)
            {
                score += reactivePressure * _profile.ReactiveRetreatPressureBonus;

                if (healthRatio <= _profile.ReactiveEmergencyHealthRatio)
                    score += reactivePressure * 30f;
            }

            float roleSurvival = _self.Definition != null ? _self.Definition.SurvivalInstinct : 1f;
            score *= roleSurvival;

            if (IsSniper) score += 10f;
            if (IsSupport) score += 8f;
            if (IsTank) score -= 10f;
            if (IsAssassin) score -= 6f;
            if (IsController) score += 5f;
            if (IsArtillery) score += 12f;

            // Gem Grab: every gem you carry makes retreat MORE attractive.
            // Dying with gems hands them to the enemy. +6 per gem is enough
            // that a 3-gem brawler gets a noticeable shift but a 1-gem
            // brawler isn't yanked off objectives.
            if (_self.State != null)
                score += 6f * _self.State.CarriedGemCount;

            score += GetMacroActionDelta(
                AIActionType.Retreat,
                macroState,
                _self.State.CarriedGemCount,
                0,
                0);

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

        private AIActionScore ScoreReposition(AITargetInfo targetInfo, uint currentTick)
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
                return new AIActionScore(AIActionType.Approach, 0f);

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

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetFocusTarget(currentTick, out var focusTarget) &&
                SpatialEntityUtility.IsAlive(focusTarget) &&
                targetInfo.Target.EntityID == focusTarget.EntityID)
            {
                score += _profile.FocusFireWeight;
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
                score += _profile.ReactiveAttackerFocusBonus * reactivePressure;
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

            if (Gem.HasAnyUnpickedWithin(_self.Position, 8f))
                score += 35f;

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

            // Combat always overrides objective movement.
            // Objective is a map-control fallback, not a replacement for fighting.
            if (targetInfo.HasLiveTarget)
            {
                _lastObjectiveScoreReason = "combat_target_exists";
                return new AIActionScore(AIActionType.Objective, 0f);
            }

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

            Vector3 objectivePosition = objective.Position;

            float dist = Vector3.Distance(
                _self.Position,
                objectivePosition);

            float score = 45f;
            _lastObjectiveScoreReason = "base_45";

            // Far from objective: moving toward it is useful.
            if (dist > 4f)
            {
                score += 20f;
                _lastObjectiveScoreReason += "|far_+20";
            }

            // Already near objective: no need to over-prioritize objective movement.
            if (dist < 2.5f)
            {
                score -= 20f;
                _lastObjectiveScoreReason += "|near_-20";
            }

            // Decision-layer anti-clumping:
            // If allies are already near the objective, reduce desire to also go there.
            float allyPressure = CalculateNearbyAllyPressure(
                objectivePosition,
                4.5f);

            float crowdingPenalty = allyPressure * GetObjectiveCrowdingPenalty();

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

            _lastObjectiveRawScore = score;

            AIActionScore finalScore = MakeScore(
                AIActionType.Objective,
                score,
                _profile.ObjectiveWeight);

            _lastObjectiveFinalScore = finalScore.Score;

            return finalScore;
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
            float score = 0f;

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) &&
                ally != null)
            {
                score += 40f;

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
            }

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
