using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public static class AbilityTargetResolver
    {
        private static readonly List<ISpatialEntity> _buffer = new List<ISpatialEntity>(32);

        public static BrawlerController ResolveSingleTarget(AbilityTargetRequest request)
        {
            if (request.Source == null || SimulationClock.Grid == null)
                return null;

            _buffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(request.Origin, request.Range, _buffer);

            BrawlerController bestTarget = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < _buffer.Count; i++)
            {
                ISpatialEntity entity = _buffer[i];
                if (entity is not BrawlerController candidate)
                    continue;

                if (!IsValidTarget(candidate, request))
                    continue;

                float score = ScoreCandidate(candidate, request);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        public static void ResolveTargetsInRadius(AbilityTargetRequest request, List<BrawlerController> results)
        {
            results.Clear();

            if (request.Source == null || SimulationClock.Grid == null)
                return;

            _buffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(request.Origin, request.Range, _buffer);

            for (int i = 0; i < _buffer.Count; i++)
            {
                ISpatialEntity entity = _buffer[i];
                if (entity is not BrawlerController candidate)
                    continue;

                if (!IsValidTarget(candidate, request))
                    continue;

                results.Add(candidate);
            }
        }

        private static bool IsValidTarget(BrawlerController candidate, AbilityTargetRequest request)
        {
            if (candidate == null || candidate.State == null)
                return false;

            if (request.RequireAlive && candidate.State.IsDead)
                return false;

            if (!request.IncludeSelf && candidate == request.Source)
                return false;

            switch (request.TeamRule)
            {
                case AbilityTargetTeamRule.Self:
                    return candidate == request.Source;

                case AbilityTargetTeamRule.Ally:
                    return candidate.Team == request.Source.Team;

                case AbilityTargetTeamRule.Enemy:
                    return candidate.Team != request.Source.Team;

                case AbilityTargetTeamRule.Any:
                    return true;

                default:
                    return false;
            }
        }

        private static float ScoreCandidate(BrawlerController candidate, AbilityTargetRequest request)
        {
            switch (request.SelectionRule)
            {
                case AbilityTargetSelectionRule.LowestHealth:
                    return candidate.State.CurrentHealth;

                case AbilityTargetSelectionRule.HighestHealth:
                    return -candidate.State.CurrentHealth;

                case AbilityTargetSelectionRule.Nearest:
                default:
                    return (candidate.Position - request.Origin).sqrMagnitude;
            }
        }
    }
}