using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    [CreateAssetMenu(fileName = "BrawlerAIProfile", menuName = "MOBA/AI/Brawler AI Profile")]
    public class BrawlerAIProfile : ScriptableObject
    {
        [Header("Brawler Role")]
        public BrawlerArchetype Archetype = BrawlerArchetype.Fighter;

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
        public float ObjectiveWeight = 1f;

        [Header("Team Tactics")]
        public float FocusFireWeight = 20f;
        public float RegroupWeight = 1f;
        public float PeelWeight = 1f;
        public float RegroupHealthThreshold = 0.35f;
        public float AllySupportRange = 8f;
        [Tooltip("How many allies can already focus a target before this bot starts preferring other viable targets.")]
        public int TargetFocusSoftLimit = 1;
        [Tooltip("Target score removed for each allied focus beyond TargetFocusSoftLimit.")]
        public float OverFocusedTargetPenaltyPerAlly = 16f;
        [Tooltip("Maximum target score removed for over-focused targets. Keeps focus-fire possible for urgent kills.")]
        public float MaxOverFocusedTargetPenalty = 36f;

        [Header("Spacing / Anti-Clump")]
        public float AllyAvoidanceRadius = 2.5f;
        public float AllyAvoidanceWeight = 1.5f;
        public float HoldRangePositionRefreshTicks = 20f;
        public float PreferredCombatOffset = 0.75f;

        [Header("Action Commitment")]
        [Tooltip("Minimum score needed before an action can be committed. Prevents tiny scores from controlling the bot.")]
        public float MinimumCommittedActionScore = 8f;

        [Tooltip("How much better a new action must be before replacing the current action.")]
        public float ActionSwitchScoreMargin = 12f;

        [Tooltip("Ticks to keep combat actions unless another action is clearly better.")]
        public uint CombatActionCommitmentTicks = 8;

        [Tooltip("Ticks to keep non-combat actions unless another action is clearly better.")]
        public uint NonCombatActionCommitmentTicks = 18;

        [Tooltip("Score where urgent actions can override commitment immediately.")]
        public float EmergencyOverrideScore = 90f;

        [Tooltip("If true, prints action switch decisions for AI debugging.")]
        public bool LogActionCommitment = false;

        [Header("Objective Debug")]
        public bool LogObjectiveDebug = false;

        [Header("Tactical Movement")]
        [Tooltip("How far the bot can side-step while holding range.")]
        public float TacticalStrafeDistance = 1.8f;

        [Tooltip("How far the bot backs up when kiting.")]
        public float TacticalKiteDistance = 2.5f;

        [Tooltip("How often the bot is allowed to pick a new tactical combat destination.")]
        public uint TacticalMoveRetargetTicks = 10;

        [Tooltip("Extra distance fragile brawlers try to keep from enemies.")]
        public float FragileRangePadding = 0.75f;

        [Tooltip("If true, logs tactical movement decisions.")]
        public bool LogTacticalMovement = false;

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
                    ObjectiveWeight = 1f;

                    FocusFireWeight = 25f;
                    RegroupWeight = 1.2f;
                    PeelWeight = 0.8f;
                    RegroupHealthThreshold = 0.45f;
                    AllySupportRange = 9f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 18f;
                    MaxOverFocusedTargetPenalty = 40f;

                    AllyAvoidanceRadius = 3.5f;
                    AllyAvoidanceWeight = 2.0f;
                    PreferredCombatOffset = 1.2f;

                    AttackCadenceTicks = 14;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.55f;
                    GadgetEnemyDistanceThreshold = 5f;
                    GadgetCooldownTicks = 120;

                    TacticalStrafeDistance = 2.2f;
                    TacticalKiteDistance = 3.0f;
                    TacticalMoveRetargetTicks = 12;
                    FragileRangePadding = 1.0f;
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
                    ObjectiveWeight = 1.25f;

                    FocusFireWeight = 18f;
                    RegroupWeight = 0.8f;
                    PeelWeight = 1.35f;
                    RegroupHealthThreshold = 0.20f;
                    AllySupportRange = 10f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 12f;
                    MaxOverFocusedTargetPenalty = 28f;

                    AllyAvoidanceRadius = 2.0f;
                    AllyAvoidanceWeight = 0.8f;
                    PreferredCombatOffset = 0.3f;

                    AttackCadenceTicks = 8;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.35f;
                    GadgetEnemyDistanceThreshold = 3.5f;
                    GadgetCooldownTicks = 75;

                    TacticalStrafeDistance = 1.2f;
                    TacticalKiteDistance = 1.6f;
                    TacticalMoveRetargetTicks = 10;
                    FragileRangePadding = 0.0f;
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
                    ObjectiveWeight = 0.85f;

                    FocusFireWeight = 25f;
                    RegroupWeight = 0.7f;
                    PeelWeight = 0.65f;
                    RegroupHealthThreshold = 0.25f;
                    AllySupportRange = 7f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 20f;
                    MaxOverFocusedTargetPenalty = 44f;

                    AllyAvoidanceRadius = 2.3f;
                    AllyAvoidanceWeight = 1.1f;
                    PreferredCombatOffset = 0.5f;

                    AttackCadenceTicks = 6;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.40f;
                    GadgetEnemyDistanceThreshold = 4.5f;
                    GadgetCooldownTicks = 60;

                    TacticalStrafeDistance = 1.8f;
                    TacticalKiteDistance = 2.0f;
                    TacticalMoveRetargetTicks = 8;
                    FragileRangePadding = 0.2f;
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
                    ObjectiveWeight = 1.0f;

                    FocusFireWeight = 18f;
                    RegroupWeight = 1.15f;
                    PeelWeight = 1.4f;
                    RegroupHealthThreshold = 0.40f;
                    AllySupportRange = 11f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 18f;
                    MaxOverFocusedTargetPenalty = 40f;

                    AllyAvoidanceRadius = 3.0f;
                    AllyAvoidanceWeight = 1.8f;
                    PreferredCombatOffset = 1.0f;

                    AttackCadenceTicks = 12;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.50f;
                    GadgetEnemyDistanceThreshold = 4f;
                    GadgetCooldownTicks = 105;

                    TacticalStrafeDistance = 2.0f;
                    TacticalKiteDistance = 2.8f;
                    TacticalMoveRetargetTicks = 12;
                    FragileRangePadding = 0.85f;
                    break;

                case BrawlerArchetype.Controller:
                    // Zone-control specialists: turrets, crowd-control, map-presence
                    // super. Mid-range play, HotZone-oriented, values super uptime.
                    RetreatWeight = 1.05f;
                    ApproachWeight = 0.9f;
                    HoldRangeWeight = 1.2f;
                    RepositionWeight = 1.1f;
                    SearchWeight = 1.0f;
                    WanderWeight = 0.9f;
                    SuperWeight = 1.3f;

                    PreferredObjective = AIObjectiveType.HotZone;
                    ObjectiveWeight = 1.2f;

                    FocusFireWeight = 24f;
                    RegroupWeight = 0.9f;
                    PeelWeight = 1.05f;
                    RegroupHealthThreshold = 0.35f;
                    AllySupportRange = 8.5f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 16f;
                    MaxOverFocusedTargetPenalty = 36f;

                    AllyAvoidanceRadius = 2.5f;
                    AllyAvoidanceWeight = 1.5f;
                    PreferredCombatOffset = 0.75f;

                    AttackCadenceTicks = 11;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.45f;
                    GadgetEnemyDistanceThreshold = 4f;
                    GadgetCooldownTicks = 100;

                    TacticalStrafeDistance = 1.8f;
                    TacticalKiteDistance = 2.4f;
                    TacticalMoveRetargetTicks = 11;
                    FragileRangePadding = 0.5f;
                    break;

                case BrawlerArchetype.Artillery:
                    // Indirect-fire AoE specialists: bottles, bombs, throwables that
                    // arc over walls. Fragile, so retreat-biased; loves cluster
                    // targeting (signature trait — only archetype to override
                    // ClusterTargetBonus). Attack cadence is slow due to arc
                    // wind-up.
                    RetreatWeight = 1.2f;
                    ApproachWeight = 0.7f;
                    HoldRangeWeight = 1.35f;
                    RepositionWeight = 1.25f;
                    SearchWeight = 1.05f;
                    WanderWeight = 0.85f;
                    SuperWeight = 1.2f;

                    PreferredObjective = AIObjectiveType.HotZone;
                    ObjectiveWeight = 1.15f;

                    ClusterTargetBonus = 16f;

                    FocusFireWeight = 18f;
                    RegroupWeight = 1.05f;
                    PeelWeight = 1f;
                    RegroupHealthThreshold = 0.3f;
                    AllySupportRange = 8f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 18f;
                    MaxOverFocusedTargetPenalty = 40f;

                    AllyAvoidanceRadius = 3f;
                    AllyAvoidanceWeight = 1.7f;
                    PreferredCombatOffset = 1f;

                    AttackCadenceTicks = 16;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.45f;
                    GadgetEnemyDistanceThreshold = 4f;
                    GadgetCooldownTicks = 100;

                    TacticalStrafeDistance = 2.2f;
                    TacticalKiteDistance = 3.2f;
                    TacticalMoveRetargetTicks = 14;
                    FragileRangePadding = 1.2f;
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
                    ObjectiveWeight = 1.0f;

                    FocusFireWeight = 22f;
                    RegroupWeight = 1.0f;
                    PeelWeight = 1.0f;
                    RegroupHealthThreshold = 0.35f;
                    AllySupportRange = 8f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 16f;
                    MaxOverFocusedTargetPenalty = 36f;

                    AllyAvoidanceRadius = 2.5f;
                    AllyAvoidanceWeight = 1.5f;
                    PreferredCombatOffset = 0.75f;

                    AttackCadenceTicks = 10;
                    EnableGadgetUsage = true;
                    GadgetLowHealthThreshold = 0.45f;
                    GadgetEnemyDistanceThreshold = 4f;
                    GadgetCooldownTicks = 90;

                    TacticalStrafeDistance = 1.8f;
                    TacticalKiteDistance = 2.5f;
                    TacticalMoveRetargetTicks = 10;
                    FragileRangePadding = 0.4f;
                    break;
            }
        }
    }
}
