using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

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

            return BuildTargetResult(request, _buffer[0]);
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
            float bestScore = float.MinValue;

            Vector3 forward = request.Forward.sqrMagnitude > 0.001f
                ? request.Forward.normalized
                : request.Source.transform.forward;

            float forwardBias = request.AbilityDefinition != null ? request.AbilityDefinition.AimAssistForwardBias : 2f;
            float distanceBias = request.AbilityDefinition != null ? request.AbilityDefinition.AimAssistDistanceBias : 1f;
            float idealRange = request.AbilityDefinition != null ? request.AbilityDefinition.AimAssistIdealRange : -1f;

            for (int i = 0; i < _buffer.Count; i++)
            {
                BrawlerController target = _buffer[i];
                if (target == null)
                    continue;

                Vector3 toTarget = target.Position - request.Origin;
                float dist = toTarget.magnitude;
                if (dist <= 0.001f)
                    continue;

                Vector3 dir = toTarget / dist;
                float facingScore = Mathf.Max(0f, Vector3.Dot(forward, dir));

                float distanceScore;
                if (idealRange > 0f)
                {
                    float idealDelta = Mathf.Abs(dist - idealRange);
                    distanceScore = 1f / Mathf.Max(1f, idealDelta + 1f);
                }
                else
                {
                    distanceScore = 1f / Mathf.Max(1f, dist);
                }

                float score = facingScore * forwardBias + distanceScore * distanceBias;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }

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

        private static AimAssistResult BuildTargetResult(in AimAssistRequest request, BrawlerController target)
        {
            AimAssistResult result = BuildDefaultResult(request);

            if (target == null)
                return result;

            Vector3 dir = target.Position - request.Origin;
            if (dir.sqrMagnitude > 0.001f)
                dir.Normalize();
            else
                dir = request.Source.transform.forward;

            result.HasResult = true;
            result.Target = target;
            result.AimDirection = dir;
            result.AimPoint = target.Position;
            return result;
        }
    }
}