using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIObjectiveMemory
    {
        private readonly List<AIObjectivePoint> _points = new List<AIObjectivePoint>(16);

        public void Register(AIObjectivePoint point)
        {
            if (point == null || _points.Contains(point))
                return;

            _points.Add(point);
        }

        public void Unregister(AIObjectivePoint point)
        {
            if (point == null)
                return;

            _points.Remove(point);
        }

        public bool TryGetBestObjective(
            Vector3 selfPosition,
            AIObjectiveType preferredType,
            TeamType team,
            out AIObjectiveCandidate objective)
        {
            objective = default;

            bool found = false;
            float bestScore = float.MinValue;

            for (int i = 0; i < _points.Count; i++)
            {
                AIObjectivePoint point = _points[i];
                if (point == null)
                    continue;

                AIObjectiveCandidate candidate =
                    AIObjectiveCandidate.FromPoint(point);
                float score = ScoreCandidate(candidate, selfPosition, preferredType);

                if (!found || score > bestScore)
                {
                    bestScore = score;
                    objective = candidate;
                    found = true;
                }
            }

            if (TryGetRuntimeObjective(
                    team,
                    preferredType,
                    selfPosition,
                    out AIObjectiveCandidate runtimeObjective))
            {
                float runtimeScore = ScoreCandidate(
                    runtimeObjective,
                    selfPosition,
                    preferredType);

                if (!found || runtimeScore > bestScore)
                {
                    objective = runtimeObjective;
                    found = true;
                }
            }

            return found;
        }

        private static bool TryGetRuntimeObjective(
            TeamType team,
            AIObjectiveType preferredType,
            Vector3 selfPosition,
            out AIObjectiveCandidate objective)
        {
            objective = default;

            if (team == TeamType.Neutral ||
                !ServiceProvider.TryGet<IAIRuntimeObjectiveProvider>(out var provider))
            {
                return false;
            }

            if (provider is UnityEngine.Object unityProvider && unityProvider == null)
            {
                ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>();
                return false;
            }

            return provider != null &&
                   provider.TryGetRuntimeObjective(
                       team,
                       preferredType,
                       selfPosition,
                       out objective);
        }

        private static float ScoreCandidate(
            AIObjectiveCandidate candidate,
            Vector3 selfPosition,
            AIObjectiveType preferredType)
        {
            float score = candidate.Weight;

            if (preferredType != AIObjectiveType.None &&
                candidate.ObjectiveType == preferredType)
            {
                score += 25f;
            }

            float distSq = (candidate.Position - selfPosition).sqrMagnitude;
            score -= distSq * 0.05f;

            if (candidate.IsRuntime)
                score += 6f;

            return score;
        }
    }
}
