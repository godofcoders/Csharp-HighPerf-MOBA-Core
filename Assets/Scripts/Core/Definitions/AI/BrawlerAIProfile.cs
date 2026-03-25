using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    [CreateAssetMenu(fileName = "BrawlerAIProfile", menuName = "MOBA/AI/Brawler AI Profile")]
    public class BrawlerAIProfile : ScriptableObject
    {
        [Header("Perception")]
        public float DetectionRadius = 10f;
        public uint MemoryDurationTicks = 90;
        public uint IdleSenseIntervalTicks = 10;
        public uint CombatSenseIntervalTicks = 3;

        [Header("Target Scoring")]
        public float DistanceWeight = 1f;
        public float CurrentTargetStickiness = 15f;
        public float LowHealthTargetBias = 25f;
        public float FinisherHealthThreshold = 0.35f;
        public float FinisherBonus = 20f;
        public float ThreatRange = 6f;
        public float ThreatBonus = 12f;
        public float ClusterTargetBonus = 10f;
        public float InRangeTargetBonus = 12f;

        [Header("Combat Distances")]
        public float AttackRangeBuffer = 0.75f;
        public float PreferredAttackRangeRatio = 0.85f;
        public float TooCloseRangeRatio = 0.45f;

        [Header("Attack / Ability Cadence")]
        public uint AttackCadenceTicks = 10;

        [Header("Gadget Usage")]
        public bool EnableGadgetUsage = true;
        public float GadgetLowHealthThreshold = 0.45f;
        public float GadgetEnemyDistanceThreshold = 4f;
        public uint GadgetCooldownTicks = 90;

        [Header("Retreat")]
        public float LowHealthRetreatRatio = 0.35f;
        public float RetreatStepDistance = 4f;

        [Header("Search / Shared Memory")]
        public uint SharedHotspotMemoryTicks = 120;

        [Header("Super Usage")]
        public bool EnableSuperUsage = true;
        public float SuperLowHealthTargetThreshold = 0.35f;
        public float SuperMinRangeRatio = 0.15f;
        public float SuperMaxRangeMultiplier = 1.2f;
        public int SuperMinClusterCount = 2;
        public uint SuperDecisionCooldownTicks = 20;

        [Header("Movement / Strafe")]
        public bool UseStrafe = true;
        public float StrafeDistance = 1.5f;
        public uint StrafeRetargetTicks = 15;
        public float RepositionStepDistance = 2.5f;

        [Header("Fallback Wander")]
        public float FallbackWanderRadius = 5f;
        public uint FallbackWanderRetargetTicks = 45;

        [Header("Utility Weights")]
        public float RetreatWeight = 1.0f;
        public float ApproachWeight = 1.0f;
        public float HoldRangeWeight = 1.0f;
        public float RepositionWeight = 1.0f;
        public float SearchWeight = 1.0f;
        public float WanderWeight = 1.0f;
        public float SuperWeight = 1.0f;

        [Header("Objective Preference")]
        public AIObjectiveType PreferredObjective = AIObjectiveType.MidControl;
        public float ObjectiveWeight = 35f;

        [Header("Team Tactics")]
        public float FocusFireWeight = 25f;
        public float RegroupWeight = 30f;
        public float PeelWeight = 20f;
        public float RegroupHealthThreshold = 0.35f;
        public float AllySupportRange = 8f;

        [Header("Spacing / Anti-Clump")]
        public float AllyAvoidanceRadius = 2.5f;
        public float AllyAvoidanceWeight = 1.5f;
        public float HoldRangePositionRefreshTicks = 20f;
        public float PreferredCombatOffset = 0.75f;

        public float GetPreferredAttackRange(float idealRange)
        {
            return Mathf.Max(0.5f, idealRange * PreferredAttackRangeRatio);
        }

        public float GetTooCloseDistance(float idealRange)
        {
            return Mathf.Max(0.5f, idealRange * TooCloseRangeRatio);
        }

        public void ApplyArchetypeDefaults(BrawlerArchetype archetype)
        {
            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                    RetreatWeight = 1.25f;
                    ApproachWeight = 0.75f;
                    HoldRangeWeight = 1.35f;
                    RepositionWeight = 1.15f;
                    SearchWeight = 1.0f;
                    WanderWeight = 0.8f;
                    SuperWeight = 1.1f;

                    PreferredObjective = AIObjectiveType.MidControl;
                    ObjectiveWeight = 45f;

                    FocusFireWeight = 30f;
                    RegroupWeight = 35f;
                    PeelWeight = 15f;
                    RegroupHealthThreshold = 0.45f;
                    AllySupportRange = 9f;

                    AllyAvoidanceRadius = 3.5f;
                    AllyAvoidanceWeight = 2.0f;
                    PreferredCombatOffset = 1.2f;

                    AttackCadenceTicks = 14;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.55f;
                    GadgetEnemyDistanceThreshold = 5f;
                    GadgetCooldownTicks = 120;
                    break;

                case BrawlerArchetype.Tank:
                    RetreatWeight = 0.7f;
                    ApproachWeight = 1.3f;
                    HoldRangeWeight = 0.9f;
                    RepositionWeight = 0.8f;
                    SearchWeight = 1.0f;
                    WanderWeight = 0.9f;
                    SuperWeight = 1.0f;

                    PreferredObjective = AIObjectiveType.HotZone;
                    ObjectiveWeight = 50f;

                    FocusFireWeight = 20f;
                    RegroupWeight = 18f;
                    PeelWeight = 35f;
                    RegroupHealthThreshold = 0.20f;
                    AllySupportRange = 10f;

                    AllyAvoidanceRadius = 2.0f;
                    AllyAvoidanceWeight = 0.8f;
                    PreferredCombatOffset = 0.3f;

                    AttackCadenceTicks = 8;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.35f;
                    GadgetEnemyDistanceThreshold = 3.5f;
                    GadgetCooldownTicks = 75;
                    break;

                case BrawlerArchetype.Assassin:
                    RetreatWeight = 0.85f;
                    ApproachWeight = 1.35f;
                    HoldRangeWeight = 0.75f;
                    RepositionWeight = 1.1f;
                    SearchWeight = 1.15f;
                    WanderWeight = 0.9f;
                    SuperWeight = 1.25f;

                    PreferredObjective = AIObjectiveType.LanePressure;
                    ObjectiveWeight = 40f;

                    FocusFireWeight = 28f;
                    RegroupWeight = 15f;
                    PeelWeight = 10f;
                    RegroupHealthThreshold = 0.25f;
                    AllySupportRange = 7f;

                    AllyAvoidanceRadius = 2.3f;
                    AllyAvoidanceWeight = 1.1f;
                    PreferredCombatOffset = 0.5f;

                    AttackCadenceTicks = 6;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.40f;
                    GadgetEnemyDistanceThreshold = 4.5f;
                    GadgetCooldownTicks = 60;
                    break;

                case BrawlerArchetype.Support:
                    RetreatWeight = 1.15f;
                    ApproachWeight = 0.85f;
                    HoldRangeWeight = 1.15f;
                    RepositionWeight = 1.2f;
                    SearchWeight = 1.05f;
                    WanderWeight = 0.9f;
                    SuperWeight = 1.15f;

                    PreferredObjective = AIObjectiveType.GemMine;
                    ObjectiveWeight = 42f;

                    FocusFireWeight = 22f;
                    RegroupWeight = 30f;
                    PeelWeight = 40f;
                    RegroupHealthThreshold = 0.40f;
                    AllySupportRange = 11f;

                    AllyAvoidanceRadius = 3.0f;
                    AllyAvoidanceWeight = 1.8f;
                    PreferredCombatOffset = 1.0f;

                    AttackCadenceTicks = 12;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.50f;
                    GadgetEnemyDistanceThreshold = 4f;
                    GadgetCooldownTicks = 105;
                    break;

                case BrawlerArchetype.Fighter:
                default:
                    RetreatWeight = 1.0f;
                    ApproachWeight = 1.0f;
                    HoldRangeWeight = 1.0f;
                    RepositionWeight = 1.0f;
                    SearchWeight = 1.0f;
                    WanderWeight = 1.0f;
                    SuperWeight = 1.0f;

                    PreferredObjective = AIObjectiveType.MidControl;
                    ObjectiveWeight = 35f;

                    FocusFireWeight = 25f;
                    RegroupWeight = 30f;
                    PeelWeight = 20f;
                    RegroupHealthThreshold = 0.35f;
                    AllySupportRange = 8f;

                    AllyAvoidanceRadius = 2.5f;
                    AllyAvoidanceWeight = 1.5f;
                    PreferredCombatOffset = 0.75f;

                    AttackCadenceTicks = 10;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.45f;
                    GadgetEnemyDistanceThreshold = 4f;
                    GadgetCooldownTicks = 90;
                    break;
            }
        }
    }
}