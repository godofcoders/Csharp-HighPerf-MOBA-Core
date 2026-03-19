using UnityEngine;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewBrawlerAIProfile", menuName = "MOBA/AI/Brawler AI Profile")]
    public class BrawlerAIProfile : ScriptableObject
    {
        [Header("Identity")]
        public AIArchetype Archetype = AIArchetype.Balanced;

        [Header("Perception")]
        public float DetectionRadius = 40f;
        public uint CombatSenseIntervalTicks = 4;
        public uint IdleSenseIntervalTicks = 12;
        public uint MemoryDurationTicks = 60;
        public uint SharedHotspotMemoryTicks = 120;

        [Header("Combat Distance")]
        public float PreferredRangeMultiplier = 0.90f;
        public float TooCloseRangeMultiplier = 0.45f;
        public float AttackRangeBuffer = 1.0f;
        public float LeashDistance = 30f;

        [Header("Health / Courage")]
        [Range(0.05f, 0.95f)]
        public float LowHealthRetreatRatio = 0.30f;

        [Header("Combat Cadence")]
        public uint AttackCadenceTicks = 30;

        [Header("Movement")]
        public bool UseStrafe = true;
        public float StrafeDistance = 2f;
        public uint StrafeRetargetTicks = 25;
        public float RepositionStepDistance = 2.5f;
        public float RetreatStepDistance = 5f;

        [Header("Patrol / Idle")]
        public float PatrolArrivalDistance = 1f;
        public float FallbackWanderRadius = 8f;
        public uint FallbackWanderRetargetTicks = 120;

        [Header("Target Selection Weights")]
        public float CurrentTargetStickiness = 20f;
        public float LowHealthTargetBias = 10f;
        public float DistanceWeight = 1f;
        public float ThreatBonus = 8f;
        public float ThreatRange = 8f;
        public float FinisherHealthThreshold = 0.30f;
        public float FinisherBonus = 12f;
        public float ClusterTargetBonus = 6f;
        public float InRangeTargetBonus = 4f;

        [Header("Gadget Usage")]
        public bool EnableGadgetUsage = true;
        public float GadgetLowHealthThreshold = 0.35f;
        public float GadgetEnemyDistanceThreshold = 4f;
        public uint GadgetCooldownTicks = 120;

        [Header("Super Usage")]
        public bool EnableSuperUsage = true;
        public float SuperMinRangeRatio = 0.35f;
        public float SuperMaxRangeMultiplier = 1.1f;
        public float SuperLowHealthTargetThreshold = 0.40f;
        public int SuperMinClusterCount = 2;
        public uint SuperDecisionCooldownTicks = 45;

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

        public float GetPreferredAttackRange(float abilityIdealRange)
        {
            return Mathf.Max(1f, abilityIdealRange * PreferredRangeMultiplier);
        }

        public float GetTooCloseDistance(float abilityIdealRange)
        {
            return Mathf.Max(0.75f, abilityIdealRange * TooCloseRangeMultiplier);
        }

        public void ApplyArchetypeDefaults()
        {
            switch (Archetype)
            {
                case AIArchetype.Sniper:
                    RetreatWeight = 1.25f;
                    ApproachWeight = 0.75f;
                    HoldRangeWeight = 1.35f;
                    RepositionWeight = 1.15f;
                    SearchWeight = 1.0f;
                    WanderWeight = 0.8f;
                    SuperWeight = 1.1f;
                    PreferredObjective = AIObjectiveType.MidControl;
                    ObjectiveWeight = 45f;
                    break;

                case AIArchetype.Tank:
                    RetreatWeight = 0.7f;
                    ApproachWeight = 1.3f;
                    HoldRangeWeight = 0.9f;
                    RepositionWeight = 0.8f;
                    SearchWeight = 1.0f;
                    WanderWeight = 0.9f;
                    SuperWeight = 1.0f;
                    PreferredObjective = AIObjectiveType.HotZone;
                    ObjectiveWeight = 50f;
                    break;

                case AIArchetype.Assassin:
                    RetreatWeight = 0.85f;
                    ApproachWeight = 1.35f;
                    HoldRangeWeight = 0.75f;
                    RepositionWeight = 1.1f;
                    SearchWeight = 1.15f;
                    WanderWeight = 0.9f;
                    SuperWeight = 1.25f;
                    PreferredObjective = AIObjectiveType.LanePressure;
                    ObjectiveWeight = 40f;
                    break;

                case AIArchetype.Support:
                    RetreatWeight = 1.15f;
                    ApproachWeight = 0.85f;
                    HoldRangeWeight = 1.15f;
                    RepositionWeight = 1.2f;
                    SearchWeight = 1.05f;
                    WanderWeight = 0.9f;
                    SuperWeight = 1.15f;
                    PreferredObjective = AIObjectiveType.GemMine;
                    ObjectiveWeight = 42f;
                    break;

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
                    break;
            }
        }
    }
}