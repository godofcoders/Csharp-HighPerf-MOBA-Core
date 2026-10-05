using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public static class ShowdownPoisonAvoidanceUtility
    {
        public static Vector3 ClampDestinationToSafeZone(
            Vector3 destination,
            Vector3 center,
            Vector2 halfExtents,
            float safetyInset)
        {
            float inset = Mathf.Max(0f, safetyInset);
            float safeHalfX = Mathf.Max(0.1f, Mathf.Abs(halfExtents.x) - inset);
            float safeHalfZ = Mathf.Max(0.1f, Mathf.Abs(halfExtents.y) - inset);

            destination.x = Mathf.Clamp(
                destination.x,
                center.x - safeHalfX,
                center.x + safeHalfX);
            destination.z = Mathf.Clamp(
                destination.z,
                center.z - safeHalfZ,
                center.z + safeHalfZ);
            return destination;
        }

        public static float CalculateActionPenalty(
            AIActionType actionType,
            float selfDistanceBeyondSafeZone,
            float selfEdgeDistance,
            float dangerBuffer,
            bool hasLiveTarget,
            float targetDistanceBeyondSafeZone)
        {
            float penalty = 0f;
            float outsideDistance = Mathf.Max(0f, selfDistanceBeyondSafeZone);
            bool selfOutside = outsideDistance > 0f;
            bool nearEdge = selfEdgeDistance <= Mathf.Max(0f, dangerBuffer);

            if (selfOutside && IsNonEscapeAction(actionType))
            {
                float outsidePenalty = Mathf.Min(96f, 58f + outsideDistance * 16f);
                penalty -= outsidePenalty;
            }
            else if (nearEdge)
            {
                switch (actionType)
                {
                    case AIActionType.Wander:
                        penalty -= 36f;
                        break;
                    case AIActionType.Search:
                        penalty -= 30f;
                        break;
                    case AIActionType.Approach:
                    case AIActionType.HoldRange:
                    case AIActionType.Reposition:
                        penalty -= 18f;
                        break;
                }
            }

            if (hasLiveTarget && targetDistanceBeyondSafeZone > 0f)
            {
                float pursuitPenalty = Mathf.Min(
                    86f,
                    48f + targetDistanceBeyondSafeZone * 14f);

                switch (actionType)
                {
                    case AIActionType.Approach:
                        penalty -= pursuitPenalty;
                        break;
                    case AIActionType.HoldRange:
                    case AIActionType.Reposition:
                        penalty -= pursuitPenalty * 0.72f;
                        break;
                    case AIActionType.UseSuper:
                    case AIActionType.Peel:
                        penalty -= pursuitPenalty * 0.48f;
                        break;
                }
            }

            return Mathf.Min(0f, penalty);
        }

        private static bool IsNonEscapeAction(AIActionType actionType)
        {
            return actionType != AIActionType.Objective &&
                   actionType != AIActionType.Retreat &&
                   actionType != AIActionType.Evade;
        }
    }
}
