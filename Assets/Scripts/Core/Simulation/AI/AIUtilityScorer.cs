using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIUtilityScorer
    {
        private readonly BrawlerController _self;
        private readonly BrawlerAIProfile _profile;
        private readonly AIObjectiveMemory _objectiveMemory;

        private readonly AITeamCoordinator _teamCoordinator;
        public AIUtilityScorer(BrawlerController self, BrawlerAIProfile profile, AIObjectiveMemory objectiveMemory, AITeamCoordinator teamCoordinator)
        {
            _self = self;
            _profile = profile;
            _objectiveMemory = objectiveMemory;
            _teamCoordinator = teamCoordinator;
        }

        public AIActionScore ScoreBestAction(AITargetInfo targetInfo, uint currentTick)
        {
            AIActionScore best = new AIActionScore(AIActionType.Wander, 0f);

            ScoreAndReplace(ref best, ScoreRetreat(targetInfo));
            ScoreAndReplace(ref best, ScoreUseSuper(targetInfo));
            ScoreAndReplace(ref best, ScoreHoldRange(targetInfo));
            ScoreAndReplace(ref best, ScoreReposition(targetInfo));
            ScoreAndReplace(ref best, ScoreApproach(targetInfo));
            ScoreAndReplace(ref best, ScorePeel(currentTick));
            ScoreAndReplace(ref best, ScoreRegroup(currentTick));
            ScoreAndReplace(ref best, ScoreSearch(targetInfo, currentTick));
            ScoreAndReplace(ref best, ScoreWander());
            ScoreAndReplace(ref best, ScoreObjective());

            return best;
        }

        private void ScoreAndReplace(ref AIActionScore best, AIActionScore candidate)
        {
            if (candidate.Score > best.Score)
                best = candidate;
        }

        private AIActionScore ScoreRetreat(AITargetInfo targetInfo)
        {
            if (_self == null || _self.State == null)
                return new AIActionScore(AIActionType.Retreat, 0f);

            float score = 0f;
            float healthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);

            if (healthRatio <= _profile.LowHealthRetreatRatio)
                score += 70f;

            if (_self.State.HasStatus(StatusEffectType.Burn))
                score += 25f;

            if (_self.State.HasStatus(StatusEffectType.Stun))
                score -= 1000f; // cannot actively retreat while stunned

            if (targetInfo.HasLiveTarget)
            {
                float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);
                if (dist <= _profile.GetTooCloseDistance(GetAbilityIdealRange()))
                    score += 20f;
            }

            return new AIActionScore(AIActionType.Retreat, score * _profile.RetreatWeight);
        }

        private AIActionScore ScoreUseSuper(AITargetInfo targetInfo)
        {
            if (_self == null || _self.State == null || !_self.State.SuperCharge.IsReady || !targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.UseSuper, 0f);

            float score = 0f;
            AbilityDefinition super = _self.Definition?.SuperAbility;
            if (super == null)
                return new AIActionScore(AIActionType.UseSuper, 0f);

            if (targetInfo.Target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                float targetHealthRatio = targetBrawler.State.CurrentHealth /
                                          Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);

                if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                    score += 40f;

                if (targetBrawler.State.HasStatus(StatusEffectType.Slow))
                    score += 20f;

                if (AIAbilityIntentUtility.IsFinisher(super) &&
                    targetHealthRatio <= _profile.SuperLowHealthTargetThreshold)
                {
                    score += 50f;
                }

                if (AIAbilityIntentUtility.IsEngage(super))
                    score += 30f;

                if (AIAbilityIntentUtility.IsEscape(super))
                {
                    float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
                    if (selfHealthRatio <= _profile.LowHealthRetreatRatio)
                        score += 45f;
                }
            }

            return new AIActionScore(AIActionType.UseSuper, score * _profile.SuperWeight);
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

            return new AIActionScore(AIActionType.HoldRange, score * _profile.HoldRangeWeight);
        }

        private AIActionScore ScoreReposition(AITargetInfo targetInfo)
        {
            if (!targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.Reposition, 0f);

            float idealRange = GetAbilityIdealRange();
            float tooClose = _profile.GetTooCloseDistance(idealRange);
            float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);

            float score = 0f;
            if (dist < tooClose)
                score += 60f;

            return new AIActionScore(AIActionType.Reposition, score * _profile.RepositionWeight);
        }

        private AIActionScore ScoreApproach(AITargetInfo targetInfo)
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
    _teamCoordinator.TryGetFocusTarget(ServiceProvider.Get<ISimulationClock>().CurrentTick, out var focusTarget) &&
    focusTarget != null &&
    targetInfo.HasLiveTarget &&
    ReferenceEquals(targetInfo.Target, focusTarget))
            {
                score += _profile.FocusFireWeight;
            }

            return new AIActionScore(AIActionType.Approach, score * _profile.ApproachWeight);
        }

        private AIActionScore ScoreSearch(AITargetInfo targetInfo, uint currentTick)
        {
            float score = 0f;

            if (targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
                score += 30f;

            if (AITeamMemory.TryGetRecentHotspot(_self.Team, currentTick, _profile.SharedHotspotMemoryTicks, out _))
                score += 20f;

            return new AIActionScore(AIActionType.Search, score * _profile.SearchWeight);
        }

        private AIActionScore ScoreWander()
        {
            return new AIActionScore(AIActionType.Wander, 5f * _profile.WanderWeight);
        }

        private float GetAbilityIdealRange()
        {
            var attack = _self.Definition?.MainAttack;
            return attack != null ? attack.GetAIIdealRange() : 6f;
        }

        private float GetAbilityMaxRange()
        {
            var attack = _self.Definition?.MainAttack;
            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private AIActionScore ScoreObjective()
        {
            if (_objectiveMemory == null)
                return new AIActionScore(AIActionType.Wander, 0f);

            var objective = _objectiveMemory.GetBestObjective(_self.Position, _profile.PreferredObjective);
            if (objective == null)
                return new AIActionScore(AIActionType.Wander, 0f);

            float score = _profile.ObjectiveWeight;
            return new AIActionScore(AIActionType.Search, score);
        }

        private AIActionScore ScoreRegroup(uint currentTick)
        {
            if (_teamCoordinator == null || _self == null || _self.State == null)
                return new AIActionScore(AIActionType.Regroup, 0f);

            float healthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
            float score = 0f;

            if (healthRatio <= _profile.RegroupHealthThreshold)
                score += 40f;

            if (_teamCoordinator.TryGetRegroupPoint(currentTick, out _))
                score += 20f;

            return new AIActionScore(AIActionType.Regroup, score * _profile.RegroupWeight);
        }

        private AIActionScore ScorePeel(uint currentTick)
        {
            if (_teamCoordinator == null)
                return new AIActionScore(AIActionType.Peel, 0f);

            float score = 0f;

            if (_teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) && ally != null)
            {
                float dist = Vector3.Distance(_self.Position, ally.Position);
                if (dist <= _profile.AllySupportRange)
                {
                    score += 35f;
                }
            }

            return new AIActionScore(AIActionType.Peel, score * _profile.PeelWeight);
        }
    }
}