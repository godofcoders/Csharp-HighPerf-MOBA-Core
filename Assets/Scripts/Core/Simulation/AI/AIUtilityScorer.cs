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

        private readonly uint _threatForgetTicks = 240;

        private bool IsSniper => _profile.Archetype == AIArchetype.Sniper;
        private bool IsTank => _profile.Archetype == AIArchetype.Tank;
        private bool IsAssassin => _profile.Archetype == AIArchetype.Assassin;
        private bool IsSupport => _profile.Archetype == AIArchetype.Support;

        public AIUtilityScorer(
            BrawlerController self,
            BrawlerAIProfile profile,
            AIObjectiveMemory objectiveMemory,
            AITeamCoordinator teamCoordinator)
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
            ScoreAndReplace(ref best, ScoreApproach(targetInfo, currentTick));
            ScoreAndReplace(ref best, ScorePeel(currentTick));
            ScoreAndReplace(ref best, ScoreRegroup(currentTick));
            ScoreAndReplace(ref best, ScoreSearch(targetInfo, currentTick));
            ScoreAndReplace(ref best, ScoreWander());
            ScoreAndReplace(ref best, ScoreObjective());

            return best;
        }

        public void CollectActionScores(AITargetInfo targetInfo, uint currentTick, List<AIActionScore> results)
        {
            results.Clear();

            results.Add(ScoreRetreat(targetInfo));
            results.Add(ScoreUseSuper(targetInfo));
            results.Add(ScoreHoldRange(targetInfo));
            results.Add(ScoreReposition(targetInfo));
            results.Add(ScoreApproach(targetInfo, currentTick));
            results.Add(ScorePeel(currentTick));
            results.Add(ScoreRegroup(currentTick));
            results.Add(ScoreSearch(targetInfo, currentTick));
            results.Add(ScoreWander());
            results.Add(ScoreObjective());
        }

        private void ScoreAndReplace(ref AIActionScore best, AIActionScore candidate)
        {
            if (candidate.Score > best.Score)
                best = candidate;
        }

        private AIActionScore ScoreRetreat(AITargetInfo targetInfo)
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

            if (IsSniper) score += 20f;
            if (IsSupport) score += 15f;
            if (IsAssassin) score -= 10f;
            if (IsTank) score -= 20f;

            return new AIActionScore(AIActionType.Retreat, score * _profile.RetreatWeight);
        }

        private AIActionScore ScoreUseSuper(AITargetInfo targetInfo)
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

            if (IsAssassin) score += 30f;
            if (IsTank) score += 20f;
            if (IsSniper) score += 10f;
            if (IsSupport) score += 15f;

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

            if (IsSniper) score += 40f;
            if (IsSupport) score += 20f;
            if (IsTank) score -= 20f;
            if (IsAssassin) score -= 10f;

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

            if (IsSniper) score += 25f;
            if (IsSupport) score += 20f;
            if (IsAssassin) score += 10f;
            if (IsTank) score -= 10f;

            return new AIActionScore(AIActionType.Reposition, score * _profile.RepositionWeight);
        }

        private AIActionScore ScoreApproach(AITargetInfo targetInfo, uint currentTick)
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
                focusTarget != null &&
                targetInfo.Target.EntityID == focusTarget.EntityID)
            {
                score += _profile.FocusFireWeight;
            }

            if (IsTank) score += 30f;
            if (IsAssassin) score += 20f;
            if (IsSniper) score -= 15f;
            if (IsSupport) score -= 10f;

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
            float score = 0f;

            if (_teamCoordinator != null && _teamCoordinator.TryGetRegroupPoint(currentTick, out _))
            {
                score += 25f;

                if (IsSupport) score += 20f;
                if (IsSniper) score += 10f;
                if (IsTank) score -= 10f;
            }

            return new AIActionScore(AIActionType.Regroup, score * _profile.RegroupWeight);
        }

        private AIActionScore ScorePeel(uint currentTick)
        {
            float score = 0f;

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) &&
                ally != null)
            {
                score += 40f;

                if (IsSupport) score += 30f;
                if (IsTank) score += 20f;
                if (IsAssassin) score -= 10f;
            }

            return new AIActionScore(AIActionType.Peel, score * _profile.PeelWeight);
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
    }
}