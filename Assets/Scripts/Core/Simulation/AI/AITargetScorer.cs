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
        private AITeamCoordinator _teamCoordinator;
        private bool _hasWinMacroCache;
        private uint _lastWinMacroTick;
        private AIGameModeMacroState _lastWinMacroState;
        private string _lastTargetContextDebug = "TargetCtx=None";
        private string _candidateTargetContextDebug = "TargetCtx=None";

        private const float CarrierThreatRadius = 5.5f;
        private const float CarrierThreatCorridorWidth = 2.15f;
        private const float CarrierThreatProximityBonus = 24f;
        private const float CarrierThreatCorridorBonus = 16f;
        private const float CarrierThreatMaxBonus = 42f;
        private const float BrawlBallCarrierBaseBonus = 92f;
        private const float BrawlBallCarrierNearBonus = 34f;
        private const float BrawlBallCarrierPressureRadius = 13f;

        public string LastTargetContextDebug => _lastTargetContextDebug;

        public AITargetScorer(BrawlerController self, BrawlerAIProfile profile, int initialCapacity = 16)
        {
            _self = self;
            _profile = profile;
            _clusterBuffer = new List<ISpatialEntity>(initialCapacity);
        }

        public void SetTeamCoordinator(AITeamCoordinator teamCoordinator)
        {
            _teamCoordinator = teamCoordinator;
        }

        public ISpatialEntity SelectBestTarget(List<ISpatialEntity> candidates, AITargetInfo memory, uint currentTick)
        {
            ISpatialEntity best = null;
            float bestScore = float.MinValue;
            string bestTargetContextDebug = "TargetCtx=None";

            for (int i = 0; i < candidates.Count; i++)
            {
                ISpatialEntity candidate = candidates[i];
                float score = ScoreTarget(candidate, memory, currentTick);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    bestTargetContextDebug = _candidateTargetContextDebug;
                }
            }

            _lastTargetContextDebug = bestTargetContextDebug;
            return best;
        }

        public float ScoreTarget(ISpatialEntity target, AITargetInfo memory, uint currentTick)
        {
            _candidateTargetContextDebug = "TargetCtx=None";

            if (!SpatialEntityUtility.IsAlive(target))
                return float.MinValue;

            Vector3 delta = target.Position - _self.Position;
            float dist = delta.sqrMagnitude;
            float distance = Mathf.Sqrt(dist);
            int targetEntityId = target.EntityID;

            float score = 0f;
            bool isCurrentTarget =
                memory.HasLiveTarget &&
                SpatialEntityUtility.IsAlive(memory.Target) &&
                memory.Target.EntityID == targetEntityId;
            bool isTeamFocusTarget = false;
            bool hasBrawlerTarget = false;
            bool isEnemyBrawlBallCarrier = false;
            BrawlerController targetBrawlerRef = null;
            int targetCarriedGems = 0;
            float targetHealthRatio = 1f;

            // 1. Distance matters
            score -= dist * Mathf.Max(0.01f, _profile.DistanceWeight);

            // 2. Prefer keeping current target a bit
            if (isCurrentTarget)
            {
                score += _profile.CurrentTargetStickiness;
            }

            // 3. Target health / status scoring
            if (target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                hasBrawlerTarget = true;
                targetBrawlerRef = targetBrawler;
                targetCarriedGems = targetBrawler.State.CarriedGemCount;
                float maxHealth = Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
                targetHealthRatio = Mathf.Clamp01(targetBrawler.State.CurrentHealth / maxHealth);
                score += (1f - targetHealthRatio) * _profile.LowHealthTargetBias;
                isEnemyBrawlBallCarrier = IsEnemyBrawlBallCarrier(targetBrawler);

                if (targetHealthRatio <= _profile.FinisherHealthThreshold)
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

                if (AIOpponentModel.TryGetSnapshot(
                        _self.Team,
                        targetEntityId,
                        currentTick,
                        360u,
                        out AIOpponentHabitSnapshot habit))
                {
                    score += habit.Aggression * 18f;

                    if (habit.PreferredTargetEntityId == _self.EntityID)
                        score += habit.TargetPreferenceConfidence * 22f;

                    if (targetHealthRatio <= _profile.FinisherHealthThreshold)
                        score += habit.LowHealthGreed * 16f;

                    if (habit.ObjectiveNeglect > 0.55f)
                        score -= habit.ObjectiveNeglect * 6f;
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
            float focusUrgency = 1f;
            BrawlerController focusTarget;
            bool hasFocusDirective;
            if (_teamCoordinator != null)
            {
                hasFocusDirective = _teamCoordinator.TryGetFocusDirective(
                    currentTick,
                    out focusTarget,
                    out focusUrgency,
                    out _);
            }
            else
            {
                hasFocusDirective = AITeamBlackboard.TryGetFocusTarget(
                    _self.Team,
                    currentTick,
                    90,
                    out focusTarget);
            }

            if (hasFocusDirective &&
                SpatialEntityUtility.IsAlive(focusTarget) &&
                focusTarget.EntityID == targetEntityId)
            {
                isTeamFocusTarget = true;
                score += _profile.FocusFireWeight +
                         Mathf.Clamp(focusUrgency * 4f, 0f, 18f);
            }

            // 6. Team anti-overfocus penalty.
            // Count allies excluding this bot so retargeting is not biased by
            // the bot's previous focus report from an earlier tick.
            float overFocusPenalty = CalculateOverFocusedTargetPenalty(
                targetEntityId,
                out int alliedFocusCount);

            if (isEnemyBrawlBallCarrier)
            {
                float proximityBonus =
                    (1f - Mathf.Clamp01(distance / BrawlBallCarrierPressureRadius)) *
                    BrawlBallCarrierNearBonus;
                score += BrawlBallCarrierBaseBonus + proximityBonus;
                overFocusPenalty *= 0.15f;
                isTeamFocusTarget = true;
            }

            if (hasBrawlerTarget)
            {
                AIWinConditionTargetEvaluation winEvaluation =
                    AIWinConditionUtility.EvaluateTarget(
                        new AIWinConditionTargetContext(
                            ResolveWinMacroState(currentTick),
                            _self.State != null ? _self.State.CarriedGemCount : 0,
                            targetCarriedGems,
                            targetHealthRatio,
                            distance,
                            isCurrentTarget,
                            isTeamFocusTarget,
                            alliedFocusCount));

                if (winEvaluation.HasDelta)
                    score += winEvaluation.ScoreDelta;

                if (winEvaluation.ShouldCollapse)
                    overFocusPenalty *= 0.25f;
            }

            if (targetBrawlerRef != null)
            {
                score += ScoreTargetContext(
                    targetBrawlerRef,
                    distance,
                    isTeamFocusTarget,
                    targetCarriedGems,
                    targetHealthRatio,
                    alliedFocusCount);
            }

            score -= overFocusPenalty;

            // 7. Carrier-protection bonus. Escorts prefer enemies who are
            // close to the carrier or entering the carrier-to-pressure lane.
            score += ScoreCarrierThreatTarget(target, currentTick, targetEntityId);

            // 8. Opponent resource windows: higher-tier bots punish enemies
            // who have no ammo, but respect ready supers.
            if (targetBrawlerRef != null)
            {
                score += AIOpponentResourceUtility.GetTargetOpportunityScore(
                    targetBrawlerRef,
                    currentTick,
                    _profile,
                    out _);
            }

            // 9. Ability-aware bonus
            score += ScoreByAbilityShape(target);

            return score;
        }

        private bool IsEnemyBrawlBallCarrier(BrawlerController targetBrawler)
        {
            BrawlBallMode mode = BrawlBallMode.Instance;
            return mode != null &&
                   mode.BallCarrier == targetBrawler &&
                   _self != null &&
                   targetBrawler.Team != _self.Team;
        }

        private AIGameModeMacroState ResolveWinMacroState(uint currentTick)
        {
            if (_hasWinMacroCache && _lastWinMacroTick == currentTick)
                return _lastWinMacroState;

            _lastWinMacroState = AIGameModeMacroStrategy.ResolveCurrentMode(_self.Team);
            _lastWinMacroTick = currentTick;
            _hasWinMacroCache = true;
            return _lastWinMacroState;
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

        public static float CalculateCarrierThreatBonus(
            AITeamPlaybookState playbookState,
            Vector3 targetPosition,
            bool selfIsCarrier,
            out string reason)
        {
            reason = "carrier_threat_none";

            if (!playbookState.IsActive ||
                playbookState.Call != AITeamPlaybookCall.EscortCarrier ||
                selfIsCarrier ||
                playbookState.CarrierEntityId == 0 ||
                !playbookState.HasAnchorPoint)
            {
                return 0f;
            }

            float bonus = 0f;
            string parts = string.Empty;
            Vector3 carrierPoint = playbookState.AnchorPoint;
            float carrierDistance = XZDistance(targetPosition, carrierPoint);

            if (carrierDistance <= CarrierThreatRadius)
            {
                float proximity = 1f - carrierDistance / CarrierThreatRadius;
                bonus += proximity * CarrierThreatProximityBonus;
                parts = AppendReason(parts, "near_carrier");
            }

            if (playbookState.HasPressurePoint)
            {
                float corridor = CalculatePressureCorridorThreat(
                    carrierPoint,
                    playbookState.PressurePoint,
                    targetPosition);

                if (corridor > 0f)
                {
                    bonus += corridor * CarrierThreatCorridorBonus;
                    parts = AppendReason(parts, "pressure_lane");
                }
            }

            if (bonus <= 0.01f)
                return 0f;

            switch (playbookState.EscortRole)
            {
                case AITeamEscortFormationRole.Screen:
                    bonus *= 1.15f;
                    break;

                case AITeamEscortFormationRole.PressureFlank:
                    bonus *= 1.05f;
                    break;

                case AITeamEscortFormationRole.Shadow:
                    bonus *= 0.80f;
                    break;
            }

            reason = $"carrier_threat:{parts}";
            return Mathf.Clamp(bonus, 0f, CarrierThreatMaxBonus);
        }

        private float ScoreCarrierThreatTarget(
            ISpatialEntity target,
            uint currentTick,
            int targetEntityId)
        {
            if (_teamCoordinator == null ||
                _self == null ||
                !SpatialEntityUtility.IsAlive(target) ||
                !_teamCoordinator.TryGetPlaybookState(
                    currentTick,
                    out AITeamPlaybookState playbookState))
            {
                return 0f;
            }

            bool selfIsCarrier = playbookState.CarrierEntityId == _self.EntityID;
            float bonus = CalculateCarrierThreatBonus(
                playbookState,
                target.Position,
                selfIsCarrier,
                out _);

            if (bonus <= 0.01f)
                return 0f;

            if (playbookState.FocusTargetEntityId != 0 &&
                playbookState.FocusTargetEntityId == targetEntityId)
            {
                bonus += 8f;
            }

            return Mathf.Clamp(bonus, 0f, CarrierThreatMaxBonus + 8f);
        }

        private float ScoreTargetContext(
            BrawlerController target,
            float distance,
            bool isTeamFocusTarget,
            int targetCarriedGems,
            float targetHealthRatio,
            int alliedFocusCount)
        {
            if (_profile == null ||
                SimulationClock.Grid == null ||
                !SpatialEntityUtility.IsAlive(target))
            {
                _candidateTargetContextDebug = "TargetCtx=None";
                return 0f;
            }

            float weight = Mathf.Max(0f, _profile.TargetContextAwarenessWeight);
            if (weight <= 0.01f)
            {
                _candidateTargetContextDebug = "TargetCtx=Off";
                return 0f;
            }

            float radius = Mathf.Max(2f, _profile.TargetContextRadius);
            _clusterBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                target.Position,
                radius,
                _clusterBuffer);

            float allyCollapsePressure = 0f;
            float protectorPressure = 0f;
            int allyCollapseCount = 0;
            int protectorCount = 0;

            for (int i = 0; i < _clusterBuffer.Count; i++)
            {
                ISpatialEntity entity = _clusterBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) ||
                    entity.EntityID == target.EntityID)
                {
                    continue;
                }

                float proximity =
                    1f - Mathf.Clamp01(XZDistance(entity.Position, target.Position) / radius);
                if (proximity <= 0f)
                    continue;

                if (entity.Team == _self.Team)
                {
                    allyCollapseCount++;
                    allyCollapsePressure += 0.35f + proximity;
                }
                else if (entity.Team == target.Team)
                {
                    protectorCount++;
                    protectorPressure += 0.35f + proximity;
                }
            }

            if (distance <= radius)
            {
                float selfProximity = 1f - Mathf.Clamp01(distance / radius);
                allyCollapsePressure += 0.50f + selfProximity;
            }

            bool highValueTarget = targetCarriedGems >= 3;
            bool vulnerableTarget =
                targetHealthRatio <= Mathf.Max(0.25f, _profile.FinisherHealthThreshold) ||
                target.State.HasStatus(StatusEffectType.Stun) ||
                target.State.HasStatus(StatusEffectType.Slow);
            float isolation = Mathf.Clamp01(1f - protectorPressure / 2.2f);
            float collapseAdvantage = allyCollapsePressure - protectorPressure;
            float score = 0f;

            if (isolation > 0.25f && (vulnerableTarget || highValueTarget || isTeamFocusTarget))
            {
                score += isolation * _profile.IsolatedTargetBonus;
            }

            if (collapseAdvantage > 0.15f)
            {
                score += Mathf.Clamp01(collapseAdvantage / 2.4f) *
                         _profile.AllyCollapseTargetBonus;
            }

            if (protectorPressure > allyCollapsePressure + 0.25f &&
                !isTeamFocusTarget &&
                !highValueTarget)
            {
                float protectedPressure =
                    Mathf.Clamp01((protectorPressure - allyCollapsePressure) / 2.5f);
                score -= protectedPressure * _profile.ProtectedTargetPenalty;
            }

            if (alliedFocusCount > 0 && vulnerableTarget)
            {
                score += Mathf.Min(1.5f, alliedFocusCount * 0.45f) *
                         _profile.AllyCollapseTargetBonus;
            }

            float weightedScore = score * weight;
            _candidateTargetContextDebug =
                $"TargetCtx=ally:{allyCollapseCount}/{allyCollapsePressure:0.0} " +
                $"protect:{protectorCount}/{protectorPressure:0.0} " +
                $"iso:{isolation:0.00} value:{highValueTarget} vuln:{vulnerableTarget} " +
                $"w:{weight:0.00} delta:{weightedScore:+0.0;-0.0}";

            return weightedScore;
        }

        private static float CalculatePressureCorridorThreat(
            Vector3 carrierPoint,
            Vector3 pressurePoint,
            Vector3 targetPosition)
        {
            Vector3 corridor = pressurePoint - carrierPoint;
            corridor.y = 0f;
            float lengthSq = corridor.sqrMagnitude;
            if (lengthSq <= 0.001f)
                return 0f;

            Vector3 toTarget = targetPosition - carrierPoint;
            toTarget.y = 0f;
            float t = Mathf.Clamp01(Vector3.Dot(toTarget, corridor) / lengthSq);
            Vector3 closest = carrierPoint + corridor * t;
            float lateralDistance = XZDistance(targetPosition, closest);

            if (lateralDistance > CarrierThreatCorridorWidth)
                return 0f;

            float lateralPressure = 1f - lateralDistance / CarrierThreatCorridorWidth;
            float carrierSideBias = 1f - t * 0.35f;
            return Mathf.Clamp01(lateralPressure * carrierSideBias);
        }

        private static float XZDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static string AppendReason(string current, string value)
        {
            return string.IsNullOrEmpty(current)
                ? value
                : $"{current}|{value}";
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

            if (attack is MeleeConeAbilityDefinition melee)
            {
                int closeTargets = CountEnemiesNear(target.Position, Mathf.Max(1.5f, melee.Range), _self.Team);
                if (closeTargets > 1)
                    return (closeTargets - 1) * _profile.ClusterTargetBonus;
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

            if (attack is VolleyProjectileAbilityDefinition volleyProjectile)
            {
                int lineTargets = CountEnemiesAlongLine(
                    target.Position,
                    volleyProjectile.Range,
                    Mathf.Max(1.15f, volleyProjectile.AimPreviewWidth));

                if (lineTargets > 1)
                    return (lineTargets - 1) * (_profile.InRangeTargetBonus * 0.65f);

                float range = Mathf.Max(1f, volleyProjectile.Range);
                float distance = Vector3.Distance(_self.Position, target.Position);
                float normalized = 1f - Mathf.Clamp01(distance / range);
                return normalized * (_profile.InRangeTargetBonus * 0.85f);
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
