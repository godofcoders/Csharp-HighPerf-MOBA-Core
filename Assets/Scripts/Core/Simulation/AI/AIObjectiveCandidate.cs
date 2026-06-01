using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public enum AIObjectiveControlState
    {
        Unknown,
        Neutral,
        FriendlyControlled,
        EnemyControlled,
        Contested
    }

    public readonly struct AIObjectiveCandidate
    {
        public readonly AIObjectiveType ObjectiveType;
        public readonly Vector3 Position;
        public readonly float Weight;
        public readonly float Radius;
        public readonly string Name;
        public readonly bool IsRuntime;
        public readonly AIObjectiveControlState ControlState;
        public readonly int FriendlyPresence;
        public readonly int EnemyPresence;

        public AIObjectiveCandidate(
            AIObjectiveType objectiveType,
            Vector3 position,
            float weight,
            float radius,
            string name,
            bool isRuntime,
            AIObjectiveControlState controlState = AIObjectiveControlState.Unknown,
            int friendlyPresence = 0,
            int enemyPresence = 0)
        {
            ObjectiveType = objectiveType;
            Position = position;
            Weight = weight;
            Radius = Mathf.Max(0.5f, radius);
            Name = string.IsNullOrEmpty(name) ? objectiveType.ToString() : name;
            IsRuntime = isRuntime;
            ControlState = controlState;
            FriendlyPresence = Mathf.Max(0, friendlyPresence);
            EnemyPresence = Mathf.Max(0, enemyPresence);
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
