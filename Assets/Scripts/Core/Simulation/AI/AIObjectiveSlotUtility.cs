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
            if (self == null)
                return objectivePosition;

            Vector3 teamForward = GetTeamForward(self.Team);
            Vector3 teamRight = new Vector3(teamForward.z, 0f, -teamForward.x);

            int sideSign = GetStableSideSign(self);

            BrawlerArchetype archetype = profile != null
                ? profile.Archetype
                : BrawlerArchetype.Fighter;

            Vector3 offset = GetArchetypeOffset(
                archetype,
                teamForward,
                teamRight,
                sideSign);

            return objectivePosition + offset;
        }

        private static Vector3 GetArchetypeOffset(
            BrawlerArchetype archetype,
            Vector3 teamForward,
            Vector3 teamRight,
            int sideSign)
        {
            switch (archetype)
            {
                case BrawlerArchetype.Tank:
                    // Tanks contest close to the front/center.
                    return teamForward * FrontSlotDistance;

                case BrawlerArchetype.Fighter:
                    // Fighters can contest slightly off-center.
                    return teamForward * FrontSlotDistance +
                           teamRight * sideSign * 1.2f;

                case BrawlerArchetype.Support:
                    // Supports stay behind the contest point.
                    return -teamForward * BackSlotDistance +
                           teamRight * sideSign * 1.4f;

                case BrawlerArchetype.Sniper:
                    // Snipers hold back angles.
                    return -teamForward * BackSlotDistance +
                           teamRight * sideSign * SideSlotDistance;

                case BrawlerArchetype.Artillery:
                    // Artillery prefers safe back-side positions.
                    return -teamForward * (BackSlotDistance + 0.8f) +
                           teamRight * sideSign * SideSlotDistance;

                case BrawlerArchetype.Controller:
                    // Controllers hold side pressure near the objective.
                    return teamRight * sideSign * SideSlotDistance;

                case BrawlerArchetype.Assassin:
                    // Assassins should not sit center; they prefer flank angles.
                    return teamForward * 0.8f +
                           teamRight * sideSign * FlankSlotDistance;

                default:
                    return teamRight * sideSign * 1.5f;
            }
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
            return (self.EntityID % 2u == 0u) ? 1 : -1;
        }
    }
}