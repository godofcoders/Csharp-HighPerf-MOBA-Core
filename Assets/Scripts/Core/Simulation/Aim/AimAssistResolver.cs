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
                    return ResolveEnemy(request, AbilityTargetSelectionRule.Nearest);

                case AimAssistMode.NearestAlly:
                    return ResolveAlly(request, AbilityTargetSelectionRule.Nearest);

                case AimAssistMode.LowestHealthAlly:
                    return ResolveAlly(request, AbilityTargetSelectionRule.LowestHealth);

                case AimAssistMode.SmartOffense:
                    return ResolveEnemy(request, AbilityTargetSelectionRule.Nearest);

                case AimAssistMode.SmartSupport:
                    return ResolveAlly(request, AbilityTargetSelectionRule.LowestHealth);

                default:
                    return result;
            }
        }

        private static AimAssistResult ResolveEnemy(in AimAssistRequest request, AbilityTargetSelectionRule selectionRule)
        {
            return ResolveTarget(
                request,
                AbilityTargetTeamRule.Enemy,
                selectionRule);
        }

        private static AimAssistResult ResolveAlly(in AimAssistRequest request, AbilityTargetSelectionRule selectionRule)
        {
            return ResolveTarget(
                request,
                AbilityTargetTeamRule.Ally,
                selectionRule);
        }

        private static AimAssistResult ResolveTarget(
            in AimAssistRequest request,
            AbilityTargetTeamRule teamRule,
            AbilityTargetSelectionRule selectionRule)
        {
            AimAssistResult result = new AimAssistResult
            {
                HasResult = false,
                Target = null,
                AimDirection = request.Forward.sqrMagnitude > 0.001f ? request.Forward.normalized : request.Source.transform.forward,
                AimPoint = request.Origin
            };

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

            BrawlerController target = _buffer[0];
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