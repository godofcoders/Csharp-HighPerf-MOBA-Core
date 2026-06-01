using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIObjectiveCandidate
    {
        public readonly AIObjectiveType ObjectiveType;
        public readonly Vector3 Position;
        public readonly float Weight;
        public readonly float Radius;
        public readonly string Name;
        public readonly bool IsRuntime;

        public AIObjectiveCandidate(
            AIObjectiveType objectiveType,
            Vector3 position,
            float weight,
            float radius,
            string name,
            bool isRuntime)
        {
            ObjectiveType = objectiveType;
            Position = position;
            Weight = weight;
            Radius = Mathf.Max(0.5f, radius);
            Name = string.IsNullOrEmpty(name) ? objectiveType.ToString() : name;
            IsRuntime = isRuntime;
        }

        public static AIObjectiveCandidate FromPoint(AIObjectivePoint point)
        {
            return new AIObjectiveCandidate(
                point.ObjectiveType,
                point.transform.position,
                point.Weight,
                point.Radius,
                point.name,
                false);
        }
    }
}
