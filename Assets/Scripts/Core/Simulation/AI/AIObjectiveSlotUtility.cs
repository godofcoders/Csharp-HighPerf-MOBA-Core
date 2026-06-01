using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
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

            return GetObjectiveSlotPosition(
                self.Team,
                archetype,
                self.EntityID,
                objective.Position,
                objective.Radius);
        }

        public static Vector3 GetObjectiveSlotPosition(
            TeamType team,
            BrawlerArchetype archetype,
            uint entityId,
            Vector3 objectivePosition,
            float objectiveRadius)
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

            return objectivePosition + offset;
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

        private static float GetRadiusScale(float objectiveRadius)
        {
            return Mathf.Clamp(Mathf.Max(0.5f, objectiveRadius) / 3f, 0.70f, 1.65f);
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

        private static int GetStableSideSign(BrawlerController self)
        {
            // Stable split so bots do not randomly flip sides every frame.
            // Even id → right side, odd id → left side.
            return GetStableSideSign(self.EntityID);
        }

        private static int GetStableSideSign(uint entityId)
        {
            return (entityId % 2u == 0u) ? 1 : -1;
        }
    }
}
