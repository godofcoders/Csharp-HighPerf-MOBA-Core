using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Simulation
{
    public static class AimAssistResolver
    {
        private static readonly List<BrawlerController> _buffer = new List<BrawlerController>(16);

        public static AimAssistResult Resolve(in AimAssistRequest request)
        {
            AimAssistResult result = new AimAssistResult
            {
                HasResult = false,
                Target = null,
                AimDirection = request.Forward.sqrMagnitude > 0.001f ? request.Forward.normalized : Vector3.forward,
                AimPoint = request.Origin
            };

            if (request.Source == null)
                return result;

            switch (request.Mode)
            {
                case AimAssistMode.None:
                case AimAssistMode.ForwardOnly:
                    result.AimDirection = request.Forward.sqrMagnitude > 0.001f
                        ? request.Forward.normalized
                        : request.Source.transform.forward;
                    result.AimPoint = request.Origin + result.AimDirection * Mathf.Max(1f, request.Range);
                    return result;

                case AimAssistMode.SelfCentered:
                    result.HasResult = true;
                    result.AimDirection = request.Source.transform.forward;
                    result.AimPoint = request.Source.Position;
                    return result;

                case AimAssistMode.NearestEnemy:
                    return ResolveTargetByRule(request, AbilityTargetTeamRule.Enemy, AbilityTargetSelectionRule.Nearest);

                case AimAssistMode.NearestAlly:
                    return ResolveTargetByRule(request, AbilityTargetTeamRule.Ally, AbilityTargetSelectionRule.Nearest);

                case AimAssistMode.LowestHealthAlly:
                    return ResolveTargetByRule(request, AbilityTargetTeamRule.Ally, AbilityTargetSelectionRule.LowestHealth);

                case AimAssistMode.SmartOffense:
                    return ResolveSmartOffense(request);

                case AimAssistMode.SmartSupport:
                    return ResolveSmartSupport(request);

                case AimAssistMode.FrontBiasedEnemy:
                    return ResolveFrontBiasedEnemy(request);

                case AimAssistMode.SmartDeployablePlacement:
                    return ResolveSmartDeployablePlacement(request);

                default:
                    return result;
            }
        }

        private static AimAssistResult ResolveTargetByRule(
            in AimAssistRequest request,
            AbilityTargetTeamRule teamRule,
            AbilityTargetSelectionRule selectionRule)
        {
            AimAssistResult result = BuildDefaultResult(request);

            _buffer.Clear();

            request.Source.ResolveTargets(
                teamRule,
                selectionRule,
                request.Range,
                _buffer,
                request.IncludeSelf,
                request.RequireAlive);

            if (_buffer.Count == 0)
            {
                result.AimPoint = request.Origin + result.AimDirection * Mathf.Max(1f, request.Range);
                return result;
            }

            for (int i = 0; i < _buffer.Count; i++)
            {
                if (CanUseAimAssistTarget(request, _buffer[i]))
                    return BuildTargetResult(request, _buffer[i]);
            }

            result.AimPoint = request.Origin + result.AimDirection * Mathf.Max(1f, request.Range);
            return result;
        }

        private static AimAssistResult ResolveSmartOffense(in AimAssistRequest request)
        {
            AimAssistResult result = BuildDefaultResult(request);

            _buffer.Clear();

            request.Source.ResolveTargets(
                AbilityTargetTeamRule.Enemy,
                AbilityTargetSelectionRule.Nearest,
                request.Range,
                _buffer,
                false,
                request.RequireAlive);

            if (_buffer.Count == 0)
                return result;

            BrawlerController best = null;
            BrawlerController closeBest = null;
            float bestScore = float.MinValue;
            float closestPriorityDistance = float.MaxValue;

            Vector3 forward = request.Forward.sqrMagnitude > 0.001f
                ? request.Forward.normalized
                : request.Source.transform.forward;

            float forwardBias = request.AbilityDefinition != null ? request.AbilityDefinition.AimAssistForwardBias : 2f;
            float distanceBias = request.AbilityDefinition != null ? request.AbilityDefinition.AimAssistDistanceBias : 1f;
            float idealRange = request.AbilityDefinition != null ? request.AbilityDefinition.AimAssistIdealRange : -1f;
            float closeTargetRange = Mathf.Max(0f, request.CloseTargetRange);

            for (int i = 0; i < _buffer.Count; i++)
            {
                BrawlerController target = _buffer[i];
                if (target == null)
                    continue;

                if (!CanUseAimAssistTarget(request, target))
                    continue;

                Vector3 aimPoint = ResolvePredictedAimPoint(request, target);
                Vector3 toTarget = aimPoint - request.Origin;
                float dist = toTarget.magnitude;
                if (dist <= 0.001f)
                    continue;

                if (closeTargetRange > 0f &&
                    dist <= closeTargetRange &&
                    dist < closestPriorityDistance)
                {
                    closestPriorityDistance = dist;
                    closeBest = target;
                }

                Vector3 dir = toTarget / dist;
                float score = ScoreSmartOffenseTarget(
                    request,
                    target,
                    forward,
                    dir,
                    dist,
                    forwardBias,
                    distanceBias,
                    idealRange);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }

            if (closeBest != null)
                return BuildTargetResult(request, closeBest);

            return best != null ? BuildTargetResult(request, best) : result;
        }

        private static AimAssistResult ResolveSmartSupport(in AimAssistRequest request)
        {
            AimAssistResult result = BuildDefaultResult(request);

            _buffer.Clear();

            request.Source.ResolveTargets(
                AbilityTargetTeamRule.Ally,
                AbilityTargetSelectionRule.LowestHealth,
                request.Range,
                _buffer,
                request.IncludeSelf,
                request.RequireAlive);

            if (_buffer.Count == 0)
                return result;

            BrawlerController best = null;
            float lowestRatio = float.MaxValue;

            for (int i = 0; i < _buffer.Count; i++)
            {
                BrawlerController target = _buffer[i];
                if (target == null || target.State == null)
                    continue;

                if (!CanUseAimAssistTarget(request, target))
                    continue;

                float maxHealth = Mathf.Max(1f, target.State.MaxHealth.Value);
                float ratio = target.State.CurrentHealth / maxHealth;

                if (ratio < lowestRatio)
                {
                    lowestRatio = ratio;
                    best = target;
                }
            }

            return best != null ? BuildTargetResult(request, best) : result;
        }

        private static AimAssistResult ResolveFrontBiasedEnemy(in AimAssistRequest request)
        {
            return ResolveSmartOffense(request);
        }

        private static AimAssistResult ResolveSmartDeployablePlacement(in AimAssistRequest request)
        {
            AimAssistResult result = BuildDefaultResult(request);

            float placementDistance = 3f;
            if (request.AbilityDefinition != null && request.AbilityDefinition.AimAssistPlacementDistance > 0f)
                placementDistance = request.AbilityDefinition.AimAssistPlacementDistance;

            AimAssistResult targetResult = ResolveSmartOffense(request);

            if (targetResult.HasResult && targetResult.Target != null)
            {
                Vector3 dir = targetResult.AimDirection.sqrMagnitude > 0.001f
                    ? targetResult.AimDirection.normalized
                    : request.Source.transform.forward;

                result.HasResult = true;
                result.Target = targetResult.Target;
                result.AimDirection = dir;
                result.AimPoint = request.Origin + dir * Mathf.Min(request.Range, placementDistance);
                return result;
            }

            Vector3 fallbackDir = request.Forward.sqrMagnitude > 0.001f
                ? request.Forward.normalized
                : request.Source.transform.forward;

            result.HasResult = true;
            result.AimDirection = fallbackDir;
            result.AimPoint = request.Origin + fallbackDir * Mathf.Min(request.Range, placementDistance);
            return result;
        }

        private static float ScoreSmartOffenseTarget(
            in AimAssistRequest request,
            BrawlerController target,
            Vector3 forward,
            Vector3 targetDirection,
            float distance,
            float forwardBias,
            float distanceBias,
            float idealRange)
        {
            float facingScore = Mathf.Max(0f, Vector3.Dot(forward, targetDirection));
            float distanceScore = ResolveDistanceScore(request.Range, distance, idealRange);
            float lowHealthScore = ResolveLowHealthScore(target);
            float carrierScore = ResolveGemCarrierScore(target);

            return facingScore * Mathf.Max(0f, forwardBias) +
                   distanceScore * Mathf.Max(0f, distanceBias) +
                   lowHealthScore * Mathf.Max(0f, request.LowHealthBias) +
                   carrierScore * Mathf.Max(0f, request.GemCarrierBias);
        }

        private static float ResolveDistanceScore(float range, float distance, float idealRange)
        {
            float safeRange = Mathf.Max(1f, range);

            if (idealRange > 0f)
            {
                float spread = Mathf.Max(1f, Mathf.Max(idealRange, safeRange - idealRange));
                return 1f - Mathf.Clamp01(Mathf.Abs(distance - idealRange) / spread);
            }

            return 1f - Mathf.Clamp01(distance / safeRange);
        }

        private static float ResolveLowHealthScore(BrawlerController target)
        {
            if (target == null || target.State == null)
                return 0f;

            float maxHealth = Mathf.Max(1f, target.State.MaxHealth.Value);
            float healthRatio = Mathf.Clamp01(target.State.CurrentHealth / maxHealth);
            return 1f - healthRatio;
        }

        private static float ResolveGemCarrierScore(BrawlerController target)
        {
            if (target == null || target.State == null)
                return 0f;

            return Mathf.Clamp01(target.State.CarriedGemCount / 5f);
        }

        private static AimAssistResult BuildDefaultResult(in AimAssistRequest request)
        {
            Vector3 forward = request.Forward.sqrMagnitude > 0.001f
                ? request.Forward.normalized
                : request.Source.transform.forward;

            return new AimAssistResult
            {
                HasResult = false,
                Target = null,
                AimDirection = forward,
                AimPoint = request.Origin
            };
        }

        private static bool CanUseAimAssistTarget(
            in AimAssistRequest request,
            BrawlerController target)
        {
            if (target == null)
                return false;

            if (!request.RequireLineOfSight)
                return true;

            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
                return true;

            Vector3 aimPoint = ResolvePredictedAimPoint(request, target);
            return AimLineOfSightUtility.HasLineOfSight(
                pathfinder,
                request.Origin,
                aimPoint,
                request.ProjectileRadius);
        }

        private static AimAssistResult BuildTargetResult(in AimAssistRequest request, BrawlerController target)
        {
            AimAssistResult result = BuildDefaultResult(request);

            if (target == null)
                return result;

            Vector3 aimPoint = ResolvePredictedAimPoint(request, target);
            Vector3 dir = aimPoint - request.Origin;
            if (dir.sqrMagnitude > 0.001f)
                dir.Normalize();
            else
                dir = request.Source.transform.forward;

            result.HasResult = true;
            result.Target = target;
            result.AimDirection = dir;
            result.AimPoint = aimPoint;
            return result;
        }

        private static Vector3 ResolvePredictedAimPoint(in AimAssistRequest request, BrawlerController target)
        {
            if (target == null)
                return request.Origin;

            Vector3 targetPosition = target.Position;
            float leadStrength = Mathf.Clamp01(request.LeadStrength);
            float projectileSpeed = Mathf.Max(0f, request.ProjectileSpeed);

            if (leadStrength <= 0f || projectileSpeed <= 0.001f)
                return targetPosition;

            Vector3 targetVelocity = target.PlanarVelocity;
            targetVelocity.y = 0f;
            if (targetVelocity.sqrMagnitude <= 0.001f)
                return targetPosition;

            Vector3 toTarget = targetPosition - request.Origin;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance <= 0.001f)
                return targetPosition;

            float leadTime = Mathf.Clamp(distance / projectileSpeed * leadStrength, 0f, 0.65f);
            Vector3 predicted = targetPosition + targetVelocity * leadTime;

            Vector3 predictedOffset = predicted - request.Origin;
            predictedOffset.y = 0f;
            float range = Mathf.Max(0.1f, request.Range);
            if (predictedOffset.sqrMagnitude > range * range)
            {
                predicted = request.Origin + predictedOffset.normalized * range;
                predicted.y = targetPosition.y;
            }

            return predicted;
        }
    }
}
