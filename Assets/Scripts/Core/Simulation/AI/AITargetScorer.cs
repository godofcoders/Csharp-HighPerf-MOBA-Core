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
            if (!SpatialEntityUtility.IsAlive(target))
                return float.MinValue;

            Vector3 delta = target.Position - _self.Position;
            float dist = delta.sqrMagnitude;
            int targetEntityId = target.EntityID;

            float score = 0f;

            // 1. Distance matters
            score -= dist * Mathf.Max(0.01f, _profile.DistanceWeight);

            // 2. Prefer keeping current target a bit
            if (memory.HasLiveTarget &&
                SpatialEntityUtility.IsAlive(memory.Target) &&
                memory.Target.EntityID == targetEntityId)
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
                score += Mathf.Clamp01(1f - (Mathf.Sqrt(dist) / Mathf.Max(1f, _profile.ThreatRange))) * _profile.ThreatBonus;

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
                float rememberedThreat = _self.State.ThreatTracker.GetThreat(targetEntityId, currentTick, _threatForgetTicks);
                score += rememberedThreat * 0.75f;

                int topThreatId = _self.State.ThreatTracker.GetHighestThreatTarget(currentTick, _threatForgetTicks);
                if (topThreatId != 0 && topThreatId == targetEntityId)
                {
                    score += 30f;
                }
            }

            // 5. Team focus-fire bonus
            if (AITeamBlackboard.TryGetFocusTarget(_self.Team, currentTick, 90, out var focusTarget) &&
                SpatialEntityUtility.IsAlive(focusTarget) &&
                focusTarget.EntityID == targetEntityId)
            {
                score += _profile.FocusFireWeight;
            }

            // 6. Team anti-overfocus penalty.
            // Count allies excluding this bot so retargeting is not biased by
            // the bot's previous focus report from an earlier tick.
            float overFocusPenalty = CalculateOverFocusedTargetPenalty(
                targetEntityId,
                out _);

            score -= overFocusPenalty;

            // 7. Ability-aware bonus
            score += ScoreByAbilityShape(target);

            return score;
        }

        public float CalculateOverFocusedTargetPenalty(
            int targetEntityId,
            out int alliedFocusCount)
        {
            alliedFocusCount = 0;

            if (targetEntityId == 0 || _self == null || _profile == null)
                return 0f;

            alliedFocusCount = AITeamBlackboard.GetTargetFocusCountExcluding(
                _self.Team,
                targetEntityId,
                _self.EntityID);

            int softLimit = Mathf.Max(0, _profile.TargetFocusSoftLimit);
            int excessFocus = alliedFocusCount - softLimit;

            if (excessFocus <= 0)
                return 0f;

            float rawPenalty = excessFocus * Mathf.Max(0f, _profile.OverFocusedTargetPenaltyPerAlly);
            return Mathf.Min(rawPenalty, Mathf.Max(0f, _profile.MaxOverFocusedTargetPenalty));
        }

        private float ScoreByAbilityShape(ISpatialEntity target)
        {
            if (!SpatialEntityUtility.IsAlive(target))
                return 0f;

            AbilityDefinition attack = _self.State != null
                ? _self.State.GetCurrentMainAttackDefinition()
                : _self.Definition?.MainAttack;
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

            if (attack is ChainProjectileAbilityDefinition chain)
            {
                int bounceTargets = CountEnemiesNear(target.Position, chain.BounceRadius, _self.Team);
                if (bounceTargets > 1)
                {
                    return (bounceTargets - 1) * _profile.ClusterTargetBonus;
                }
            }

            if (attack is ThrownHybridAoEAbilityDefinition thrown)
            {
                int clusterCount = CountEnemiesNear(target.Position, thrown.ImpactRadius * 1.35f, _self.Team);
                if (clusterCount > 1)
                {
                    return (clusterCount - 1) * _profile.ClusterTargetBonus;
                }
            }

            if (attack is ThrownVolleyAoEAbilityDefinition volley)
            {
                int clusterCount = CountEnemiesNear(target.Position, volley.ImpactRadius * 1.75f, _self.Team);
                if (clusterCount > 1)
                {
                    return (clusterCount - 1) * _profile.ClusterTargetBonus;
                }
            }

            if (attack is BurstSequenceProjectileAbilityDefinition burst)
            {
                int lineTargets = CountEnemiesAlongLine(target.Position, burst.Range, 1.5f);
                if (lineTargets > 1)
                {
                    return (lineTargets - 1) * (_profile.InRangeTargetBonus * 0.75f);
                }

                float distance = Vector3.Distance(_self.Position, target.Position);
                float normalized = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, burst.Range));
                return normalized * _profile.InRangeTargetBonus;
            }

            if (attack is HybridProjectileAbilityDefinition hybrid)
            {
                float distance = Vector3.Distance(_self.Position, target.Position);
                float normalized = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, hybrid.Range));
                return normalized * _profile.InRangeTargetBonus;
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
                if (!SpatialEntityUtility.IsAlive(entity) || entity.Team == selfTeam)
                    continue;

                if (entity is BrawlerController bc && (bc.State == null || bc.State.IsDead))
                    continue;

                count++;
            }

            return count;
        }

        private int CountEnemiesAlongLine(Vector3 targetPosition, float range, float width)
        {
            if (SimulationClock.Grid == null)
                return 0;

            Vector3 direction = targetPosition - _self.Position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
                return 0;

            direction.Normalize();

            _clusterBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(_self.Position, range, _clusterBuffer);

            int count = 0;

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                ISpatialEntity entity = _clusterBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) || entity.Team == _self.Team)
                    continue;

                if (entity is BrawlerController bc && (bc.State == null || bc.State.IsDead))
                    continue;

                Vector3 toEntity = entity.Position - _self.Position;
                toEntity.y = 0f;

                float forwardDistance = Vector3.Dot(toEntity, direction);
                if (forwardDistance <= 0f || forwardDistance > range)
                    continue;

                Vector3 closestPoint = _self.Position + direction * forwardDistance;
                if (Vector3.Distance(closestPoint, entity.Position) <= width)
                    count++;
            }

            return count;
        }
    }
}
