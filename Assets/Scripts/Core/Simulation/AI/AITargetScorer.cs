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

            score -= distSq * Mathf.Max(0.01f, _profile.DistanceWeight);

            if (memory.HasLiveTarget && memory.Target != null && memory.Target.EntityID == target.EntityID)
            {
                score += _profile.CurrentTargetStickiness;
            }

            if (target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                float maxHealth = Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
                float healthRatio = targetBrawler.State.CurrentHealth / maxHealth;
                score += (1f - healthRatio) * _profile.LowHealthTargetBias;

                // Prefer targets that are also low enough to plausibly finish soon.
                if (healthRatio <= _profile.FinisherHealthThreshold)
                {
                    score += _profile.FinisherBonus;
                }

                // Threat bonus: enemies that are close are more urgent.
                score += Mathf.Clamp01(1f - (Mathf.Sqrt(distSq) / Mathf.Max(1f, _profile.ThreatRange))) * _profile.ThreatBonus;
            }

            // Ability-aware bonus
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