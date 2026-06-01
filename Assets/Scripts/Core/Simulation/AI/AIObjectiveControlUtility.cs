using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public static class AIObjectiveControlUtility
    {
        public static AIObjectiveControlState ResolveForTeam(
            TeamType controllingTeam,
            TeamType selfTeam,
            int friendlyPresence,
            int enemyPresence)
        {
            if (selfTeam == TeamType.Neutral)
                return AIObjectiveControlState.Unknown;

            friendlyPresence = Mathf.Max(0, friendlyPresence);
            enemyPresence = Mathf.Max(0, enemyPresence);

            if (friendlyPresence > 0 && enemyPresence > 0)
                return AIObjectiveControlState.Contested;

            if (controllingTeam == selfTeam)
                return AIObjectiveControlState.FriendlyControlled;

            if (controllingTeam != TeamType.Neutral)
                return AIObjectiveControlState.EnemyControlled;

            if (enemyPresence > 0)
                return AIObjectiveControlState.EnemyControlled;

            if (friendlyPresence > 0)
                return AIObjectiveControlState.FriendlyControlled;

            return AIObjectiveControlState.Neutral;
        }

        public static float GetSelectionScoreDelta(AIObjectiveControlState controlState)
        {
            switch (controlState)
            {
                case AIObjectiveControlState.EnemyControlled:
                    return 14f;

                case AIObjectiveControlState.Contested:
                    return 12f;

                case AIObjectiveControlState.Neutral:
                    return 5f;

                case AIObjectiveControlState.FriendlyControlled:
                    return -8f;

                default:
                    return 0f;
            }
        }

        public static float GetUtilityScoreDelta(AIObjectiveControlState controlState)
        {
            switch (controlState)
            {
                case AIObjectiveControlState.EnemyControlled:
                    return 18f;

                case AIObjectiveControlState.Contested:
                    return 16f;

                case AIObjectiveControlState.Neutral:
                    return 7f;

                case AIObjectiveControlState.FriendlyControlled:
                    return -10f;

                default:
                    return 0f;
            }
        }

        public static float GetSelectionPresenceDelta(
            int friendlyPresence,
            int enemyPresence)
        {
            return GetPresenceDelta(
                friendlyPresence,
                enemyPresence,
                1.5f,
                -5f,
                7f);
        }

        public static float GetUtilityPresenceDelta(
            int friendlyPresence,
            int enemyPresence)
        {
            return GetPresenceDelta(
                friendlyPresence,
                enemyPresence,
                2.5f,
                -8f,
                10f);
        }

        private static float GetPresenceDelta(
            int friendlyPresence,
            int enemyPresence,
            float perPresence,
            float minDelta,
            float maxDelta)
        {
            friendlyPresence = Mathf.Max(0, friendlyPresence);
            enemyPresence = Mathf.Max(0, enemyPresence);

            return Mathf.Clamp(
                (enemyPresence - friendlyPresence) * perPresence,
                minDelta,
                maxDelta);
        }
    }
}
