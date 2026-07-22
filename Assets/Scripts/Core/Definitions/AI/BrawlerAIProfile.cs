using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    [CreateAssetMenu(fileName = "BrawlerAIProfile", menuName = "MOBA/AI/Brawler AI Profile")]
    public class BrawlerAIProfile : ScriptableObject
    {
        [Header("Brawler Role")]
        public BrawlerArchetype Archetype = BrawlerArchetype.Fighter;

        [Header("Difficulty / Personality")]
        public AIDifficultyLevel Difficulty = AIDifficultyLevel.Normal;
        public AIPersonalityType Personality = AIPersonalityType.Balanced;
        [Tooltip("Additional ticks added to perception scheduling. Higher values feel slower to react.")]
        public uint ReactionDelayTicks = 0;
        [Tooltip("Random yaw/placement error applied to AI casts. 0 means perfect AI aim.")]
        public float AimErrorDegrees = 0f;

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

        [Header("Target Context Awareness")]
        [Tooltip("Scales target selection awareness for isolated enemies, protected enemies, and team collapse support.")]
        public float TargetContextAwarenessWeight = 1f;
        [Tooltip("Target score added when an enemy is isolated from their allies.")]
        public float IsolatedTargetBonus = 16f;
        [Tooltip("Target score removed when an enemy is protected by nearby allies and our team has no collapse pressure.")]
        public float ProtectedTargetPenalty = 18f;
        [Tooltip("Target score added when allies are already close enough to collapse on the target.")]
        public float AllyCollapseTargetBonus = 14f;
        [Tooltip("How far around a target to evaluate nearby ally/enemy support.")]
        public float TargetContextRadius = 4.75f;

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
        [Tooltip("How far non-combat bots look for loose Gem Grab gems before falling back to map pressure.")]
        public float GemPickupSearchRadius = 11f;
        [Tooltip("Base search utility added when a loose gem is available.")]
        public float GemPickupBaseScore = 42f;
        [Tooltip("Additional search utility per gem value on the pickup.")]
        public float GemPickupValueScore = 8f;
        [Tooltip("Extra search utility for nearby gems, fading to zero at GemPickupSearchRadius.")]
        public float GemPickupCloseRangeBonus = 22f;
        [Tooltip("Loose gems within this radius are treated as a cluster/dropped pile for pickup urgency.")]
        public float GemPickupClusterRadius = 1.75f;
        [Tooltip("Minimum evaluated pickup score before Search movement commits to a gem.")]
        public float GemPickupMinimumScore = 30f;
        [Tooltip("Bonus when a pickup can put our team at or above the Gem Grab win threshold.")]
        public float GemPickupSecureThresholdBonus = 34f;
        [Tooltip("Bonus when a pickup denies the enemy reaching or extending threshold pressure.")]
        public float GemPickupDenyThresholdBonus = 30f;
        [Tooltip("Bonus when enemy countdown is active and loose gems can help reset pressure.")]
        public float GemPickupCountdownResetBonus = 38f;
        [Tooltip("Penalty for risky carrier pickups during own countdown/hold states.")]
        public float GemPickupCarrierSafetyPenalty = 10f;
        [Tooltip("Radius around known enemy pressure where gem pickups are considered contested.")]
        public float GemPickupThreatRadius = 4.5f;
        [Tooltip("Score removed at full enemy pressure near a gem unless the pickup is strategically urgent.")]
        public float GemPickupThreatPenalty = 24f;

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

        [Header("Lane Discipline")]
        public bool UseLaneDiscipline = true;
        [Tooltip("Scales lane hold and lane break penalties. Higher values make bots preserve formation more strongly.")]
        public float LaneDisciplineWeight = 1f;
        [Tooltip("Score added to map-control objective posture when this bot can hold its assigned lane.")]
        public float LaneHoldObjectiveBonus = 10f;
        [Tooltip("Score added to search/idle posture when lane holding is available.")]
        public float LaneHoldSearchScore = 18f;
        [Tooltip("How far from a lane anchor to search for authored lane cells.")]
        public float LaneHoldSearchRadius = 7f;
        [Tooltip("Side offset used for procedural left/right lanes when no semantic lane cell is authored.")]
        public float LaneSideOffset = 5f;
        [Tooltip("Forward offset used when holding lane around an objective or pressure point.")]
        public float LaneForwardOffset = 1.5f;
        [Tooltip("Target health ratio where chase pressure starts being considered.")]
        public float LowHealthChaseHealthThreshold = 0.32f;
        [Tooltip("Maximum distance for normal low-health chase pressure before lane discipline starts resisting.")]
        public float LowHealthChaseMaxDistance = 8.5f;
        [Tooltip("Approach score added for a secure low-health chase.")]
        public float LowHealthChaseApproachBonus = 28f;
        [Tooltip("Approach score removed when a chase would overextend from lane or safety.")]
        public float UnsafeChasePenalty = 30f;
        [Tooltip("Ticks a valid chase receives commitment pressure before normal break-off checks fully apply.")]
        public uint LowHealthChaseCommitTicks = 18;
        [Tooltip("Maximum ticks to chase a non-carrier target before disengaging.")]
        public uint LowHealthChaseMaxTicks = 90;
        [Tooltip("Ticks to suppress repeat chase attempts after a break-off.")]
        public uint LowHealthChaseCooldownTicks = 28;
        [Tooltip("Distance multiplier beyond LowHealthChaseMaxDistance where active chases must break off.")]
        public float LowHealthChaseBreakDistanceMultiplier = 1.35f;
        [Tooltip("Score added while a chase is inside its committed pursuit window.")]
        public float ChaseCommitScoreBonus = 10f;
        [Tooltip("Approach score removed when chase memory decides to break off.")]
        public float ChaseDisengageScorePenalty = 42f;
        [Tooltip("Approach score removed when a chase would enter bad map geometry without a valuable target.")]
        public float BadMapChasePenalty = 24f;

        [Header("Opponent Resource Awareness")]
        [Tooltip("Scales how strongly this bot reads enemy ammo and super windows. Higher tiers increase this at runtime.")]
        public float OpponentResourceAwarenessWeight = 1f;
        [Tooltip("Target/action score added when an enemy is low on ammo.")]
        public float EnemyLowAmmoOpportunityBonus = 18f;
        [Tooltip("Approach score added when an enemy currently cannot fire.")]
        public float EnemyNoAttackApproachBonus = 12f;
        [Tooltip("Approach pressure removed when an enemy has a ready super.")]
        public float EnemySuperReadyThreatPenalty = 20f;
        [Tooltip("Smaller caution penalty when an enemy is close to a ready super.")]
        public float EnemyNearlySuperThreatPenalty = 8f;
        [Tooltip("Hold/reposition score added when an enemy has a ready or nearly ready super.")]
        public float EnemySuperRespectBonus = 14f;

        [Header("Close Range Role Strategy")]
        [Tooltip("Short-range roles can freely chase until this multiple of their own attack range. Past this, they need a finisher, objective, super, or team-collapse reason.")]
        public float CloseRangeCatchDistanceMultiplier = 2.35f;
        [Tooltip("Approach score removed when a short-range brawler is badly outranged and the chase has no concrete payoff.")]
        public float CloseRangeOutrangedChasePenalty = 52f;
        [Tooltip("Reposition score added when a short-range brawler should break line, take cover, or rotate instead of chasing forever.")]
        public float CloseRangeCoverRepositionBonus = 26f;
        [Tooltip("Approach/reposition pressure added when a short-range brawler has a valid engage but should close with evasive footwork.")]
        public float CloseRangeEvasivePressureBonus = 12f;

        [Header("Game Mode Macro")]
        [Tooltip("Scales score deltas from mode-level push/hold/reset macro calls.")]
        public float MacroActionBiasWeight = 1f;

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
        [Tooltip("If true, action scores are softly shaped around allied action reservations.")]
        public bool UseTeamRoleCoordination = true;
        [Tooltip("Multiplier for team role bonuses and duplicate-role penalties.")]
        public float TeamRoleCoordinationWeight = 1f;
        [Tooltip("Score removed for each allied bot already filling a saturated action role.")]
        public float TeamActionCrowdingPenalty = 12f;
        [Tooltip("Score added when this bot is a good frontline candidate and no ally is currently pressuring forward.")]
        public float TeamFrontlineNeedBonus = 12f;
        [Tooltip("Score added for backline actions when an ally is already pressuring forward.")]
        public float TeamBacklineAnchorBonus = 6f;
        [Tooltip("Soft cap for allies already approaching before duplicate approach pressure is discouraged.")]
        public int MaxTeamApproachers = 1;
        [Tooltip("Soft cap for allies already responding to peel before duplicate peel is discouraged.")]
        public int MaxTeamPeelResponders = 1;
        [Tooltip("Soft cap for allies already regrouping before duplicate regroup is discouraged.")]
        public int MaxTeamRegroupResponders = 2;
        [Tooltip("Soft cap for allies already moving to objective before duplicate objective movement is discouraged.")]
        public int MaxTeamObjectiveMovers = 1;

        [Header("Advanced Team Fight")]
        [Tooltip("Scales coordinated collapse, flank, bait, and peel adjustments during active fights.")]
        public float TeamFightCoordinationWeight = 1f;
        [Tooltip("Action score added when an ally has already created a focus-fire collapse on a vulnerable target.")]
        public float TeamCollapseFocusBonus = 16f;
        [Tooltip("Reposition score added when this bot should take a flank/angle instead of dogpiling a target.")]
        public float TeamFlankRepositionBonus = 12f;
        [Tooltip("Hold-range score added when this bot should bait pressure while allies collapse.")]
        public float TeamBaitHoldBonus = 10f;
        [Tooltip("Peel score added when a nearby ally is under threat.")]
        public float TeamPeelAssistBonus = 14f;
        [Tooltip("Approach score removed when the team is already overcommitted and the target is not clearly finishable.")]
        public float TeamOvercommitApproachPenalty = 14f;

        [Header("Role Macro Intelligence")]
        [Tooltip("Scales archetype-specific macro behavior. Runtime performance tiers make this visibly weaker or stronger.")]
        public float RoleMacroBehaviorWeight = 1f;
        [Tooltip("Tank score added for creating space around objectives, carriers, and team pushes.")]
        public float RoleTankSpaceCreationBonus = 16f;
        [Tooltip("Backline score added for anchoring lanes, holding safe angles, and avoiding needless dives.")]
        public float RoleBacklineAnchorMacroBonus = 14f;
        [Tooltip("Support score added for peel, regroup, and carrier-protection macro calls.")]
        public float RoleSupportPeelMacroBonus = 14f;
        [Tooltip("Assassin score added for pick windows on low-health, disabled, or high-value targets.")]
        public float RoleAssassinPickPressureBonus = 16f;
        [Tooltip("Controller score added for objective zones, lane ownership, and choke control.")]
        public float RoleControllerZoneMacroBonus = 15f;
        [Tooltip("Artillery score added for denial, safe angles, and anti-clump pressure.")]
        public float RoleArtilleryDenialMacroBonus = 14f;
        [Tooltip("Fighter score added for flexible objective pressure and fight-to-objective transitions.")]
        public float RoleFighterFlexMacroBonus = 10f;

        [Header("Engagement Risk Awareness")]
        [Tooltip("Scales local fight odds evaluation. Higher tiers avoid bad dives and capitalize on supported fights more strongly.")]
        public float EngagementRiskAwarenessWeight = 1f;
        [Tooltip("Radius used to compare nearby ally and enemy pressure around this bot.")]
        public float EngagementRiskRadius = 5.25f;
        [Tooltip("Approach score removed when this bot is locally outnumbered without a clear pick reason.")]
        public float OutnumberedApproachPenalty = 24f;
        [Tooltip("Approach/use-super score added when allies can support a collapse.")]
        public float SupportedFightCommitBonus = 14f;
        [Tooltip("Reposition/hold score added when a fight should be reset before re-engaging.")]
        public float BadDiveRepositionBonus = 18f;
        [Tooltip("Regroup/retreat score added when local pressure is unsafe or the bot is carrying objective value.")]
        public float EngagementRiskSafetyBonus = 12f;

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

        [Tooltip("If a bot has reached its tactical combat point, refresh after this many ticks so it keeps footwork alive.")]
        public uint TacticalMoveHeartbeatTicks = 18;

        [Tooltip("Retarget tactical movement early when the enemy has moved this far from the position used by the last tactical plan.")]
        public float TacticalDestinationStaleDistance = 1.25f;

        [Tooltip("Minimum ticks before normal tactical movement may reverse direction. Emergency retreats ignore this.")]
        public uint TacticalDirectionFlipCooldownTicks = 24;

        [Tooltip("How far a new tactical destination must differ before replacing the current one while the bot is still travelling.")]
        public float TacticalDestinationSwitchDistance = 1.2f;

        [Tooltip("How strongly normal tactical movement adopts a new destination. Lower values preserve the current lane longer.")]
        [Range(0.05f, 1f)] public float TacticalDestinationBlend = 0.55f;

        [Tooltip("Maximum AI movement input turn per simulation tick. Prevents instant left/right movement and rotation snaps.")]
        public float AIMoveInputTurnRateDegreesPerTick = 22f;

        [Tooltip("Maximum AI movement input turn per tick for high-priority routes such as evade/recovery.")]
        public float AIHighPriorityMoveInputTurnRateDegreesPerTick = 54f;

        [Tooltip("Movement input magnitude used by AI during normal routes. 1 is full brawler move speed.")]
        [Range(0.35f, 1f)] public float AIMoveSpeedScale = 0.86f;

        [Tooltip("Movement input magnitude used by AI for urgent evade/recovery routes. Keep below 1 for fair, readable dodges.")]
        [Range(0.35f, 1f)] public float AIHighPriorityMoveSpeedScale = 0.90f;

        [Tooltip("Minimum distance a tactical move should ask the bot to travel. Prevents tiny stale destinations from turning into stationary aim.")]
        public float TacticalMinimumStepDistance = 0.75f;

        [Tooltip("Maximum ticks a tactical no-move hold may persist before fallback movement is forced.")]
        public uint TacticalStopMaxHoldTicks = 10;

        [Tooltip("Extra distance fragile brawlers try to keep from enemies.")]
        public float FragileRangePadding = 0.75f;

        [Tooltip("If true, logs tactical movement decisions.")]
        public bool LogTacticalMovement = false;

        [Header("Map Intelligence")]
        public bool UseMapIntelligence = true;
        [Tooltip("How far around a requested movement point the AI may search for a better map-aware destination.")]
        public float MapDestinationSearchRadius = 3f;
        [Tooltip("Score bonus for routes that end in bush when the current intent benefits from stealth.")]
        public float MapBushPreference = 9f;
        [Tooltip("Score bonus for cover-adjacent cells when the current intent benefits from protection or angle play.")]
        public float MapCoverPreference = 7f;
        [Tooltip("Score bonus when map blockers sit between the bot and a known threat on defensive routes.")]
        public float MapLineOfSightCoverPreference = 10f;
        [Tooltip("Score penalty for exposed defensive destinations with no map blocker between the bot and known threat.")]
        public float MapExposedPositionPenalty = 7f;
        [Tooltip("Score bonus for direct-fire combat routes that keep an unobstructed map line toward the target.")]
        public float MapOpenShotPreference = 6f;
        [Tooltip("Penalty for narrow cells with few exits. Fragile bots and non-combat routes avoid these more strongly.")]
        public float MapChokepointPenalty = 12f;
        [Tooltip("How strongly map routing reacts to known enemy threat positions.")]
        public float MapThreatAvoidanceWeight = 4f;
        [Tooltip("Small route-length penalty used when several map-aware destinations are otherwise similar.")]
        public float MapPathCostWeight = 0.2f;
        [Tooltip("Bonus for direct-fire cells that sit beside cover while keeping a clear shot lane.")]
        public float MapCoverPeekPreference = 8f;
        [Tooltip("Bonus for cells that hold the lane between the bot and a pressure point.")]
        public float MapLaneControlPreference = 7f;
        [Tooltip("Bonus for controlling the edge of a chokepoint without standing inside it.")]
        public float MapChokeControlPreference = 9f;
        [Tooltip("Bonus for thrower-safe cells with wall cover between the bot and a threat.")]
        public float MapThrowerSafePositionPreference = 12f;
        [Tooltip("Bonus or penalty for whether walls support the brawler's pressure style.")]
        public float MapWallPressurePreference = 8f;
        [Tooltip("Penalty for trap-like wall and boundary cells with too few real exits.")]
        public float MapWallHugPenalty = 9f;
        [Tooltip("Bonus for combat destinations with enough exits to keep moving around cover.")]
        public float MapEscapeSpacePreference = 5f;
        [Tooltip("Bonus for cover-adjacent combat cells that support peek, strafe, and retreat rhythm.")]
        public float MapCoverDancePreference = 5f;
        [Tooltip("Bonus for direct-fire brawlers holding clear lanes without standing in cramped cells.")]
        public float MapFireLanePressurePreference = 5f;
        [Tooltip("Bonus for artillery cells that keep wall cover while preserving spacing and exits.")]
        public float MapThrowerSpacingPreference = 7f;
        [Tooltip("If true, logs map-aware movement decisions.")]
        public bool LogMapIntelligence = false;

        [Header("Reactive Combat")]
        [Tooltip("Ticks recent incoming damage remains tactically relevant.")]
        public uint ReactiveDamageMemoryTicks = 45;
        [Tooltip("Retreat score bonus at full recent-damage pressure.")]
        public float ReactiveRetreatPressureBonus = 38f;
        [Tooltip("Reposition score bonus at full recent-damage pressure.")]
        public float ReactiveRepositionPressureBonus = 28f;
        [Tooltip("Combat score bonus against the most recent attacker.")]
        public float ReactiveAttackerFocusBonus = 18f;
        [Tooltip("Below this health ratio, recent damage becomes an emergency survival signal.")]
        public float ReactiveEmergencyHealthRatio = 0.40f;
        [Tooltip("If true, logs reactive damage memory updates.")]
        public bool LogReactiveEvents = false;

        [Header("Danger Avoidance")]
        [Tooltip("How far the bot scans for active projectiles and hostile area hazards.")]
        public float DangerScanRadius = 7f;
        [Tooltip("Ticks between danger scans. Keep low for responsiveness, but never every frame for every bot unless profiling proves it safe.")]
        public uint DangerRefreshIntervalTicks = 4;
        [Tooltip("Extra padding added around the bot and threat radius while evaluating danger.")]
        public float DangerPersonalSpace = 0.45f;
        [Tooltip("Threat impact window used for early evasion. Higher values make bots dodge sooner.")]
        public float DangerReactionTimeSeconds = 0.55f;
        [Tooltip("Minimum danger pressure needed before Evade can score.")]
        public float DangerEvadePressureThreshold = 0.36f;
        [Tooltip("Evade score added at full danger pressure.")]
        public float DangerEvadeScoreBonus = 50f;
        [Tooltip("Step distance requested when dodging a projectile or leaving a hazard.")]
        public float DangerEvadeDistance = 1.8f;
        [Tooltip("Ticks before recalculating an evade route while danger remains active.")]
        public uint DangerEvadeRetargetTicks = 10;
        [Tooltip("Recalculate evade movement when the primary threat moves this far from the last planned threat position.")]
        public float DangerThreatStaleDistance = 1.25f;
        [Tooltip("Local map search radius for evade. Smaller than normal map routing to avoid expensive per-tick candidate scans.")]
        public float DangerMapSearchRadius = 1.2f;
        [Tooltip("If true, logs danger avoidance memory updates.")]
        public bool LogDangerAvoidance = false;

        [Header("Failure Recovery")]
        public bool EnableFailureRecovery = true;
        [Tooltip("Ticks between navigation movement-progress samples. Lower values detect obstacle stalls sooner.")]
        public uint NavigationStuckSampleIntervalTicks = 8;
        [Tooltip("Minimum movement between stuck samples before the bot is considered to be making progress.")]
        public float NavigationStuckMoveThreshold = 0.08f;
        [Tooltip("Navigation samples that must report no meaningful movement before recovery opens.")]
        public int NavigationStuckSampleLimit = 2;
        [Tooltip("Blocked route reports needed before the bot attempts a local detour.")]
        public int BlockedRouteRecoveryLimit = 1;
        [Tooltip("Ticks a destination can remain active with minimal progress before it is considered stale.")]
        public uint StaleDestinationRecoveryTicks = 90;
        [Tooltip("Minimum distance that counts as real progress toward a long-lived destination.")]
        public float StaleDestinationProgressThreshold = 0.6f;
        [Tooltip("Minimum ticks between navigation recovery attempts.")]
        public uint FailureRecoveryCooldownTicks = 18;
        [Tooltip("Distance of the local side-step / detour requested after a navigation recovery trigger.")]
        public float FailureRecoveryDetourDistance = 1.8f;
        [Tooltip("Ticks a targetless bot may have no destination before idle hesitation recovery opens.")]
        public uint IdleHesitationRecoveryTicks = 14;
        [Tooltip("Minimum ticks between idle hesitation recovery nudges.")]
        public uint IdleHesitationCooldownTicks = 24;
        [Tooltip("Chosen action score at or below this value counts as low-confidence idle intent.")]
        public float IdleHesitationLowScoreThreshold = 8f;
        [Tooltip("Failed casts remembered for this many ticks while detecting repeated invalid ability usage.")]
        public uint FailedCastMemoryTicks = 60;
        [Tooltip("Failed casts within memory before the slot is briefly suppressed.")]
        public int FailedCastRecoveryLimit = 2;
        [Tooltip("Ticks to suppress a repeatedly failing ability slot.")]
        public uint FailedCastSuppressionTicks = 30;
        [Tooltip("If true, logs stuck, blocked-route, stale-command, and failed-cast recovery decisions.")]
        public bool LogFailureRecovery = false;

        [Header("Production Hardening")]
        [Tooltip("If true, AI lifecycle events may be logged. Keep false in normal gameplay builds.")]
        public bool LogLifecycle = false;
        [Tooltip("If true, periodic AI action tick summaries may be logged. Keep false outside targeted debugging.")]
        public bool LogDecisionTicks = false;
        [Tooltip("If true, perception target scans may be logged. This can be noisy with many bots.")]
        public bool LogPerception = false;
        [Tooltip("If true, AI validation telemetry is recorded for regression health tracking.")]
        public bool EnableValidationTelemetry = true;
        [Tooltip("If true, AI debug snapshots are published to AIDebugTracker.")]
        public bool EnableDebugSnapshots = true;
        [Tooltip("Ticks between debug snapshot refreshes. Higher values reduce per-bot string/list churn.")]
        public uint DebugSnapshotIntervalTicks = 5;
        [Tooltip("If true, expensive AI work uses per-tick permits and gracefully defers non-critical work under load.")]
        public bool EnableAIBudgetEnforcement = true;
        [Tooltip("Per-tick perception scans allowed before idle/low-priority bots defer sensing.")]
        public int MaxPerceptionScansPerTick = 8;
        [Tooltip("Per-tick danger scans allowed before low-priority danger refreshes defer.")]
        public int MaxDangerRefreshesPerTick = 8;
        [Tooltip("Per-tick map resolve budget before AI perf telemetry reports pressure.")]
        public int MaxMapResolvesPerTick = 24;
        [Tooltip("Per-tick path query budget before AI perf telemetry reports pressure.")]
        public int MaxPathQueriesPerTick = 12;
        [Tooltip("Per-tick A* touched-node budget before AI perf telemetry reports pressure.")]
        public int MaxPathTouchedNodesPerTick = 5000;
        [Tooltip("Ticks to delay an optional perception scan when the AI budget is saturated.")]
        public uint BudgetDeferredSenseTicks = 2;
        [Tooltip("Ticks to delay an optional danger scan when the AI budget is saturated.")]
        public uint BudgetDeferredDangerTicks = 2;
        [Tooltip("Ticks to delay an optional path repath when the AI budget is saturated.")]
        public uint BudgetDeferredPathTicks = 2;
        [Tooltip("Consecutive path-budget deferrals without a usable route before this bot escalates its next path query.")]
        public int PathBudgetStarvationLimit = 3;
        [Tooltip("If true, emergency retreat/evade/recovery work may exceed the soft budget instead of being dropped.")]
        public bool AllowCriticalBudgetOverspend = true;
        [Tooltip("If true, over-budget AI frames emit rate-limited warnings.")]
        public bool LogBudgetWarnings = false;

        [Header("Humanization / Believability")]
        [Tooltip("If true, applies small bounded imperfections so bots feel skillful but human.")]
        public bool EnableHumanization = true;
        [Tooltip("Maximum extra perception jitter added by reaction rhythm. Keeps reads from landing on machine-perfect intervals.")]
        public uint HumanizationReactionJitterTicks = 2;
        [Tooltip("Small per-action score variance applied before commitment. Emergency actions are protected.")]
        public float HumanizationActionScoreJitter = 1.75f;
        [Tooltip("Chance per fake-out check to briefly overstate a movement action when not in immediate danger.")]
        [Range(0f, 1f)] public float HumanizationFakeOutChance = 0.08f;
        [Tooltip("Score bonus applied to the active fake-out movement action.")]
        public float HumanizationFakeOutScoreBonus = 6f;
        [Tooltip("Ticks a fake-out expression remains active once selected.")]
        public uint HumanizationFakeOutDurationTicks = 8;
        [Tooltip("Minimum ticks between fake-out checks.")]
        public uint HumanizationFakeOutCooldownTicks = 80;
        [Tooltip("Chance per pressure check to make a short hesitation/over-defensive choice under pressure.")]
        [Range(0f, 1f)] public float HumanizationPressureMistakeChance = 0.04f;
        [Tooltip("Score penalty applied to offensive actions during a pressure mistake window.")]
        public float HumanizationPressureMistakePenalty = 8f;
        [Tooltip("Health ratio below which pressure mistakes can be considered even without active danger.")]
        public float HumanizationPressureHealthThreshold = 0.45f;
        [Tooltip("Ticks a pressure mistake window remains active once triggered.")]
        public uint HumanizationPressureMistakeDurationTicks = 8;
        [Tooltip("Minimum ticks between pressure mistake checks.")]
        public uint HumanizationPressureMistakeCooldownTicks = 110;
        [Tooltip("Scales personality-specific expression in fake-outs and hesitation.")]
        public float HumanizationPersonalityExpression = 1f;
        [Tooltip("If true, logs humanization windows and score shaping decisions.")]
        public bool LogHumanization = false;

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
            UseTeamRoleCoordination = true;

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
                    MacroActionBiasWeight = 0.95f;

                    FocusFireWeight = 25f;
                    RegroupWeight = 1.2f;
                    PeelWeight = 0.8f;
                    RegroupHealthThreshold = 0.45f;
                    AllySupportRange = 9f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 18f;
                    MaxOverFocusedTargetPenalty = 40f;
                    TeamRoleCoordinationWeight = 1.1f;
                    TeamActionCrowdingPenalty = 14f;
                    TeamFrontlineNeedBonus = 4f;
                    TeamBacklineAnchorBonus = 10f;
                    MaxTeamApproachers = 1;
                    MaxTeamPeelResponders = 1;
                    MaxTeamRegroupResponders = 2;
                    MaxTeamObjectiveMovers = 1;

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
                    TacticalMoveHeartbeatTicks = 18;
                    TacticalDestinationStaleDistance = 1.25f;
                    TacticalMinimumStepDistance = 0.8f;
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
                    MacroActionBiasWeight = 1.10f;

                    FocusFireWeight = 18f;
                    RegroupWeight = 0.8f;
                    PeelWeight = 1.35f;
                    RegroupHealthThreshold = 0.20f;
                    AllySupportRange = 10f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 12f;
                    MaxOverFocusedTargetPenalty = 28f;
                    TeamRoleCoordinationWeight = 0.9f;
                    TeamActionCrowdingPenalty = 8f;
                    TeamFrontlineNeedBonus = 18f;
                    TeamBacklineAnchorBonus = 2f;
                    MaxTeamApproachers = 2;
                    MaxTeamPeelResponders = 2;
                    MaxTeamRegroupResponders = 1;
                    MaxTeamObjectiveMovers = 1;

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
                    TacticalMoveHeartbeatTicks = 20;
                    TacticalDestinationStaleDistance = 1.4f;
                    TacticalMinimumStepDistance = 0.55f;
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
                    MacroActionBiasWeight = 0.90f;

                    FocusFireWeight = 25f;
                    RegroupWeight = 0.7f;
                    PeelWeight = 0.65f;
                    RegroupHealthThreshold = 0.25f;
                    AllySupportRange = 7f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 20f;
                    MaxOverFocusedTargetPenalty = 44f;
                    TeamRoleCoordinationWeight = 0.95f;
                    TeamActionCrowdingPenalty = 10f;
                    TeamFrontlineNeedBonus = 10f;
                    TeamBacklineAnchorBonus = 4f;
                    MaxTeamApproachers = 1;
                    MaxTeamPeelResponders = 1;
                    MaxTeamRegroupResponders = 1;
                    MaxTeamObjectiveMovers = 1;

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
                    TacticalMoveHeartbeatTicks = 14;
                    TacticalDestinationStaleDistance = 1.5f;
                    TacticalMinimumStepDistance = 0.85f;
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
                    MacroActionBiasWeight = 1.10f;

                    FocusFireWeight = 18f;
                    RegroupWeight = 1.15f;
                    PeelWeight = 1.4f;
                    RegroupHealthThreshold = 0.40f;
                    AllySupportRange = 11f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 18f;
                    MaxOverFocusedTargetPenalty = 40f;
                    TeamRoleCoordinationWeight = 1.15f;
                    TeamActionCrowdingPenalty = 13f;
                    TeamFrontlineNeedBonus = 3f;
                    TeamBacklineAnchorBonus = 10f;
                    MaxTeamApproachers = 1;
                    MaxTeamPeelResponders = 2;
                    MaxTeamRegroupResponders = 2;
                    MaxTeamObjectiveMovers = 1;

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
                    TacticalMoveHeartbeatTicks = 18;
                    TacticalDestinationStaleDistance = 1.25f;
                    TacticalMinimumStepDistance = 0.75f;
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
                    MacroActionBiasWeight = 1.15f;

                    FocusFireWeight = 24f;
                    RegroupWeight = 0.9f;
                    PeelWeight = 1.05f;
                    RegroupHealthThreshold = 0.35f;
                    AllySupportRange = 8.5f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 16f;
                    MaxOverFocusedTargetPenalty = 36f;
                    TeamRoleCoordinationWeight = 1.05f;
                    TeamActionCrowdingPenalty = 11f;
                    TeamFrontlineNeedBonus = 7f;
                    TeamBacklineAnchorBonus = 8f;
                    MaxTeamApproachers = 1;
                    MaxTeamPeelResponders = 1;
                    MaxTeamRegroupResponders = 2;
                    MaxTeamObjectiveMovers = 2;

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
                    TacticalMoveHeartbeatTicks = 17;
                    TacticalDestinationStaleDistance = 1.3f;
                    TacticalMinimumStepDistance = 0.7f;
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
                    MacroActionBiasWeight = 1.05f;

                    ClusterTargetBonus = 16f;

                    FocusFireWeight = 18f;
                    RegroupWeight = 1.05f;
                    PeelWeight = 1f;
                    RegroupHealthThreshold = 0.3f;
                    AllySupportRange = 8f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 18f;
                    MaxOverFocusedTargetPenalty = 40f;
                    TeamRoleCoordinationWeight = 1.15f;
                    TeamActionCrowdingPenalty = 14f;
                    TeamFrontlineNeedBonus = 2f;
                    TeamBacklineAnchorBonus = 11f;
                    MaxTeamApproachers = 1;
                    MaxTeamPeelResponders = 1;
                    MaxTeamRegroupResponders = 2;
                    MaxTeamObjectiveMovers = 1;

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
                    TacticalMoveHeartbeatTicks = 20;
                    TacticalDestinationStaleDistance = 1.2f;
                    TacticalMinimumStepDistance = 0.85f;
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
                    MacroActionBiasWeight = 1.0f;

                    FocusFireWeight = 22f;
                    RegroupWeight = 1.0f;
                    PeelWeight = 1.0f;
                    RegroupHealthThreshold = 0.35f;
                    AllySupportRange = 8f;
                    TargetFocusSoftLimit = 1;
                    OverFocusedTargetPenaltyPerAlly = 16f;
                    MaxOverFocusedTargetPenalty = 36f;
                    TeamRoleCoordinationWeight = 1f;
                    TeamActionCrowdingPenalty = 12f;
                    TeamFrontlineNeedBonus = 12f;
                    TeamBacklineAnchorBonus = 6f;
                    MaxTeamApproachers = 1;
                    MaxTeamPeelResponders = 1;
                    MaxTeamRegroupResponders = 2;
                    MaxTeamObjectiveMovers = 1;

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
                    TacticalMoveHeartbeatTicks = 16;
                    TacticalDestinationStaleDistance = 1.3f;
                    TacticalMinimumStepDistance = 0.7f;
                    FragileRangePadding = 0.4f;
                    break;
            }

            ApplyGemGrabObjectiveDefaults(archetype);
            ApplyLaneDisciplineDefaults(archetype);
            ApplyTacticalStabilizationDefaults(archetype);
            ApplyMapIntelligenceDefaults(archetype);
            ApplyReactiveCombatDefaults(archetype);
            ApplyDangerAvoidanceDefaults(archetype);
            ApplyFailureRecoveryDefaults(archetype);
            ApplyProductionBudgetDefaults(archetype);
            ApplyRoleMacroDefaults(archetype);
        }

        private void ApplyRoleMacroDefaults(BrawlerArchetype archetype)
        {
            RoleMacroBehaviorWeight = 1f;
            RoleTankSpaceCreationBonus = 14f;
            RoleBacklineAnchorMacroBonus = 13f;
            RoleSupportPeelMacroBonus = 13f;
            RoleAssassinPickPressureBonus = 14f;
            RoleControllerZoneMacroBonus = 14f;
            RoleArtilleryDenialMacroBonus = 13f;
            RoleFighterFlexMacroBonus = 10f;

            switch (archetype)
            {
                case BrawlerArchetype.Tank:
                    RoleTankSpaceCreationBonus = 18f;
                    RoleFighterFlexMacroBonus = 8f;
                    break;

                case BrawlerArchetype.Assassin:
                    RoleAssassinPickPressureBonus = 20f;
                    RoleBacklineAnchorMacroBonus = 9f;
                    break;

                case BrawlerArchetype.Sniper:
                    RoleBacklineAnchorMacroBonus = 18f;
                    RoleAssassinPickPressureBonus = 10f;
                    break;

                case BrawlerArchetype.Support:
                    RoleSupportPeelMacroBonus = 18f;
                    RoleBacklineAnchorMacroBonus = 15f;
                    break;

                case BrawlerArchetype.Controller:
                    RoleControllerZoneMacroBonus = 19f;
                    RoleSupportPeelMacroBonus = 12f;
                    break;

                case BrawlerArchetype.Artillery:
                    RoleArtilleryDenialMacroBonus = 19f;
                    RoleBacklineAnchorMacroBonus = 16f;
                    break;

                case BrawlerArchetype.Fighter:
                default:
                    RoleFighterFlexMacroBonus = 14f;
                    break;
            }
        }

        private void ApplyGemGrabObjectiveDefaults(BrawlerArchetype archetype)
        {
            GemPickupSearchRadius = 11f;
            GemPickupBaseScore = 42f;
            GemPickupValueScore = 8f;
            GemPickupCloseRangeBonus = 22f;
            GemPickupClusterRadius = 1.75f;
            GemPickupMinimumScore = 30f;
            GemPickupSecureThresholdBonus = 34f;
            GemPickupDenyThresholdBonus = 30f;
            GemPickupCountdownResetBonus = 38f;
            GemPickupCarrierSafetyPenalty = 10f;
            GemPickupThreatRadius = 4.5f;
            GemPickupThreatPenalty = 24f;

            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                    GemPickupMinimumScore = 36f;
                    GemPickupCarrierSafetyPenalty = 13f;
                    GemPickupThreatPenalty = 30f;
                    break;

                case BrawlerArchetype.Tank:
                    GemPickupMinimumScore = 24f;
                    GemPickupThreatPenalty = 16f;
                    GemPickupCarrierSafetyPenalty = 7f;
                    break;

                case BrawlerArchetype.Assassin:
                    GemPickupSearchRadius = 12f;
                    GemPickupMinimumScore = 26f;
                    GemPickupDenyThresholdBonus = 34f;
                    GemPickupThreatPenalty = 18f;
                    break;

                case BrawlerArchetype.Support:
                    GemPickupMinimumScore = 34f;
                    GemPickupSecureThresholdBonus = 38f;
                    GemPickupCarrierSafetyPenalty = 12f;
                    GemPickupThreatPenalty = 28f;
                    break;

                case BrawlerArchetype.Controller:
                    GemPickupMinimumScore = 30f;
                    GemPickupSecureThresholdBonus = 36f;
                    GemPickupDenyThresholdBonus = 34f;
                    GemPickupThreatPenalty = 24f;
                    break;

                case BrawlerArchetype.Artillery:
                    GemPickupMinimumScore = 38f;
                    GemPickupCarrierSafetyPenalty = 14f;
                    GemPickupThreatPenalty = 32f;
                    break;
            }
        }

        private void ApplyLaneDisciplineDefaults(BrawlerArchetype archetype)
        {
            UseLaneDiscipline = true;
            LaneDisciplineWeight = 1f;
            LaneHoldObjectiveBonus = 10f;
            LaneHoldSearchScore = 18f;
            LaneHoldSearchRadius = 7f;
            LaneSideOffset = 5f;
            LaneForwardOffset = 1.5f;
            LowHealthChaseHealthThreshold = 0.32f;
            LowHealthChaseMaxDistance = 8.5f;
            LowHealthChaseApproachBonus = 28f;
            UnsafeChasePenalty = 30f;
            LowHealthChaseCommitTicks = 18;
            LowHealthChaseMaxTicks = 90;
            LowHealthChaseCooldownTicks = 28;
            LowHealthChaseBreakDistanceMultiplier = 1.35f;
            ChaseCommitScoreBonus = 10f;
            ChaseDisengageScorePenalty = 42f;
            BadMapChasePenalty = 24f;
            CloseRangeCatchDistanceMultiplier = 2.35f;
            CloseRangeOutrangedChasePenalty = 52f;
            CloseRangeCoverRepositionBonus = 26f;
            CloseRangeEvasivePressureBonus = 12f;

            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                    LaneDisciplineWeight = 1.25f;
                    LaneHoldObjectiveBonus = 12f;
                    LaneHoldSearchScore = 20f;
                    LowHealthChaseMaxDistance = 7f;
                    LowHealthChaseApproachBonus = 18f;
                    UnsafeChasePenalty = 36f;
                    LowHealthChaseCommitTicks = 14;
                    LowHealthChaseMaxTicks = 60;
                    ChaseCommitScoreBonus = 7f;
                    ChaseDisengageScorePenalty = 50f;
                    BadMapChasePenalty = 30f;
                    break;

                case BrawlerArchetype.Tank:
                    LaneDisciplineWeight = 0.85f;
                    LaneHoldObjectiveBonus = 8f;
                    LowHealthChaseMaxDistance = 7.5f;
                    LowHealthChaseApproachBonus = 22f;
                    UnsafeChasePenalty = 24f;
                    LowHealthChaseCommitTicks = 20;
                    LowHealthChaseMaxTicks = 100;
                    ChaseDisengageScorePenalty = 34f;
                    BadMapChasePenalty = 16f;
                    CloseRangeCatchDistanceMultiplier = 2.45f;
                    CloseRangeOutrangedChasePenalty = 58f;
                    CloseRangeCoverRepositionBonus = 30f;
                    CloseRangeEvasivePressureBonus = 15f;
                    break;

                case BrawlerArchetype.Assassin:
                    LaneDisciplineWeight = 0.70f;
                    LaneHoldObjectiveBonus = 6f;
                    LaneHoldSearchScore = 14f;
                    LowHealthChaseHealthThreshold = 0.42f;
                    LowHealthChaseMaxDistance = 10.5f;
                    LowHealthChaseApproachBonus = 40f;
                    UnsafeChasePenalty = 18f;
                    LowHealthChaseCommitTicks = 24;
                    LowHealthChaseMaxTicks = 120;
                    LowHealthChaseBreakDistanceMultiplier = 1.55f;
                    ChaseCommitScoreBonus = 16f;
                    ChaseDisengageScorePenalty = 30f;
                    BadMapChasePenalty = 14f;
                    CloseRangeCatchDistanceMultiplier = 2.80f;
                    CloseRangeOutrangedChasePenalty = 44f;
                    CloseRangeCoverRepositionBonus = 24f;
                    CloseRangeEvasivePressureBonus = 18f;
                    break;

                case BrawlerArchetype.Support:
                    LaneDisciplineWeight = 1.20f;
                    LaneHoldObjectiveBonus = 12f;
                    LaneHoldSearchScore = 20f;
                    LowHealthChaseMaxDistance = 7f;
                    LowHealthChaseApproachBonus = 16f;
                    UnsafeChasePenalty = 38f;
                    LowHealthChaseCommitTicks = 12;
                    LowHealthChaseMaxTicks = 55;
                    ChaseCommitScoreBonus = 6f;
                    ChaseDisengageScorePenalty = 52f;
                    BadMapChasePenalty = 30f;
                    break;

                case BrawlerArchetype.Controller:
                    LaneDisciplineWeight = 1.25f;
                    LaneHoldObjectiveBonus = 14f;
                    LaneHoldSearchScore = 22f;
                    LowHealthChaseMaxDistance = 8f;
                    LowHealthChaseApproachBonus = 22f;
                    UnsafeChasePenalty = 32f;
                    LowHealthChaseCommitTicks = 16;
                    LowHealthChaseMaxTicks = 75;
                    ChaseCommitScoreBonus = 8f;
                    ChaseDisengageScorePenalty = 44f;
                    BadMapChasePenalty = 28f;
                    break;

                case BrawlerArchetype.Artillery:
                    LaneDisciplineWeight = 1.30f;
                    LaneHoldObjectiveBonus = 12f;
                    LaneHoldSearchScore = 21f;
                    LowHealthChaseMaxDistance = 7f;
                    LowHealthChaseApproachBonus = 14f;
                    UnsafeChasePenalty = 40f;
                    LowHealthChaseCommitTicks = 12;
                    LowHealthChaseMaxTicks = 55;
                    ChaseCommitScoreBonus = 5f;
                    ChaseDisengageScorePenalty = 54f;
                    BadMapChasePenalty = 32f;
                    break;

                case BrawlerArchetype.Fighter:
                default:
                    break;
            }
        }

        private void ApplyTacticalStabilizationDefaults(BrawlerArchetype archetype)
        {
            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                    TacticalDirectionFlipCooldownTicks = 28;
                    TacticalDestinationSwitchDistance = 1.35f;
                    TacticalDestinationBlend = 0.45f;
                    AIMoveInputTurnRateDegreesPerTick = 18f;
                    AIHighPriorityMoveInputTurnRateDegreesPerTick = 44f;
                    AIMoveSpeedScale = 0.82f;
                    AIHighPriorityMoveSpeedScale = 0.88f;
                    break;

                case BrawlerArchetype.Tank:
                    TacticalDirectionFlipCooldownTicks = 18;
                    TacticalDestinationSwitchDistance = 1.0f;
                    TacticalDestinationBlend = 0.65f;
                    AIMoveInputTurnRateDegreesPerTick = 18f;
                    AIHighPriorityMoveInputTurnRateDegreesPerTick = 46f;
                    AIMoveSpeedScale = 0.82f;
                    AIHighPriorityMoveSpeedScale = 0.88f;
                    break;

                case BrawlerArchetype.Assassin:
                    TacticalDirectionFlipCooldownTicks = 16;
                    TacticalDestinationSwitchDistance = 0.95f;
                    TacticalDestinationBlend = 0.7f;
                    AIMoveInputTurnRateDegreesPerTick = 26f;
                    AIHighPriorityMoveInputTurnRateDegreesPerTick = 68f;
                    AIMoveSpeedScale = 0.94f;
                    AIHighPriorityMoveSpeedScale = 0.98f;
                    break;

                case BrawlerArchetype.Support:
                    TacticalDirectionFlipCooldownTicks = 26;
                    TacticalDestinationSwitchDistance = 1.3f;
                    TacticalDestinationBlend = 0.5f;
                    AIMoveInputTurnRateDegreesPerTick = 18f;
                    AIHighPriorityMoveInputTurnRateDegreesPerTick = 46f;
                    AIMoveSpeedScale = 0.84f;
                    AIHighPriorityMoveSpeedScale = 0.90f;
                    break;

                case BrawlerArchetype.Controller:
                    TacticalDirectionFlipCooldownTicks = 24;
                    TacticalDestinationSwitchDistance = 1.2f;
                    TacticalDestinationBlend = 0.55f;
                    AIMoveInputTurnRateDegreesPerTick = 20f;
                    AIHighPriorityMoveInputTurnRateDegreesPerTick = 52f;
                    AIMoveSpeedScale = 0.86f;
                    AIHighPriorityMoveSpeedScale = 0.91f;
                    break;

                case BrawlerArchetype.Artillery:
                    TacticalDirectionFlipCooldownTicks = 30;
                    TacticalDestinationSwitchDistance = 1.4f;
                    TacticalDestinationBlend = 0.45f;
                    AIMoveInputTurnRateDegreesPerTick = 16f;
                    AIHighPriorityMoveInputTurnRateDegreesPerTick = 40f;
                    AIMoveSpeedScale = 0.80f;
                    AIHighPriorityMoveSpeedScale = 0.86f;
                    break;

                case BrawlerArchetype.Fighter:
                default:
                    TacticalDirectionFlipCooldownTicks = 22;
                    TacticalDestinationSwitchDistance = 1.15f;
                    TacticalDestinationBlend = 0.6f;
                    AIMoveInputTurnRateDegreesPerTick = 22f;
                    AIHighPriorityMoveInputTurnRateDegreesPerTick = 54f;
                    AIMoveSpeedScale = 0.88f;
                    AIHighPriorityMoveSpeedScale = 0.92f;
                    break;
            }
        }

        private void ApplyMapIntelligenceDefaults(BrawlerArchetype archetype)
        {
            UseMapIntelligence = true;
            MapDestinationSearchRadius = 3f;
            MapBushPreference = 9f;
            MapCoverPreference = 7f;
            MapLineOfSightCoverPreference = 10f;
            MapExposedPositionPenalty = 7f;
            MapOpenShotPreference = 6f;
            MapChokepointPenalty = 12f;
            MapThreatAvoidanceWeight = 4f;
            MapPathCostWeight = 0.2f;
            MapCoverPeekPreference = 8f;
            MapLaneControlPreference = 7f;
            MapChokeControlPreference = 9f;
            MapThrowerSafePositionPreference = 12f;
            MapWallPressurePreference = 8f;
            MapWallHugPenalty = 9f;
            MapEscapeSpacePreference = 5f;
            MapCoverDancePreference = 5f;
            MapFireLanePressurePreference = 5f;
            MapThrowerSpacingPreference = 7f;
            LogMapIntelligence = false;

            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                    MapBushPreference = 12f;
                    MapCoverPreference = 11f;
                    MapLineOfSightCoverPreference = 14f;
                    MapExposedPositionPenalty = 10f;
                    MapOpenShotPreference = 9f;
                    MapChokepointPenalty = 16f;
                    MapThreatAvoidanceWeight = 5f;
                    MapCoverPeekPreference = 12f;
                    MapLaneControlPreference = 10f;
                    MapChokeControlPreference = 7f;
                    MapWallPressurePreference = 11f;
                    MapWallHugPenalty = 12f;
                    MapEscapeSpacePreference = 7f;
                    MapCoverDancePreference = 8f;
                    MapFireLanePressurePreference = 11f;
                    MapThrowerSpacingPreference = 0f;
                    break;

                case BrawlerArchetype.Tank:
                    MapBushPreference = 5f;
                    MapCoverPreference = 4f;
                    MapLineOfSightCoverPreference = 5f;
                    MapExposedPositionPenalty = 3f;
                    MapOpenShotPreference = 4f;
                    MapChokepointPenalty = 6f;
                    MapThreatAvoidanceWeight = 2.5f;
                    MapCoverPeekPreference = 4f;
                    MapLaneControlPreference = 8f;
                    MapChokeControlPreference = 13f;
                    MapThrowerSafePositionPreference = 4f;
                    MapWallPressurePreference = 5f;
                    MapWallHugPenalty = 5f;
                    MapEscapeSpacePreference = 4f;
                    MapCoverDancePreference = 2f;
                    MapFireLanePressurePreference = 3f;
                    MapThrowerSpacingPreference = 0f;
                    break;

                case BrawlerArchetype.Assassin:
                    MapBushPreference = 14f;
                    MapCoverPreference = 6f;
                    MapLineOfSightCoverPreference = 7f;
                    MapExposedPositionPenalty = 5f;
                    MapOpenShotPreference = 7f;
                    MapChokepointPenalty = 8f;
                    MapThreatAvoidanceWeight = 3.5f;
                    MapCoverPeekPreference = 9f;
                    MapLaneControlPreference = 7f;
                    MapChokeControlPreference = 6f;
                    MapWallPressurePreference = 7f;
                    MapWallHugPenalty = 8f;
                    MapEscapeSpacePreference = 6f;
                    MapCoverDancePreference = 7f;
                    MapFireLanePressurePreference = 5f;
                    MapThrowerSpacingPreference = 0f;
                    break;

                case BrawlerArchetype.Support:
                    MapBushPreference = 10f;
                    MapCoverPreference = 10f;
                    MapLineOfSightCoverPreference = 13f;
                    MapExposedPositionPenalty = 9f;
                    MapOpenShotPreference = 6f;
                    MapChokepointPenalty = 15f;
                    MapThreatAvoidanceWeight = 5f;
                    MapCoverPeekPreference = 8f;
                    MapLaneControlPreference = 9f;
                    MapChokeControlPreference = 9f;
                    MapWallPressurePreference = 7f;
                    MapWallHugPenalty = 11f;
                    MapEscapeSpacePreference = 8f;
                    MapCoverDancePreference = 6f;
                    MapFireLanePressurePreference = 5f;
                    MapThrowerSpacingPreference = 0f;
                    break;

                case BrawlerArchetype.Controller:
                    MapBushPreference = 8f;
                    MapCoverPreference = 9f;
                    MapLineOfSightCoverPreference = 11f;
                    MapExposedPositionPenalty = 7f;
                    MapOpenShotPreference = 8f;
                    MapChokepointPenalty = 10f;
                    MapThreatAvoidanceWeight = 4f;
                    MapCoverPeekPreference = 10f;
                    MapLaneControlPreference = 12f;
                    MapChokeControlPreference = 15f;
                    MapWallPressurePreference = 9f;
                    MapWallHugPenalty = 9f;
                    MapEscapeSpacePreference = 6f;
                    MapCoverDancePreference = 7f;
                    MapFireLanePressurePreference = 8f;
                    MapThrowerSpacingPreference = 0f;
                    break;

                case BrawlerArchetype.Artillery:
                    MapBushPreference = 11f;
                    MapCoverPreference = 13f;
                    MapLineOfSightCoverPreference = 16f;
                    MapExposedPositionPenalty = 11f;
                    MapOpenShotPreference = 0f;
                    MapChokepointPenalty = 17f;
                    MapThreatAvoidanceWeight = 5.5f;
                    MapCoverPeekPreference = 4f;
                    MapLaneControlPreference = 9f;
                    MapChokeControlPreference = 13f;
                    MapThrowerSafePositionPreference = 18f;
                    MapWallPressurePreference = 14f;
                    MapWallHugPenalty = 11f;
                    MapEscapeSpacePreference = 7f;
                    MapCoverDancePreference = 5f;
                    MapFireLanePressurePreference = 0f;
                    MapThrowerSpacingPreference = 13f;
                    break;
            }
        }

        private void ApplyReactiveCombatDefaults(BrawlerArchetype archetype)
        {
            ReactiveDamageMemoryTicks = 45;
            ReactiveRetreatPressureBonus = 38f;
            ReactiveRepositionPressureBonus = 28f;
            ReactiveAttackerFocusBonus = 18f;
            ReactiveEmergencyHealthRatio = 0.40f;
            LogReactiveEvents = false;

            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                    ReactiveRetreatPressureBonus = 48f;
                    ReactiveRepositionPressureBonus = 38f;
                    ReactiveAttackerFocusBonus = 14f;
                    ReactiveEmergencyHealthRatio = 0.48f;
                    break;

                case BrawlerArchetype.Tank:
                    ReactiveRetreatPressureBonus = 22f;
                    ReactiveRepositionPressureBonus = 16f;
                    ReactiveAttackerFocusBonus = 22f;
                    ReactiveEmergencyHealthRatio = 0.26f;
                    break;

                case BrawlerArchetype.Assassin:
                    ReactiveRetreatPressureBonus = 28f;
                    ReactiveRepositionPressureBonus = 30f;
                    ReactiveAttackerFocusBonus = 28f;
                    ReactiveEmergencyHealthRatio = 0.34f;
                    break;

                case BrawlerArchetype.Support:
                    ReactiveRetreatPressureBonus = 44f;
                    ReactiveRepositionPressureBonus = 34f;
                    ReactiveAttackerFocusBonus = 14f;
                    ReactiveEmergencyHealthRatio = 0.46f;
                    break;

                case BrawlerArchetype.Controller:
                    ReactiveRetreatPressureBonus = 36f;
                    ReactiveRepositionPressureBonus = 32f;
                    ReactiveAttackerFocusBonus = 18f;
                    ReactiveEmergencyHealthRatio = 0.40f;
                    break;

                case BrawlerArchetype.Artillery:
                    ReactiveRetreatPressureBonus = 50f;
                    ReactiveRepositionPressureBonus = 42f;
                    ReactiveAttackerFocusBonus = 12f;
                    ReactiveEmergencyHealthRatio = 0.50f;
                    break;
            }
        }

        private void ApplyDangerAvoidanceDefaults(BrawlerArchetype archetype)
        {
            DangerScanRadius = 7f;
            DangerRefreshIntervalTicks = 4;
            DangerPersonalSpace = 0.45f;
            DangerReactionTimeSeconds = 0.55f;
            DangerEvadePressureThreshold = 0.36f;
            DangerEvadeScoreBonus = 50f;
            DangerEvadeDistance = 1.8f;
            DangerEvadeRetargetTicks = 10;
            DangerThreatStaleDistance = 1.25f;
            DangerMapSearchRadius = 1.2f;
            LogDangerAvoidance = false;

            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                    DangerScanRadius = 7.5f;
                    DangerReactionTimeSeconds = 0.65f;
                    DangerEvadePressureThreshold = 0.32f;
                    DangerEvadeScoreBonus = 58f;
                    DangerEvadeDistance = 2.1f;
                    DangerEvadeRetargetTicks = 9;
                    break;

                case BrawlerArchetype.Tank:
                    DangerScanRadius = 5.5f;
                    DangerReactionTimeSeconds = 0.45f;
                    DangerEvadePressureThreshold = 0.44f;
                    DangerEvadeScoreBonus = 34f;
                    DangerEvadeDistance = 1.4f;
                    DangerEvadeRetargetTicks = 12;
                    DangerMapSearchRadius = 1.0f;
                    break;

                case BrawlerArchetype.Assassin:
                    DangerScanRadius = 6.5f;
                    DangerReactionTimeSeconds = 0.55f;
                    DangerEvadePressureThreshold = 0.34f;
                    DangerEvadeScoreBonus = 48f;
                    DangerEvadeDistance = 1.8f;
                    DangerEvadeRetargetTicks = 9;
                    break;

                case BrawlerArchetype.Support:
                    DangerScanRadius = 7f;
                    DangerReactionTimeSeconds = 0.60f;
                    DangerEvadePressureThreshold = 0.34f;
                    DangerEvadeScoreBonus = 56f;
                    DangerEvadeDistance = 2.0f;
                    DangerEvadeRetargetTicks = 10;
                    break;

                case BrawlerArchetype.Controller:
                    DangerScanRadius = 6.5f;
                    DangerReactionTimeSeconds = 0.55f;
                    DangerEvadePressureThreshold = 0.36f;
                    DangerEvadeScoreBonus = 50f;
                    DangerEvadeDistance = 1.8f;
                    DangerEvadeRetargetTicks = 10;
                    break;

                case BrawlerArchetype.Artillery:
                    DangerScanRadius = 7.75f;
                    DangerReactionTimeSeconds = 0.70f;
                    DangerEvadePressureThreshold = 0.32f;
                    DangerEvadeScoreBonus = 60f;
                    DangerEvadeDistance = 2.2f;
                    DangerEvadeRetargetTicks = 10;
                    break;
            }
        }

        private void ApplyFailureRecoveryDefaults(BrawlerArchetype archetype)
        {
            EnableFailureRecovery = true;
            NavigationStuckSampleIntervalTicks = 8;
            NavigationStuckMoveThreshold = 0.08f;
            NavigationStuckSampleLimit = 2;
            BlockedRouteRecoveryLimit = 1;
            StaleDestinationRecoveryTicks = 90;
            StaleDestinationProgressThreshold = 0.6f;
            FailureRecoveryCooldownTicks = 18;
            FailureRecoveryDetourDistance = 1.8f;
            IdleHesitationRecoveryTicks = 14;
            IdleHesitationCooldownTicks = 24;
            IdleHesitationLowScoreThreshold = 8f;
            FailedCastMemoryTicks = 60;
            FailedCastRecoveryLimit = 2;
            FailedCastSuppressionTicks = 30;
            LogFailureRecovery = false;

            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                case BrawlerArchetype.Support:
                case BrawlerArchetype.Artillery:
                    FailureRecoveryCooldownTicks = 16;
                    FailureRecoveryDetourDistance = 2.2f;
                    StaleDestinationRecoveryTicks = 75;
                    break;

                case BrawlerArchetype.Tank:
                    FailureRecoveryCooldownTicks = 22;
                    FailureRecoveryDetourDistance = 1.4f;
                    FailedCastSuppressionTicks = 24;
                    break;

                case BrawlerArchetype.Assassin:
                    FailureRecoveryCooldownTicks = 14;
                    FailureRecoveryDetourDistance = 2.0f;
                    StaleDestinationRecoveryTicks = 70;
                    break;
            }
        }

        private void ApplyProductionBudgetDefaults(BrawlerArchetype archetype)
        {
            EnableAIBudgetEnforcement = true;
            EnableValidationTelemetry = true;
            EnableDebugSnapshots = true;
            DebugSnapshotIntervalTicks = 5;
            MaxPerceptionScansPerTick = 8;
            MaxDangerRefreshesPerTick = 8;
            MaxMapResolvesPerTick = 24;
            MaxPathQueriesPerTick = 12;
            MaxPathTouchedNodesPerTick = 5000;
            BudgetDeferredSenseTicks = 2;
            BudgetDeferredDangerTicks = 2;
            BudgetDeferredPathTicks = 2;
            PathBudgetStarvationLimit = 3;
            AllowCriticalBudgetOverspend = true;
            LogBudgetWarnings = false;

            switch (archetype)
            {
                case BrawlerArchetype.Sniper:
                case BrawlerArchetype.Support:
                case BrawlerArchetype.Artillery:
                    MaxDangerRefreshesPerTick = 10;
                    break;

                case BrawlerArchetype.Tank:
                    MaxDangerRefreshesPerTick = 6;
                    BudgetDeferredDangerTicks = 3;
                    break;
            }
        }
    }
}
