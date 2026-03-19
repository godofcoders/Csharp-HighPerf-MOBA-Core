using System.Collections.Generic;
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

        public AIObjectivePoint GetBestObjective(Vector3 selfPosition, AIObjectiveType preferredType)
        {
            AIObjectivePoint best = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < _points.Count; i++)
            {
                var point = _points[i];
                if (point == null)
                    continue;

                float score = point.Weight;

                if (preferredType != AIObjectiveType.None && point.ObjectiveType == preferredType)
                    score += 25f;

                float distSq = (point.transform.position - selfPosition).sqrMagnitude;
                score -= distSq * 0.05f;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = point;
                }
            }

            return best;
        }
    }
}