using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AITargetScorer
    {
        private readonly BrawlerController _self;
        private readonly BrawlerAIProfile _profile;
        private readonly List<ISpatialEntity> _clusterBuffer;
        private readonly uint _threatForgetTicks = 240;

        public AITargetScorer(BrawlerController self, BrawlerAIProfile profile, int initialCapacity = 16)
        {
            _self = self;
            _profile = profile;
            _clusterBuffer = new List<ISpatialEntity>(initialCapacity);
        }

        public ISpatialEntity SelectBestTarget(List<ISpatialEntity> candidates, AITargetInfo memory, uint currentTick)
        {
            ISpatialEntity best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                ISpatialEntity candidate = candidates[i];
                float score = ScoreTarget(candidate, memory, currentTick);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        public float ScoreTarget(ISpatialEntity target, AITargetInfo memory, uint currentTick)
        {
            if (target == null)
                return float.MinValue;

            Vector3 delta = target.Position - _self.Position;
            float distSq = delta.sqrMagnitude;

            float score = 0f;

            // 1. Distance matters
            score -= distSq * Mathf.Max(0.01f, _profile.DistanceWeight);

            // 2. Prefer keeping current target a bit
            if (memory.HasLiveTarget && memory.Target != null && memory.Target.EntityID == target.EntityID)
            {
                score += _profile.CurrentTargetStickiness;
            }

            // 3. Target health / status scoring
            if (target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                float maxHealth = Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
                float healthRatio = targetBrawler.State.CurrentHealth / maxHealth;
                score += (1f - healthRatio) * _profile.LowHealthTargetBias;

                if (healthRatio <= _profile.FinisherHealthThreshold)
                {
                    score += _profile.FinisherBonus;
                }

                // Close enemies are more urgent
                score += Mathf.Clamp01(1f - (Mathf.Sqrt(distSq) / Mathf.Max(1f, _profile.ThreatRange))) * _profile.ThreatBonus;

                // STATUS-AWARE TARGETING
                if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                {
                    score += 50f;
                }

                if (targetBrawler.State.HasStatus(StatusEffectType.Slow))
                {
                    score += 20f;
                }

                if (targetBrawler.State.HasStatus(StatusEffectType.Burn))
                {
                    score += 10f;
                }
            }

            // 4. My own remembered threat memory
            if (_self.State != null && _self.State.ThreatTracker != null)
            {
                float rememberedThreat = _self.State.ThreatTracker.GetThreat(target.EntityID, currentTick, _threatForgetTicks);
                score += rememberedThreat * 0.75f;

                int topThreatId = _self.State.ThreatTracker.GetHighestThreatTarget(currentTick, _threatForgetTicks);
                if (topThreatId != 0 && topThreatId == target.EntityID)
                {
                    score += 30f;
                }
            }

            // 5. Team focus-fire bonus
            if (AITeamBlackboard.TryGetFocusTarget(_self.Team, currentTick, 90, out var focusTarget) &&
                focusTarget != null &&
                focusTarget.EntityID == target.EntityID)
            {
                score += _profile.FocusFireWeight;
            }

            // 6. Ability-aware bonus
            score += ScoreByAbilityShape(target);

            return score;
        }

        private float ScoreByAbilityShape(ISpatialEntity target)
        {
            AbilityDefinition attack = _self.Definition?.MainAttack;
            if (attack == null || SimulationClock.Grid == null)
                return 0f;

            // AoE users prefer clustered enemies.
            if (attack is AoEAbilityDefinition aoe)
            {
                int clusterCount = CountEnemiesNear(target.Position, aoe.Radius * 1.1f, _self.Team);
                if (clusterCount > 1)
                {
                    return (clusterCount - 1) * _profile.ClusterTargetBonus;
                }
            }

            // Straight projectile users prefer more reachable targets.
            if (attack is ProjectileAbilityDefinition projectile)
            {
                float range = Mathf.Max(1f, projectile.Range);
                float distance = Vector3.Distance(_self.Position, target.Position);
                float normalized = 1f - Mathf.Clamp01(distance / range);
                return normalized * _profile.InRangeTargetBonus;
            }

            return 0f;
        }

        private int CountEnemiesNear(Vector3 position, float radius, TeamType selfTeam)
        {
            _clusterBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(position, radius, _clusterBuffer);

            int count = 0;
            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                ISpatialEntity entity = _clusterBuffer[i];
                if (entity == null || entity.Team == selfTeam)
                    continue;

                if (entity is BrawlerController bc && (bc.State == null || bc.State.IsDead))
                    continue;

                count++;
            }

            return count;
        }
    }
}