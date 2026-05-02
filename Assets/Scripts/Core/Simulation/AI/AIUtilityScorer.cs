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

        private bool IsSniper => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Sniper;
        private bool IsTank => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Tank;
        private bool IsAssassin => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Assassin;
        private bool IsSupport => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Support;
        private bool IsFighter => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Fighter;
        private bool IsController => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Controller;
        private bool IsArtillery => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Artillery;

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
                score += 5f * carrierTarget.State.CarriedGemCount;
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

            if (IsSniper) score += 20f;
            if (IsSupport) score += 10f;
            if (IsTank) score -= 12f;
            if (IsAssassin) score -= 6f;
            if (IsController) score += 12f;
            if (IsArtillery) score += 18f;

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

            if (IsSniper) score += 15f;
            if (IsSupport) score += 10f;
            if (IsAssassin) score += 6f;
            if (IsTank) score -= 8f;
            if (IsController) score += 10f;
            if (IsArtillery) score += 14f;

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

            float roleAggression = _self.Definition != null ? _self.Definition.Aggression : 1f;
            score *= roleAggression;

            if (IsTank) score += 12f;
            if (IsAssassin) score += 10f;
            if (IsSniper) score -= 8f;
            if (IsSupport) score -= 6f;
            if (IsController) score -= 3f;
            if (IsArtillery) score -= 10f;

            // Gem Grab: behind on gems → push harder. The behind-ness check
            // is null-safe so non-Gem-Grab matches and unit-test contexts
            // skip this branch entirely.
            if (GemGrabMode.Instance != null && GemGrabMode.Instance.IsTeamBehind(_self.Team))
                score += 8f;

            return new AIActionScore(AIActionType.Approach, score * _profile.ApproachWeight);
        }

        private AIActionScore ScoreSearch(AITargetInfo targetInfo, uint currentTick)
        {
            float score = 0f;

            if (targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
                score += 30f;

            if (AITeamMemory.TryGetRecentHotspot(_self.Team, currentTick, _profile.SharedHotspotMemoryTicks, out _))
                score += 20f;

            // Gem Grab hunger: if there's an unpicked Gem within sensing
            // range, sharply prioritise movement so the brawler explores
            // toward it (rather than holding range or wandering). +35 is
            // intentionally higher than the recent-memory bonus — a fresh
            // gem in sight is more valuable than chasing an old hotspot.
            if (Gem.HasAnyUnpickedWithin(_self.Position, 8f))
                score += 35f;

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
            }

            float teamplay = _self.Definition != null ? _self.Definition.TeamplayWeight : 1f;
            score *= teamplay;

            if (IsSupport) score += 10f;
            if (IsSniper) score += 6f;
            if (IsTank) score -= 6f;
            if (IsController) score += 8f;
            if (IsArtillery) score += 8f;

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

                // Gem Grab carrier protection: if the threatened ally is
                // carrying gems, peel even harder. +8 per gem stacks on top
                // of the base +40 — a 3-gem carrier under threat earns +64
                // total, comfortably outranking most other actions.
                if (ally.State != null && ally.State.CarriedGemCount > 0)
                    score += 8f * ally.State.CarriedGemCount;
            }

            float teamplay = _self.Definition != null ? _self.Definition.TeamplayWeight : 1f;
            score *= teamplay;

            if (IsSupport) score += 20f;
            if (IsTank) score += 12f;
            if (IsAssassin) score -= 8f;
            if (IsController) score += 15f;
            if (IsArtillery) score += 10f;

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