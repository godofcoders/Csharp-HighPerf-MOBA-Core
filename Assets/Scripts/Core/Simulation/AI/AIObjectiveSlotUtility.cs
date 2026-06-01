using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public enum AIObjectiveSlotRole
    {
        Default,
        Contest,
        Breaker,
        Anchor,
        Perimeter,
        Flank,
        Pressure
    }

    /// <summary>
    /// Provides stable tactical positions around an objective.
    ///
    /// This prevents all bots from moving to the exact same objective center.
    /// Instead, each bot gets a deterministic slot based on team, archetype,
    /// and entity id.
    /// </summary>
    public static class AIObjectiveSlotUtility
    {
        private const float FrontSlotDistance = 1.4f;
        private const float SideSlotDistance = 2.2f;
        private const float BackSlotDistance = 3.0f;
        private const float FlankSlotDistance = 3.4f;

        public static Vector3 GetObjectiveSlotPosition(
            BrawlerController self,
            BrawlerAIProfile profile,
            Vector3 objectivePosition)
        {
            return GetObjectiveSlotPosition(
                self,
                profile,
                new AIObjectiveCandidate(
                    AIObjectiveType.None,
                    objectivePosition,
                    0f,
                    3f,
                    "Objective",
                    false));
        }

        public static Vector3 GetObjectiveSlotPosition(
            BrawlerController self,
            BrawlerAIProfile profile,
            AIObjectiveCandidate objective)
        {
            if (self == null)
                return objective.Position;

            BrawlerArchetype archetype = profile != null
                ? profile.Archetype
                : BrawlerArchetype.Fighter;
            AIObjectiveSlotRole slotRole = GetObjectiveSlotRole(
                archetype,
                objective);

            return GetObjectiveSlotPosition(
                self.Team,
                archetype,
                self.EntityID,
                objective.Position,
                objective.Radius,
                slotRole,
                objective.FriendlyPresence,
                objective.EnemyPresence);
        }

        public static Vector3 GetObjectiveSlotPosition(
            TeamType team,
            BrawlerArchetype archetype,
            int entityId,
            Vector3 objectivePosition,
            float objectiveRadius)
        {
            return GetObjectiveSlotPosition(
                team,
                archetype,
                entityId,
                objectivePosition,
                objectiveRadius,
                AIObjectiveSlotRole.Default,
                0,
                0);
        }

        public static Vector3 GetObjectiveSlotPosition(
            TeamType team,
            BrawlerArchetype archetype,
            int entityId,
            Vector3 objectivePosition,
            float objectiveRadius,
            AIObjectiveControlState controlState,
            int friendlyPresence,
            int enemyPresence)
        {
            return GetObjectiveSlotPosition(
                team,
                archetype,
                entityId,
                objectivePosition,
                objectiveRadius,
                GetObjectiveSlotRole(
                    archetype,
                    controlState,
                    friendlyPresence,
                    enemyPresence),
                friendlyPresence,
                enemyPresence);
        }

        public static Vector3 GetObjectiveSlotPosition(
            TeamType team,
            BrawlerArchetype archetype,
            int entityId,
            Vector3 objectivePosition,
            float objectiveRadius,
            AIObjectiveSlotRole slotRole,
            int friendlyPresence,
            int enemyPresence)
        {
            Vector3 teamForward = GetTeamForward(team);
            Vector3 teamRight = new Vector3(teamForward.z, 0f, -teamForward.x);
            int sideSign = GetStableSideSign(entityId);
            float radiusScale = GetRadiusScale(objectiveRadius);

            Vector3 offset = GetArchetypeOffset(
                archetype,
                teamForward,
                teamRight,
                sideSign,
                radiusScale);

            offset = ApplySlotRoleOffset(
                offset,
                slotRole,
                teamForward,
                teamRight,
                sideSign,
                radiusScale,
                friendlyPresence,
                enemyPresence);

            return objectivePosition + offset;
        }

        public static AIObjectiveSlotRole GetObjectiveSlotRole(
            BrawlerArchetype archetype,
            AIObjectiveCandidate objective)
        {
            return GetObjectiveSlotRole(
                archetype,
                objective.ControlState,
                objective.FriendlyPresence,
                objective.EnemyPresence);
        }

        public static AIObjectiveSlotRole GetObjectiveSlotRole(
            BrawlerArchetype archetype,
            AIObjectiveControlState controlState,
            int friendlyPresence,
            int enemyPresence)
        {
            bool friendlySaturated =
                friendlyPresence >= enemyPresence + 2;

            switch (controlState)
            {
                case AIObjectiveControlState.EnemyControlled:
                    if (IsFrontline(archetype))
                        return AIObjectiveSlotRole.Breaker;

                    return archetype == BrawlerArchetype.Assassin
                        ? AIObjectiveSlotRole.Flank
                        : AIObjectiveSlotRole.Pressure;

                case AIObjectiveControlState.Contested:
                    if (friendlySaturated && archetype != BrawlerArchetype.Tank)
                        return AIObjectiveSlotRole.Perimeter;

                    if (IsFrontline(archetype) ||
                        archetype == BrawlerArchetype.Controller)
                    {
                        return AIObjectiveSlotRole.Contest;
                    }

                    return archetype == BrawlerArchetype.Assassin
                        ? AIObjectiveSlotRole.Flank
                        : AIObjectiveSlotRole.Perimeter;

                case AIObjectiveControlState.FriendlyControlled:
                    if (IsFrontline(archetype) ||
                        archetype == BrawlerArchetype.Controller)
                    {
                        return AIObjectiveSlotRole.Anchor;
                    }

                    return archetype == BrawlerArchetype.Assassin
                        ? AIObjectiveSlotRole.Flank
                        : AIObjectiveSlotRole.Perimeter;

                case AIObjectiveControlState.Neutral:
                    if (IsFrontline(archetype) ||
                        archetype == BrawlerArchetype.Controller)
                    {
                        return AIObjectiveSlotRole.Contest;
                    }

                    return archetype == BrawlerArchetype.Assassin
                        ? AIObjectiveSlotRole.Flank
                        : AIObjectiveSlotRole.Perimeter;

                default:
                    return AIObjectiveSlotRole.Default;
            }
        }

        private static Vector3 GetArchetypeOffset(
            BrawlerArchetype archetype,
            Vector3 teamForward,
            Vector3 teamRight,
            int sideSign,
            float radiusScale)
        {
            float frontDistance = FrontSlotDistance * radiusScale;
            float sideDistance = SideSlotDistance * radiusScale;
            float backDistance = BackSlotDistance * radiusScale;
            float flankDistance = FlankSlotDistance * radiusScale;

            switch (archetype)
            {
                case BrawlerArchetype.Tank:
                    // Tanks contest close to the front/center.
                    return teamForward * frontDistance;

                case BrawlerArchetype.Fighter:
                    // Fighters can contest slightly off-center.
                    return teamForward * frontDistance +
                           teamRight * sideSign * 1.2f * radiusScale;

                case BrawlerArchetype.Support:
                    // Supports stay behind the contest point.
                    return -teamForward * backDistance +
                           teamRight * sideSign * 1.4f * radiusScale;

                case BrawlerArchetype.Sniper:
                    // Snipers hold back angles.
                    return -teamForward * backDistance +
                           teamRight * sideSign * sideDistance;

                case BrawlerArchetype.Artillery:
                    // Artillery prefers safe back-side positions.
                    return -teamForward * (backDistance + 0.8f * radiusScale) +
                           teamRight * sideSign * sideDistance;

                case BrawlerArchetype.Controller:
                    // Controllers hold side pressure near the objective.
                    return teamRight * sideSign * sideDistance;

                case BrawlerArchetype.Assassin:
                    // Assassins should not sit center; they prefer flank angles.
                    return teamForward * 0.8f * radiusScale +
                           teamRight * sideSign * flankDistance;

                default:
                    return teamRight * sideSign * 1.5f * radiusScale;
            }
        }

        private static Vector3 ApplySlotRoleOffset(
            Vector3 baseOffset,
            AIObjectiveSlotRole slotRole,
            Vector3 teamForward,
            Vector3 teamRight,
            int sideSign,
            float radiusScale,
            int friendlyPresence,
            int enemyPresence)
        {
            friendlyPresence = Mathf.Max(0, friendlyPresence);
            enemyPresence = Mathf.Max(0, enemyPresence);

            float friendlySaturation =
                Mathf.Clamp(friendlyPresence - enemyPresence, 0, 3) *
                0.35f *
                radiusScale;
            float enemyPressure =
                Mathf.Clamp(enemyPresence - friendlyPresence, 0, 3) *
                0.25f *
                radiusScale;

            switch (slotRole)
            {
                case AIObjectiveSlotRole.Breaker:
                    return baseOffset * 0.82f +
                           teamForward * (0.35f * radiusScale + enemyPressure);

                case AIObjectiveSlotRole.Contest:
                    return baseOffset * 0.92f +
                           teamRight * sideSign * friendlySaturation;

                case AIObjectiveSlotRole.Anchor:
                    return baseOffset -
                           teamForward * (0.55f * radiusScale + friendlySaturation);

                case AIObjectiveSlotRole.Perimeter:
                    return baseOffset * 1.16f -
                           teamForward * 0.35f * radiusScale +
                           teamRight * sideSign * friendlySaturation;

                case AIObjectiveSlotRole.Flank:
                    return baseOffset * 1.08f +
                           teamRight * sideSign * (0.65f * radiusScale + enemyPressure);

                case AIObjectiveSlotRole.Pressure:
                    return baseOffset * 1.04f +
                           teamForward * (0.25f * radiusScale + enemyPressure) +
                           teamRight * sideSign * 0.35f * radiusScale;

                default:
                    return baseOffset;
            }
        }

        private static float GetRadiusScale(float objectiveRadius)
        {
            return Mathf.Clamp(Mathf.Max(0.5f, objectiveRadius) / 3f, 0.70f, 1.65f);
        }

        private static bool IsFrontline(BrawlerArchetype archetype)
        {
            return archetype == BrawlerArchetype.Tank ||
                   archetype == BrawlerArchetype.Fighter;
        }

        private static Vector3 GetTeamForward(TeamType team)
        {
            // Assumption:
            // Blue generally pushes toward +Z, Red generally pushes toward -Z.
            // If your map uses a different orientation, change this one method only.
            switch (team)
            {
                case TeamType.Blue:
                    return Vector3.forward;

                case TeamType.Red:
                    return Vector3.back;

                default:
                    return Vector3.forward;
            }
        }

        private static int GetStableSideSign(int entityId)
        {
            return (entityId % 2 == 0) ? 1 : -1;
        }
    }
}
