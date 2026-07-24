using UnityEngine;
using UnityEngine.SceneManagement;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MOBA.Core.Infrastructure
{
    public class BrawlerAIController : SimulationEntity
    {
        // AI runs in InputApply phase — earlier than Movement. This guarantees
        // that commands produced by the AI (via _commandSource) are queued BEFORE
        // BrawlerController.Tick (Movement phase) calls CollectCommands to consume
        // them. Previously this ordering was an accident of Unity component order.
        protected override TickPhase Phase => TickPhase.InputApply;

        [SerializeField] private BrawlerController _brawler;
        [Header("Runtime Performance")]
        [Tooltip("Custom uses the explicit difficulty/personality below. Named tiers can be changed in Play Mode and rebuild AI tuning immediately.")]
        [SerializeField] private AIBotPerformanceTier _performanceTier = AIBotPerformanceTier.Elite;
        [SerializeField] private AIDifficultyLevel _difficulty = AIDifficultyLevel.Hard;
        [SerializeField] private AIPersonalityType _personality = AIPersonalityType.Aggressive;
        [SerializeField] private AITuningCatalog _tuningCatalog;

#if UNITY_EDITOR
        [Header("AI Scene Debug")]
        [Tooltip("Draw editor-only AI decision/movement gizmos while the game is running.")]
        [SerializeField] private bool _drawSceneDebug = true;
        [Tooltip("If true, every bot draws concise debug markers in Scene view without selecting it.")]
        [SerializeField] private bool _drawSceneDebugWhenNotSelected = true;
        [Tooltip("Draw compact labels with action, target, objective, and navigation state.")]
        [SerializeField] private bool _drawSceneDebugLabels = true;
        [Tooltip("Draw a line from the bot to its current combat target.")]
        [SerializeField] private bool _drawSceneDebugTargetLinks = true;
        [Tooltip("Draw the actual queued movement vector after smoothing/avoidance.")]
        [SerializeField] private bool _drawSceneDebugMoveVector = true;
        [Tooltip("Height above the bot for Scene view debug labels.")]
        [SerializeField] private float _sceneDebugLabelHeight = 2.15f;

        [Header("AI Runtime Debug (Read Only)")]
        [Tooltip("Mirrors the current AI decision state into inspector fields during Play Mode.")]
        [SerializeField] private bool _mirrorRuntimeDebugToInspector = true;
        [SerializeField] private uint _inspectorDebugTick;
        [SerializeField] private string _inspectorDebugBrawler;
        [SerializeField] private string _inspectorTacticalIntentSummary;
        [SerializeField] private AIActionType _inspectorCurrentAction;
        [SerializeField] private float _inspectorCurrentActionScore;
        [SerializeField] private string _inspectorDecisionPerformanceRank;
        [SerializeField] private float _inspectorDecisionPerformanceScore;
        [SerializeField] private AITacticalMovementIntent _inspectorTacticalIntent;
        [SerializeField] private string _inspectorTarget;
        [SerializeField] private string _inspectorRouteState;
        [SerializeField] private Vector3 _inspectorDestination;
        [SerializeField] private Vector3 _inspectorWaypoint;
        [SerializeField] private Vector3 _inspectorMoveDirection;
        [SerializeField] private float _inspectorDistanceToDestination;
        [SerializeField] private int _inspectorPathIndex;
        [SerializeField] private int _inspectorPathNodeCount;
        [SerializeField] private bool _inspectorRouteBlocked;
        [SerializeField] private int _inspectorStuckSamples;
        [SerializeField] private int _inspectorPathBudgetDeferrals;
        [TextArea(2, 5)]
        [SerializeField] private string _inspectorDecisionDetails;
        [TextArea(2, 4)]
        [SerializeField] private string _inspectorDecisionPerformanceBreakdown;
        [TextArea(2, 4)]
        [SerializeField] private string _inspectorPerformanceTierDetails;
        [TextArea(2, 5)]
        [SerializeField] private string _inspectorMovementDetails;
        [TextArea(2, 5)]
        [SerializeField] private string _inspectorObjectiveDetails;
        [TextArea(2, 5)]
        [SerializeField] private string _inspectorTeamDetails;
        [TextArea(2, 5)]
        [SerializeField] private string _inspectorRecoveryAndPerfDetails;
#endif

        private static AIObjectivePoint[] _cachedObjectivePoints;
        private static int _cachedObjectiveSceneHandle = -1;

        private NavigationAgent _navAgent;
        private AIPerception _perception;
        private AITargetInfo _targetInfo;
        private AITargetScorer _targetScorer;
        private AIAbilityDecider _abilityDecider;
        private AISuperDecider _superDecider;
        private AIUtilityScorer _utilityScorer;
        private AIActionCommitment _actionCommitment;
        private AIActionExecutor _actionExecutor;
        private AIObjectiveMemory _objectiveMemory;
        private AITeamCoordinator _teamCoordinator;
        private AIReactiveMemory _reactiveMemory;
        private AIReactiveListener _reactiveListener;
        private AIDangerMemory _dangerMemory;
        private AIFailureRecoveryMemory _failureRecovery;
        private AIFailureRecoveryListener _failureRecoveryListener;
        private AIIdleHesitationMemory _idleHesitation;
        private AIHumanizationController _humanization;
        private AICommandSource _commandSource;

        private BrawlerAIProfile _baseProfileSource;
        private BrawlerAIProfile _profile;
        private AITuningCatalog _activeTuningCatalog;
        private int _runtimeTuningVersion = -1;
        private AIBotPerformanceTier _appliedPerformanceTier = AIBotPerformanceTier.Elite;
        private AIDifficultyLevel _appliedDifficulty = AIDifficultyLevel.Hard;
        private AIPersonalityType _appliedPersonality = AIPersonalityType.Aggressive;

        private uint _nextSenseTick;
        private uint _nextDangerRefreshTick;
        private uint _nextDebugSnapshotTick;
        private uint _nextBudgetWarningTick;
        private bool _brainInitialized;
        private string _lastReactiveDebug = "Reactive=None";
        private string _lastDangerDebug = "Danger=None";
        private string _lastFailureRecoveryDebug = "Recovery=None";
        private string _lastIdleHesitationDebug = "Idle=None";
        private string _lastTuningDebug = "Tuning=None";
        private string _lastOpponentModelDebug = "Opponent=None";
        private string _lastBudgetDebug = "Budget=OK map=0/0 paths=0/0 nodes=0/0 maxNodes=0";
        private string _lastTacticalIntentSummary = "Intent=None";
        private readonly AIDebugSnapshot _debugSnapshot = new AIDebugSnapshot();
        private readonly System.Collections.Generic.List<AIActionScore> _debugScores = new System.Collections.Generic.List<AIActionScore>(16);
        private AIActionScore _lastChosenAction;

        public bool HasObjectiveDebug =>
      _actionExecutor != null && _actionExecutor.HasObjectiveDebug;

        public Vector3 LastObjectiveCenter =>
            _actionExecutor != null ? _actionExecutor.LastObjectiveCenter : Vector3.zero;

        public Vector3 LastObjectiveSlot =>
            _actionExecutor != null ? _actionExecutor.LastObjectiveSlot : Vector3.zero;

        public Vector3 LastObjectiveDestination =>
            _actionExecutor != null ? _actionExecutor.LastObjectiveDestination : Vector3.zero;

        public string LastObjectiveName =>
            _actionExecutor != null ? _actionExecutor.LastObjectiveName : string.Empty;

        public AIObjectiveType LastObjectiveType =>
            _actionExecutor != null ? _actionExecutor.LastObjectiveType : AIObjectiveType.None;

        public float LastObjectiveRadius =>
            _actionExecutor != null ? _actionExecutor.LastObjectiveRadius : 0f;

        public bool LastObjectiveIsRuntime =>
            _actionExecutor != null && _actionExecutor.LastObjectiveIsRuntime;

        public AIObjectiveControlState LastObjectiveControlState =>
            _actionExecutor != null
                ? _actionExecutor.LastObjectiveControlState
                : AIObjectiveControlState.Unknown;

        public int LastObjectiveFriendlyPresence =>
            _actionExecutor != null ? _actionExecutor.LastObjectiveFriendlyPresence : 0;

        public int LastObjectiveEnemyPresence =>
            _actionExecutor != null ? _actionExecutor.LastObjectiveEnemyPresence : 0;

        public AIObjectiveSlotRole LastObjectiveSlotRole =>
            _actionExecutor != null
                ? _actionExecutor.LastObjectiveSlotRole
                : AIObjectiveSlotRole.Default;

        public AIObjectiveSlotRole LastObjectiveDesiredSlotRole =>
            _actionExecutor != null
                ? _actionExecutor.LastObjectiveDesiredSlotRole
                : AIObjectiveSlotRole.Default;

        public string LastObjectiveSlotCommitmentDebug =>
            _actionExecutor != null
                ? _actionExecutor.LastObjectiveSlotCommitmentDebug
                : "SlotCommit=None";

        public float LastObjectiveAllyPressure =>
            _utilityScorer != null ? _utilityScorer.LastObjectiveAllyPressure : 0f;

        public float LastObjectiveCrowdingPenalty =>
            _utilityScorer != null ? _utilityScorer.LastObjectiveCrowdingPenalty : 0f;

        public float LastObjectiveRawScore =>
            _utilityScorer != null ? _utilityScorer.LastObjectiveRawScore : 0f;

        public float LastObjectiveFinalScore =>
            _utilityScorer != null ? _utilityScorer.LastObjectiveFinalScore : 0f;

        public string LastObjectiveScoreReason =>
            _utilityScorer != null ? _utilityScorer.LastObjectiveScoreReason : string.Empty;

        public AITacticalMovementIntent LastTacticalMovementIntent =>
_actionExecutor != null
    ? _actionExecutor.LastTacticalMovementIntent
    : AITacticalMovementIntent.None;

        public Vector3 LastTacticalMoveDestination =>
            _actionExecutor != null
                ? _actionExecutor.LastTacticalMoveDestination
                : Vector3.zero;

        public float LastTacticalTargetDistance =>
            _actionExecutor != null
                ? _actionExecutor.LastTacticalTargetDistance
                : 0f;

        public float LastTacticalPreferredRange =>
            _actionExecutor != null
                ? _actionExecutor.LastTacticalPreferredRange
                : 0f;

        public float LastTacticalTooCloseDistance =>
            _actionExecutor != null
                ? _actionExecutor.LastTacticalTooCloseDistance
                : 0f;

        public uint LastTacticalRetargetTick =>
            _actionExecutor != null
                ? _actionExecutor.LastTacticalRetargetTick
                : 0u;

        public uint NextTacticalMoveRetargetTick =>
            _actionExecutor != null
                ? _actionExecutor.NextTacticalMoveRetargetTick
                : 0u;

        public string LastTacticalMoveReason =>
            _actionExecutor != null
                ? _actionExecutor.LastTacticalMoveReason
                : string.Empty;

        public string LastMapRouteDebug =>
            _actionExecutor != null
                ? _actionExecutor.LastMapRouteDebug
                : string.Empty;
        public string LastTacticalStopDebug =>
            _actionExecutor != null
                ? _actionExecutor.LastTacticalStopDebug
                : "Stop=None";

        public AIDifficultyLevel Difficulty => _profile != null ? _profile.Difficulty : _difficulty;
        public AIPersonalityType Personality => _profile != null ? _profile.Personality : _personality;
        public AIBotPerformanceTier PerformanceTier => _performanceTier;
        public string ReactiveDebug => _lastReactiveDebug;
        public string DangerDebug => _lastDangerDebug;
        public string FailureRecoveryDebug => $"{_lastFailureRecoveryDebug} {_lastIdleHesitationDebug}";
        public string TeamRoleDebug =>
            _utilityScorer != null ? _utilityScorer.LastTeamRoleDebug : "RoleCoord=None";
        public string MacroDebug =>
            _utilityScorer != null
                ? $"{_utilityScorer.LastMacroDebug} {_utilityScorer.LastObjectiveIntentDebug} {_utilityScorer.LastWinConditionDebug}"
                : "Macro=None ObjIntent=None Win=None";
        public string PlaybookDebug =>
            _utilityScorer != null ? _utilityScorer.LastPlaybookDebug : "Playbook=None";
        public string ChaseDebug =>
            _utilityScorer != null ? _utilityScorer.LastChaseDebug : "Chase=None";
        public string GemPickupDebug =>
            _utilityScorer != null ? _utilityScorer.LastGemPickupDebug : "GemPickup=None";
        public string ResourceAwarenessDebug =>
            _utilityScorer != null ? _utilityScorer.LastResourceAwarenessDebug : "ResAware=None";
        public string TeamFightDebug =>
            _utilityScorer != null ? _utilityScorer.LastTeamFightDebug : "TeamFight=None";
        public string RoleMacroDebug =>
            _utilityScorer != null ? _utilityScorer.LastRoleMacroDebug : "RoleMacro=None";
        public string RoleMatchupDebug =>
            _utilityScorer != null ? _utilityScorer.LastRoleMatchupDebug : "Matchup=None";
        public string BrawlerIdentityDebug =>
            _utilityScorer != null ? _utilityScorer.LastBrawlerIdentityDebug : "Identity=None";
        public string CoverPeekDebug =>
            _utilityScorer != null ? _utilityScorer.LastCoverPeekDebug : "CoverPeek=None";
        public string ModeClutchDebug =>
            _utilityScorer != null ? _utilityScorer.LastModeClutchDebug : "Clutch=None";
        public string SpacingDebug =>
            _utilityScorer != null ? _utilityScorer.LastSpacingDebug : "Spacing=None";
        public string AbilityThreatDebug =>
            _utilityScorer != null ? _utilityScorer.LastAbilityThreatDebug : "ThreatPred=None";
        public string EngagementRiskDebug =>
            _utilityScorer != null ? _utilityScorer.LastEngagementRiskDebug : "EngageRisk=None";
        public string PressureRotationDebug =>
            _utilityScorer != null ? _utilityScorer.LastPressureRotationDebug : "PressureRot=None";
        public string TargetContextDebug =>
            _targetScorer != null ? _targetScorer.LastTargetContextDebug : "TargetCtx=None";
        public string DecisionConfidenceDebug =>
            _actionCommitment != null ? _actionCommitment.LastDecisionConfidenceDebug : "DecisionConf=None";
        public string OpponentModelDebug => _lastOpponentModelDebug;
        public string HumanizationDebug =>
            _humanization != null ? _humanization.DebugSummary : "Human=None";
        public string TuningDebug => _lastTuningDebug;
        public string TacticalIntentSummary => _lastTacticalIntentSummary;

        public int CurrentTargetFocusCount =>
            _teamCoordinator != null &&
            _targetInfo != null &&
            _targetInfo.HasLiveTarget &&
            _targetInfo.Target != null
                ? _teamCoordinator.GetTargetFocusCount(_targetInfo.Target.EntityID)
                : 0;

        public int CurrentTargetAllyFocusCount
        {
            get
            {
                if (_targetScorer == null ||
                    _targetInfo == null ||
                    !_targetInfo.HasLiveTarget ||
                    _targetInfo.Target == null)
                {
                    return 0;
                }

                _targetScorer.CalculateOverFocusedTargetPenalty(
                    _targetInfo.Target.EntityID,
                    out int alliedFocusCount);

                return alliedFocusCount;
            }
        }

        public float CurrentTargetOverFocusPenalty
        {
            get
            {
                if (_targetScorer == null ||
                    _targetInfo == null ||
                    !_targetInfo.HasLiveTarget ||
                    _targetInfo.Target == null)
                {
                    return 0f;
                }

                return _targetScorer.CalculateOverFocusedTargetPenalty(
                    _targetInfo.Target.EntityID,
                    out _);
            }
        }

        /// ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public void SetTarget(BrawlerController brawler)
        {
            _brawler = brawler;
            TryInitializeBrain();
        }

        protected override void Awake()
        {
            base.Awake();

            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();

            TryInitializeBrain();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_brainInitialized)
            {
                EnsureReactiveListener();
                EnsureFailureRecoveryListener();
            }
        }

        public override void Tick(uint currentTick)
        {
            if (!CanRunAI())
            {
                ResetTransientAIState();

                if (_profile != null && _profile.LogDecisionTicks && currentTick % 30 == 0)
                    Debug.Log($"[AI-{(_brawler != null ? _brawler.name : "?")}] CanRunAI=false brain={_brainInitialized} brawlerNull={_brawler == null} stateNull={(_brawler == null ? "?" : (_brawler.State == null).ToString())} dead={(_brawler == null || _brawler.State == null ? "?" : _brawler.State.IsDead.ToString())} gridNull={SimulationClock.Grid == null}");

#if UNITY_EDITOR
                UpdateInspectorRuntimeDebug(currentTick, "CanRunAI=false");
#endif
                return;
            }

            if (_profile.LogDecisionTicks && currentTick % 30 == 0)
                Debug.Log($"[AI-{_brawler.name}] tick ok hasTarget={_targetInfo.HasLiveTarget} action={_lastChosenAction.ActionType} score={_lastChosenAction.Score:0.0}");

            ApplyPerformanceTierSelection();
            RefreshRuntimeTuningIfNeeded(currentTick);

            if (_brawler.State.HasStatus(StatusEffectType.Stun))
            {
                _actionCommitment?.Reset();
                _humanization?.Reset();
                _idleHesitation?.Reset();
                _teamCoordinator?.ClearTargetFocusCount();
                _teamCoordinator?.ClearActionIntent();
                _teamCoordinator?.ClearLaneOwnership();
                _commandSource?.QueueMove(Vector3.zero);
#if UNITY_EDITOR
                UpdateInspectorRuntimeDebug(currentTick, "Stunned");
#endif
                return;
            }

            if (currentTick >= _nextSenseTick)
            {
                if (AIBudgetCoordinator.TryAcquirePerceptionScan(
                        currentTick,
                        _profile,
                        highPriority: _targetInfo != null && _targetInfo.HasLiveTarget))
                {
                    _perception.UpdateTarget(_brawler, _targetInfo, currentTick);
                    ScheduleNextSense(currentTick);
                }
                else
                {
                    DeferSense(currentTick);
                }
            }

            _teamCoordinator.UpdateTeamSignals(_targetInfo, currentTick);
            UpdateOpponentModel(currentTick);
            ReportCurrentTargetFocus();
            RefreshDangerIfDue(currentTick);

            _utilityScorer.CollectActionScores(_targetInfo, currentTick, _debugScores);
            _humanization?.ShapeActionScores(
                _debugScores,
                currentTick,
                _targetInfo.HasLiveTarget,
                GetHealthRatio(),
                _dangerMemory != null && _dangerMemory.HasDanger);

            AIActionScore chosenAction = _actionCommitment.SelectAction(
                _debugScores,
                currentTick,
                _brawler.name);

            _lastChosenAction = chosenAction;
            _teamCoordinator?.ReportActionIntent(chosenAction.ActionType, currentTick);

            if (_profile.EnableValidationTelemetry)
            {
                AIValidationTelemetry.RecordDecision(
                    _brawler.EntityID,
                    currentTick,
                    chosenAction,
                    _targetInfo.HasLiveTarget,
                    _debugScores,
                    TeamRoleDebug);
            }

            _actionExecutor.Execute(
                chosenAction.ActionType,
                _targetInfo,
                currentTick,
                GetAbilityMaxRange(),
                GetAbilityIdealRange(),
                GetSuperMaxRange());

            _navAgent.Tick();
            UpdateTacticalIntentSummary();

            UpdateIdleHesitation(currentTick);
            UpdateFailureRecovery(currentTick);

            UpdateProductionBudget(currentTick);
            UpdateDebugSnapshotIfDue(currentTick);
#if UNITY_EDITOR
            UpdateInspectorRuntimeDebug(currentTick);
#endif
        }

        private void UpdateOpponentModel(uint currentTick)
        {
            _lastOpponentModelDebug = _brawler != null
                ? AIOpponentModel.GetBestDebugSummary(_brawler.Team, currentTick)
                : "Opponent=None";

            if (_brawler == null ||
                _targetInfo == null ||
                !_targetInfo.HasLiveTarget ||
                _targetInfo.Target is not BrawlerController opponent ||
                opponent.State == null ||
                opponent.State.IsDead)
            {
                return;
            }

            bool hasObjectivePoint = TryGetOpponentModelObjectivePoint(out Vector3 objectivePoint);
            float opponentHealthRatio =
                opponent.State.CurrentHealth /
                Mathf.Max(1f, opponent.State.MaxHealth.Value);

            AIOpponentModel.RecordMovementSample(
                _brawler.Team,
                opponent.EntityID,
                _brawler.Position,
                opponent.Position,
                opponentHealthRatio,
                hasObjectivePoint,
                objectivePoint,
                4.5f,
                currentTick);

            if (AIOpponentModel.TryGetSnapshot(
                    _brawler.Team,
                    opponent.EntityID,
                    currentTick,
                    360u,
                    out AIOpponentHabitSnapshot snapshot))
            {
                _lastOpponentModelDebug = snapshot.GetDebugSummary();
            }
        }

        private bool TryGetOpponentModelObjectivePoint(out Vector3 objectivePoint)
        {
            objectivePoint = default;

            if (_objectiveMemory == null)
            {
                return false;
            }

            if (!_objectiveMemory.TryGetBestObjective(
                    _brawler.Position,
                    _profile != null ? _profile.PreferredObjective : AIObjectiveType.None,
                    _brawler.Team,
                    out AIObjectiveCandidate objective))
            {
                return false;
            }

            objectivePoint = objective.Position;
            return true;
        }

        private void UpdateDebugSnapshot(uint currentTick)
        {
            if (_brawler == null || _brawler.State == null)
                return;

            _debugSnapshot.ClearLists();

            _debugSnapshot.BrawlerName = _brawler.Definition != null ? _brawler.Definition.BrawlerName : _brawler.name;
            _debugSnapshot.CurrentAction = $"{_lastChosenAction.ActionType} ({_lastChosenAction.Score:0.0})";
            _debugSnapshot.TacticalIntentSummary = TacticalIntentSummary;
            _debugSnapshot.Difficulty = Difficulty.ToString();
            _debugSnapshot.Personality = Personality.ToString();
            _debugSnapshot.ReactionDelayTicks = _profile != null ? _profile.ReactionDelayTicks : 0u;
            _debugSnapshot.AimErrorDegrees = _profile != null ? _profile.AimErrorDegrees : 0f;
            _debugSnapshot.Health = _brawler.State.CurrentHealth;
            _debugSnapshot.MaxHealth = _brawler.State.MaxHealth.Value;
            _debugSnapshot.Position = _brawler.Position;

            _debugSnapshot.IsStunned = _brawler.State.HasStatus(StatusEffectType.Stun);
            _debugSnapshot.IsBurning = _brawler.State.HasStatus(StatusEffectType.Burn);
            _debugSnapshot.IsSlowed = _brawler.State.HasStatus(StatusEffectType.Slow);
            _debugSnapshot.IsRevealed = _brawler.State.IsRevealed;

            if (_targetInfo.HasLiveTarget && _targetInfo.Target is BrawlerController targetBrawler)
            {
                _debugSnapshot.CurrentTargetName = targetBrawler.Definition != null
                    ? targetBrawler.Definition.BrawlerName
                    : targetBrawler.name;

                _debugSnapshot.CurrentTargetId = targetBrawler.EntityID;
                _debugSnapshot.TargetPosition = targetBrawler.Position;
            }
            else
            {
                _debugSnapshot.CurrentTargetName = "None";
                _debugSnapshot.CurrentTargetId = 0;
                _debugSnapshot.TargetPosition = null;
            }
            _debugSnapshot.CurrentTargetFocusCount = CurrentTargetFocusCount;
            _debugSnapshot.CurrentTargetAllyFocusCount = CurrentTargetAllyFocusCount;
            _debugSnapshot.CurrentTargetOverFocusPenalty = CurrentTargetOverFocusPenalty;
            _debugSnapshot.TargetContextDebug = TargetContextDebug;
            _debugSnapshot.DecisionConfidenceDebug = DecisionConfidenceDebug;

            if (_teamCoordinator != null)
            {
                if (_teamCoordinator.TryGetFocusTarget(currentTick, out var focusTarget) && focusTarget != null)
                {
                    _debugSnapshot.TeamTactic = $"FocusFire:{focusTarget.EntityID}";
                }
                else if (_teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) && ally != null)
                {
                    _debugSnapshot.TeamTactic = $"Peel:{ally.EntityID}";
                }
                else if (_teamCoordinator.TryGetRegroupPoint(currentTick, out _))
                {
                    _debugSnapshot.TeamTactic = "Regroup";
                }
                else
                {
                    _debugSnapshot.TeamTactic = "None";
                }

                string threatSignal = _teamCoordinator.TryGetThreatCenter(
                    currentTick,
                    out var threatCenter,
                    out float threatPressure)
                    ? $"Threat={FormatVector(threatCenter)} p={threatPressure:0.0}"
                    : "Threat=None";

                string hotspotSignal = _teamCoordinator.TryGetEnemyHotspot(
                    currentTick,
                    out var enemyHotspot,
                    out float hotspotPressure)
                    ? $"Hotspot={FormatVector(enemyHotspot)} p={hotspotPressure:0.0}"
                    : "Hotspot=None";

                _debugSnapshot.TeamSignalDebug = $"{threatSignal} {hotspotSignal}";
                _debugSnapshot.TeamRoleDebug = TeamRoleDebug;
                _debugSnapshot.PlaybookDebug = PlaybookDebug;
                _debugSnapshot.ChaseDebug = ChaseDebug;
                _debugSnapshot.GemPickupDebug = GemPickupDebug;
                _debugSnapshot.ResourceAwarenessDebug = ResourceAwarenessDebug;
                _debugSnapshot.TeamFightDebug = TeamFightDebug;
                _debugSnapshot.RoleMacroDebug = RoleMacroDebug;
                _debugSnapshot.RoleMatchupDebug = RoleMatchupDebug;
                _debugSnapshot.BrawlerIdentityDebug = BrawlerIdentityDebug;
                _debugSnapshot.CoverPeekDebug = CoverPeekDebug;
                _debugSnapshot.ModeClutchDebug = ModeClutchDebug;
                _debugSnapshot.SpacingDebug = SpacingDebug;
                _debugSnapshot.AbilityThreatDebug = AbilityThreatDebug;
                _debugSnapshot.EngagementRiskDebug = EngagementRiskDebug;
                _debugSnapshot.PressureRotationDebug = PressureRotationDebug;
            }
            else
            {
                _debugSnapshot.TeamTactic = "None";
                _debugSnapshot.TeamSignalDebug = "Threat=None Hotspot=None";
                _debugSnapshot.TeamRoleDebug = "RoleCoord=None";
                _debugSnapshot.MacroDebug = "Macro=None";
                _debugSnapshot.PlaybookDebug = "Playbook=None";
                _debugSnapshot.ChaseDebug = "Chase=None";
                _debugSnapshot.GemPickupDebug = "GemPickup=None";
                _debugSnapshot.ResourceAwarenessDebug = "ResAware=None";
                _debugSnapshot.TeamFightDebug = "TeamFight=None";
                _debugSnapshot.RoleMacroDebug = "RoleMacro=None";
                _debugSnapshot.RoleMatchupDebug = "Matchup=None";
                _debugSnapshot.BrawlerIdentityDebug = "Identity=None";
                _debugSnapshot.CoverPeekDebug = "CoverPeek=None";
                _debugSnapshot.ModeClutchDebug = "Clutch=None";
                _debugSnapshot.SpacingDebug = "Spacing=None";
                _debugSnapshot.AbilityThreatDebug = "ThreatPred=None";
                _debugSnapshot.EngagementRiskDebug = "EngageRisk=None";
                _debugSnapshot.PressureRotationDebug = "PressureRot=None";
            }

            _lastReactiveDebug = _reactiveMemory != null && _profile != null
                ? _reactiveMemory.GetDebugSummary(currentTick, _profile.ReactiveDamageMemoryTicks)
                : "Reactive=None";
            _debugSnapshot.ReactiveDebug = _lastReactiveDebug;
            _lastDangerDebug = _dangerMemory != null
                ? _dangerMemory.GetDebugSummary()
                : "Danger=None";
            _debugSnapshot.DangerDebug = _lastDangerDebug;
            _debugSnapshot.FailureRecoveryDebug = FailureRecoveryDebug;
            _debugSnapshot.HumanizationDebug = HumanizationDebug;
            _debugSnapshot.TuningDebug = TuningDebug;
            _debugSnapshot.OpponentModelDebug = OpponentModelDebug;

            _debugSnapshot.ObjectiveName = _profile != null ? _profile.PreferredObjective.ToString() : "None";

            for (int i = 0; i < _debugScores.Count; i++)
            {
                _debugSnapshot.ActionScores.Add(_debugScores[i]);
            }

            for (int i = 0; i < _brawler.State.ActiveStatusEffects.Count; i++)
            {
                _debugSnapshot.ActiveStatuses.Add(_brawler.State.ActiveStatusEffects[i].Type.ToString());
            }

            if (HasObjectiveDebug)
            {
                _debugSnapshot.ObjectiveDebug =
                    $"Obj={LastObjectiveName} " +
                    $"Type={LastObjectiveType} " +
                    $"Runtime={LastObjectiveIsRuntime} " +
                    $"Radius={LastObjectiveRadius:0.0} " +
                    $"Control={LastObjectiveControlState} " +
                    $"Presence={LastObjectiveFriendlyPresence}:{LastObjectiveEnemyPresence} " +
                    $"SlotRole={LastObjectiveSlotRole} " +
                    $"DesiredRole={LastObjectiveDesiredSlotRole} " +
                    $"Center={FormatVector(LastObjectiveCenter)} " +
                    $"Slot={FormatVector(LastObjectiveSlot)} " +
                    $"Dest={FormatVector(LastObjectiveDestination)} " +
                    $"Pressure={LastObjectiveAllyPressure:0.00} " +
                    $"Penalty={LastObjectiveCrowdingPenalty:0.0} " +
                    $"Raw={LastObjectiveRawScore:0.0} " +
                    $"Final={LastObjectiveFinalScore:0.0} " +
                    $"{LastObjectiveSlotCommitmentDebug} " +
                    $"Reason={LastObjectiveScoreReason}";

            }
            else
            {
                _debugSnapshot.ObjectiveDebug =
                    $"Pressure={LastObjectiveAllyPressure:0.00} " +
                    $"Penalty={LastObjectiveCrowdingPenalty:0.0} " +
                    $"Raw={LastObjectiveRawScore:0.0} " +
                    $"Final={LastObjectiveFinalScore:0.0} " +
                    $"Reason={LastObjectiveScoreReason}";
            }
            _debugSnapshot.TacticalMovementDebug =
$"Intent={LastTacticalMovementIntent} " +
$"Dest={FormatVector(LastTacticalMoveDestination)} " +
$"Dist={LastTacticalTargetDistance:0.0} " +
$"Preferred={LastTacticalPreferredRange:0.0} " +
$"TooClose={LastTacticalTooCloseDistance:0.0} " +
$"Retarget={LastTacticalRetargetTick}->{NextTacticalMoveRetargetTick} " +
$"Reason={LastTacticalMoveReason} " +
$"NavZero={(_navAgent != null ? _navAgent.ConsecutiveActiveZeroMoveTicks : 0)} " +
$"PathDefers={(_navAgent != null ? _navAgent.ConsecutivePathBudgetDeferrals : 0)} " +
$"{LastTacticalStopDebug} " +
$"Map={LastMapRouteDebug}";
            _debugSnapshot.NavigationDebug = BuildNavigationDebug();

            _debugSnapshot.PerformanceDebug = AIPerformanceTracker.GetDebugSummary(currentTick);
            _debugSnapshot.IncidentDebug = AIIncidentLogger.GetDebugSummary(_brawler.EntityID);
            _lastBudgetDebug = BuildBudgetSummary(currentTick);
            _debugSnapshot.ProductionBudgetDebug = _lastBudgetDebug;
            _debugSnapshot.ValidationDebug = _profile.EnableValidationTelemetry
                ? AIValidationTelemetry.GetDebugSummary(currentTick)
                : "Valid=disabled";
            _debugSnapshot.ValidationHealthDebug = _profile.EnableValidationTelemetry
                ? AIValidationHealthTracker.GetDebugSummary()
                : "Health=disabled";
            _debugSnapshot.ValidationScenarioDebug = _profile.EnableValidationTelemetry
                ? AIValidationScenarioTracker.GetDebugSummary()
                : "Scenario=disabled";
            _debugSnapshot.ValidationGauntletDebug = _profile.EnableValidationTelemetry
                ? AIValidationGauntlet.GetDebugSummary()
                : "Gauntlet=disabled";
            _debugSnapshot.ReportCardDebug = AIReportCardTracker.GetBotDebugSummary(
                _brawler.EntityID,
                currentTick);
            if (_profile.EnableValidationTelemetry)
            {
                AIMatchTelemetryReviewSnapshot matchReview =
                    AIMatchTelemetryReview.Build(
                        currentTick,
                        new AIMatchTelemetryReviewLimits(
                            _profile.MaxMapResolvesPerTick,
                            _profile.MaxPathQueriesPerTick,
                            _profile.MaxPathTouchedNodesPerTick));
                _debugSnapshot.MatchTelemetryReviewDebug =
                    matchReview.GetDebugSummary();
                AIMatchTelemetryTrendSnapshot matchTrend =
                    AIMatchTelemetryTrendTracker.Record(matchReview);
                _debugSnapshot.MatchTelemetryTrendDebug =
                    matchTrend.GetDebugSummary();
                _debugSnapshot.BotTelemetryOutlierDebug =
                    AIBotTelemetryOutlierReview.Build(currentTick).GetDebugSummary();
                _debugSnapshot.AIReadinessDebug =
                    AIReadinessReview.Evaluate(
                        currentTick,
                        matchReview,
                        matchTrend,
                        AIValidationGauntlet.LastResult).GetDebugSummary();
            }
            else
            {
                _debugSnapshot.MatchTelemetryReviewDebug = "MatchReview=disabled";
                _debugSnapshot.MatchTelemetryTrendDebug = "MatchTrend=disabled";
                _debugSnapshot.BotTelemetryOutlierDebug = "BotOutlier=disabled";
                _debugSnapshot.AIReadinessDebug = "AIReady=disabled";
            }
            _debugSnapshot.MacroDebug = MacroDebug;
            _debugSnapshot.PlaybookDebug = PlaybookDebug;
            _debugSnapshot.RoleMacroDebug = RoleMacroDebug;
            _debugSnapshot.RoleMatchupDebug = RoleMatchupDebug;
            _debugSnapshot.BrawlerIdentityDebug = BrawlerIdentityDebug;
            _debugSnapshot.CoverPeekDebug = CoverPeekDebug;
            _debugSnapshot.ModeClutchDebug = ModeClutchDebug;
            _debugSnapshot.SpacingDebug = SpacingDebug;
            _debugSnapshot.AbilityThreatDebug = AbilityThreatDebug;
            _debugSnapshot.EngagementRiskDebug = EngagementRiskDebug;
            _debugSnapshot.PressureRotationDebug = PressureRotationDebug;

            AIDebugTracker.UpdateSnapshot(_brawler, _debugSnapshot);
        }

        private void UpdateDebugSnapshotIfDue(uint currentTick)
        {
            if (_profile == null || !_profile.EnableDebugSnapshots)
                return;

            if (currentTick < _nextDebugSnapshotTick)
                return;

            uint interval = _profile.DebugSnapshotIntervalTicks == 0u
                ? 1u
                : _profile.DebugSnapshotIntervalTicks;

            _nextDebugSnapshotTick = currentTick + interval;
            UpdateDebugSnapshot(currentTick);
        }

        private void TryInitializeBrain()
        {

            if (_brainInitialized || _brawler == null || _brawler.Definition == null)
                return;

            _profile = ResolveAIProfile(_brawler.Definition);
            AIReportCardTracker.RegisterBot(
                _brawler.EntityID,
                _brawler.Team,
                _brawler.Definition != null ? _brawler.Definition.BrawlerName : _brawler.name);

            _targetInfo = new AITargetInfo();

            _commandSource = new AICommandSource(_profile);
            _brawler.SetCommandSource(_commandSource);

            _navAgent = new NavigationAgent(_brawler, _commandSource, _profile);
            _targetScorer = new AITargetScorer(_brawler, _profile);
            _objectiveMemory = new AIObjectiveMemory();
            _teamCoordinator = new AITeamCoordinator(_brawler);
            _targetScorer.SetTeamCoordinator(_teamCoordinator);
            _reactiveMemory = new AIReactiveMemory();
            _dangerMemory = new AIDangerMemory();
            _failureRecovery = new AIFailureRecoveryMemory();
            _idleHesitation = new AIIdleHesitationMemory();
            _humanization = new AIHumanizationController(
                _profile,
                unchecked((uint)_brawler.EntityID));

            _perception = new AIPerception(
                _profile.DetectionRadius,
                _profile.MemoryDurationTicks,
                _targetScorer,
                _profile.LogPerception);
            _abilityDecider = new AIAbilityDecider(_brawler, _profile, _commandSource, _failureRecovery);
            _superDecider = new AISuperDecider(_brawler, _profile, _commandSource, _failureRecovery);

            _utilityScorer = new AIUtilityScorer(_brawler, _profile, _objectiveMemory, _teamCoordinator, _reactiveMemory, _dangerMemory);
            _actionCommitment = new AIActionCommitment(_profile);
            _actionExecutor = new AIActionExecutor(_brawler, _profile, _navAgent, _abilityDecider, _superDecider, _objectiveMemory, _teamCoordinator, _commandSource, _dangerMemory);
            EnsureReactiveListener();
            EnsureFailureRecoveryListener();

            AIObjectivePoint[] objectivePoints = GetSceneObjectivePoints();
            if (_profile.LogLifecycle)
            {
                Debug.Log(
                    $"[AI-{_brawler.name}] Registered Objectives: {objectivePoints.Length}");
            }

            for (int i = 0; i < objectivePoints.Length; i++)
            {
                _objectiveMemory.Register(objectivePoints[i]);
            }

            _nextSenseTick = (uint)Random.Range(0, 8);
            _nextDangerRefreshTick = (uint)Random.Range(
                0,
                Mathf.Max(1, (int)_profile.DangerRefreshIntervalTicks));
            _nextDebugSnapshotTick = (uint)Random.Range(
                0,
                Mathf.Max(1, (int)_profile.DebugSnapshotIntervalTicks));

            if (_profile.EnableDebugSnapshots)
                AIDebugTracker.Register(_brawler);

            _brainInitialized = true;
        }

        private BrawlerAIProfile ResolveAIProfile(BrawlerDefinition definition)
        {
            ApplyPerformanceTierSelection();

            BrawlerAIProfile baseProfile = null;

            if (definition != null && definition.AIProfile != null)
            {
                baseProfile = definition.AIProfile;
            }
            else
            {
                Debug.LogWarning($"Brawler '{definition?.BrawlerName}' has no AIProfile assigned. Using emergency fallback values.");

                baseProfile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
                if (definition != null)
                    baseProfile.ApplyArchetypeDefaults(definition.Archetype);
            }

            _baseProfileSource = baseProfile;
            BrawlerAIProfile runtimeProfile = Instantiate(baseProfile);
            runtimeProfile.name = $"{baseProfile.name}_Runtime_{_difficulty}_{_personality}";

            _activeTuningCatalog = AITuningCatalogProvider.Resolve(_tuningCatalog);
            AIProfileTuningUtility.ApplyRuntimeTuning(
                runtimeProfile,
                _difficulty,
                _personality,
                _activeTuningCatalog,
                _performanceTier);
            _runtimeTuningVersion = AITuningRuntimeOverrides.Version;
            _appliedPerformanceTier = _performanceTier;
            _appliedDifficulty = _difficulty;
            _appliedPersonality = _personality;
            _lastTuningDebug = BuildTuningSummary();

            return runtimeProfile;
        }

        private static AIObjectivePoint[] GetSceneObjectivePoints()
        {
            int sceneHandle = SceneManager.GetActiveScene().handle;
            if (_cachedObjectivePoints == null ||
                _cachedObjectiveSceneHandle != sceneHandle ||
                HasDestroyedObjectivePoint(_cachedObjectivePoints))
            {
                _cachedObjectivePoints = Object.FindObjectsOfType<AIObjectivePoint>();
                _cachedObjectiveSceneHandle = sceneHandle;
            }

            return _cachedObjectivePoints;
        }

        private static bool HasDestroyedObjectivePoint(AIObjectivePoint[] points)
        {
            if (points == null)
                return true;

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null)
                    return true;
            }

            return false;
        }

        private bool CanRunAI()
        {
            return _brainInitialized &&
                   _brawler != null &&
                   _brawler.State != null &&
                   !_brawler.State.IsDead &&
                   IsMatchActiveForAI() &&
                   SimulationClock.Grid != null;
        }

        private static bool IsMatchActiveForAI()
        {
            MatchManager matchManager = MatchManager.Instance;
            return matchManager == null || matchManager.CurrentState == MatchState.Active;
        }

        private void ResetTransientAIState()
        {
            _actionCommitment?.Reset();
            _humanization?.Reset();
            _idleHesitation?.Reset();
            _targetInfo?.Clear();
            _teamCoordinator?.ClearTargetFocusCount();
            _teamCoordinator?.ClearActionIntent();
            _teamCoordinator?.ClearLaneOwnership();
            _navAgent?.ClearDestinationForFallback();
            _failureRecovery?.Reset();
            _commandSource?.ClearQueuedCommands();
        }

        private void ScheduleNextSense(uint currentTick)
        {
            bool hot = _targetInfo.HasLiveTarget;
            uint baseInterval = hot ? _profile.CombatSenseIntervalTicks : _profile.IdleSenseIntervalTicks;
            uint rhythmJitter = _humanization != null
                ? _humanization.GetReactionJitterTicks(currentTick, hot)
                : 0u;
            _nextSenseTick = currentTick + baseInterval + _profile.ReactionDelayTicks + rhythmJitter;
        }

        private float GetHealthRatio()
        {
            if (_brawler == null || _brawler.State == null)
                return 1f;

            return _brawler.State.CurrentHealth / Mathf.Max(1f, _brawler.State.MaxHealth.Value);
        }

        private void RefreshDangerIfDue(uint currentTick)
        {
            if (_dangerMemory == null || _profile == null || currentTick < _nextDangerRefreshTick)
                return;

            if (!AIBudgetCoordinator.TryAcquireDangerRefresh(
                    currentTick,
                    _profile,
                    highPriority: _dangerMemory.HasDanger))
            {
                DeferDangerRefresh(currentTick);
                return;
            }

            _dangerMemory.Refresh(_brawler, _profile, currentTick);

            uint interval = _profile.DangerRefreshIntervalTicks == 0u
                ? 1u
                : _profile.DangerRefreshIntervalTicks;

            _nextDangerRefreshTick = currentTick + interval;
        }

        private float GetAbilityIdealRange()
        {
            var attack = _brawler.State != null
                ? _brawler.State.GetCurrentMainAttackDefinition()
                : _brawler.Definition?.MainAttack;
            return attack != null ? attack.GetAIIdealRange() : 6f;
        }

        private float GetAbilityMaxRange()
        {
            var attack = _brawler.State != null
                ? _brawler.State.GetCurrentMainAttackDefinition()
                : _brawler.Definition?.MainAttack;
            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private float GetSuperMaxRange()
        {
            var super = _brawler.State != null
                ? _brawler.State.GetCurrentSuperDefinition()
                : _brawler.Definition?.SuperAbility;
            return super != null ? super.GetAIMaxRange() : 6f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _teamCoordinator?.ClearTargetFocusCount();
            _teamCoordinator?.ClearActionIntent();
            _commandSource?.ClearQueuedCommands();
            _reactiveListener?.Dispose();
            _reactiveListener = null;
            _failureRecoveryListener?.Dispose();
            _failureRecoveryListener = null;

            if (_brawler != null)
            {
                AIDebugTracker.Unregister(_brawler);
            }
        }

        private void EnsureReactiveListener()
        {
            if (_reactiveListener != null ||
                _brawler == null ||
                _profile == null ||
                _targetInfo == null ||
                _reactiveMemory == null)
            {
                return;
            }

            _reactiveListener = new AIReactiveListener(
                _brawler,
                _profile,
                _targetInfo,
                _reactiveMemory);
        }

        private void EnsureFailureRecoveryListener()
        {
            if (_failureRecoveryListener != null ||
                _brawler == null ||
                _profile == null ||
                _failureRecovery == null)
            {
                return;
            }

            _failureRecoveryListener = new AIFailureRecoveryListener(
                _brawler,
                _profile,
                _failureRecovery);
        }

        private void RefreshRuntimeTuningIfNeeded(uint currentTick)
        {
            AITuningCatalog resolvedCatalog = AITuningCatalogProvider.Resolve(_tuningCatalog);
            if (_profile == null ||
                _baseProfileSource == null)
            {
                return;
            }

            bool tuningCurrent =
                _runtimeTuningVersion == AITuningRuntimeOverrides.Version &&
                _activeTuningCatalog == resolvedCatalog &&
                _appliedPerformanceTier == _performanceTier &&
                _appliedDifficulty == _difficulty &&
                _appliedPersonality == _personality;

            if (tuningCurrent)
            {
                return;
            }

            _activeTuningCatalog = resolvedCatalog;
            AIProfileTuningUtility.RebuildRuntimeTuning(
                _baseProfileSource,
                _profile,
                _difficulty,
                _personality,
                _activeTuningCatalog,
                _performanceTier);
            _runtimeTuningVersion = AITuningRuntimeOverrides.Version;
            _appliedPerformanceTier = _performanceTier;
            _appliedDifficulty = _difficulty;
            _appliedPersonality = _personality;
            _lastTuningDebug = BuildTuningSummary();

            _perception = new AIPerception(
                _profile.DetectionRadius,
                _profile.MemoryDurationTicks,
                _targetScorer,
                _profile.LogPerception);

            if (_profile.EnableDebugSnapshots)
                AIDebugTracker.Register(_brawler);
            else
                AIDebugTracker.Unregister(_brawler);

            _actionCommitment?.Reset();
            _humanization?.Reset();
            _idleHesitation?.Reset();
            _nextSenseTick = currentTick;
            _nextDangerRefreshTick = currentTick;
        }

        private void UpdateIdleHesitation(uint currentTick)
        {
            if (_idleHesitation == null ||
                _actionExecutor == null ||
                _navAgent == null ||
                _profile == null ||
                !_profile.EnableFailureRecovery)
            {
                _lastIdleHesitationDebug = "Idle=None";
                return;
            }

            bool hasRecentTargetMemory =
                _targetInfo != null &&
                _targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks);

            AIIdleHesitationDecision decision = _idleHesitation.Evaluate(
                new AIIdleHesitationContext(
                    currentTick,
                    _lastChosenAction,
                    _targetInfo != null && _targetInfo.HasLiveTarget,
                    hasRecentTargetMemory,
                    _navAgent.HasDestination &&
                    !_navAgent.IsActiveDestinationMovementSuppressed,
                    _dangerMemory != null && _dangerMemory.HasDanger,
                    _profile.IdleHesitationRecoveryTicks,
                    _profile.IdleHesitationCooldownTicks,
                    _profile.IdleHesitationLowScoreThreshold));

            _lastIdleHesitationDebug = decision.GetDebugSummary(
                _idleHesitation.NextRecoveryTick);

            if (!decision.ShouldRecover)
                return;

            AIIncidentLogger.Record(
                _brawler.EntityID,
                AIIncidentType.MovementStall,
                currentTick,
                decision.Reason);
            _actionExecutor.HandleIdleHesitation(_targetInfo, currentTick);
            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.FailureRecovery,
                currentTick);
            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.MovementStall,
                currentTick);
            AIReportCardTracker.RecordFailureRecovery(
                _brawler.EntityID,
                AIFailureRecoveryReason.IdleHesitation,
                currentTick);

            if (_profile.LogFailureRecovery)
            {
                Debug.Log(
                    $"[AIIdle-{_brawler.name}] " +
                    $"reason={decision.Reason} " +
                    $"elapsed={decision.ElapsedTicks}");
            }
        }

        private void UpdateFailureRecovery(uint currentTick)
        {
            if (_failureRecovery == null || _navAgent == null || _profile == null)
            {
                _lastFailureRecoveryDebug = "Recovery=None";
                return;
            }

            if (_navAgent.TryGetFailureSignal(_profile, currentTick, out AIFailureRecoverySignal signal) &&
                _failureRecovery.TryCreateNavigationRecovery(
                    signal,
                    _profile,
                    currentTick,
                    out AIFailureRecoveryRequest request))
            {
                AIIncidentLogger.Record(
                    _brawler.EntityID,
                    ToIncidentType(signal.Reason),
                    currentTick,
                    $"dist={signal.DistanceToDestination:0.0} age={signal.DestinationAgeTicks}");
                RecordGauntletSignal(signal.Reason, currentTick);
                _actionExecutor?.HandleFailureRecovery(request, currentTick);

                bool recovered = _navAgent.TryRequestRecoveryDestination(
                    request,
                    _profile,
                    out Vector3 recoveryDestination);
                if (!recovered)
                {
                    _navAgent.ClearDestinationForFallback();
                    _actionExecutor?.HandleNavigationRecoveryFallback(
                        _targetInfo,
                        currentTick,
                        request.Reason);
                }

                AIValidationGauntlet.RecordSignal(
                    AIValidationGauntletSignal.FailureRecovery,
                    currentTick);
                AIReportCardTracker.RecordFailureRecovery(
                    _brawler.EntityID,
                    request.Reason,
                    currentTick);

                if (_profile.LogFailureRecovery)
                {
                    Debug.Log(
                        $"[AIFailure-{_brawler.name}] " +
                        $"reason={request.Reason} " +
                        $"count={request.ConsecutiveCount} " +
                        $"dist={request.DistanceToDestination:0.0} " +
                        $"detour={FormatVector(recoveryDestination)} " +
                        $"applied={recovered}");
                }
            }

            _lastFailureRecoveryDebug = _failureRecovery.GetDebugSummary(currentTick);
        }

        private static AIIncidentType ToIncidentType(AIFailureRecoveryReason reason)
        {
            switch (reason)
            {
                case AIFailureRecoveryReason.NavigationStall:
                case AIFailureRecoveryReason.IdleHesitation:
                    return AIIncidentType.MovementStall;

                case AIFailureRecoveryReason.BlockedRoute:
                    return AIIncidentType.RouteBlocked;

                case AIFailureRecoveryReason.StaleDestination:
                    return AIIncidentType.StaleDestination;

                default:
                    return AIIncidentType.None;
            }
        }

        private static void RecordGauntletSignal(
            AIFailureRecoveryReason reason,
            uint currentTick)
        {
            switch (reason)
            {
                case AIFailureRecoveryReason.NavigationStall:
                case AIFailureRecoveryReason.IdleHesitation:
                    AIValidationGauntlet.RecordSignal(
                        AIValidationGauntletSignal.MovementStall,
                        currentTick);
                    break;

                case AIFailureRecoveryReason.BlockedRoute:
                    AIValidationGauntlet.RecordSignal(
                        AIValidationGauntletSignal.RouteBlocked,
                        currentTick);
                    break;
            }
        }

        private void UpdateProductionBudget(uint currentTick)
        {
            if (_profile == null)
            {
                _lastBudgetDebug = "Budget=NoProfile";
                return;
            }

            AIPerformanceSnapshot performanceSnapshot =
                AIPerformanceTracker.GetSnapshot(currentTick);
            bool overBudget = performanceSnapshot.IsOverBudget(
                _profile.MaxMapResolvesPerTick,
                _profile.MaxPathQueriesPerTick,
                _profile.MaxPathTouchedNodesPerTick);
            bool budgetPressure = AIBudgetCoordinator.HasPressure(currentTick);

            if (!overBudget && !budgetPressure)
            {
                _lastBudgetDebug = BuildBudgetSummary(currentTick);
                return;
            }

            if (!_profile.LogBudgetWarnings)
            {
                _lastBudgetDebug = BuildBudgetSummary(currentTick);
                return;
            }

            if (currentTick < _nextBudgetWarningTick)
            {
                _lastBudgetDebug = BuildBudgetSummary(currentTick);
                return;
            }

            _lastBudgetDebug = BuildBudgetSummary(currentTick);
            Debug.LogWarning($"[AIBudget-{_brawler.name}] {_lastBudgetDebug}");
            _nextBudgetWarningTick = currentTick + 30u;
        }

        private string BuildBudgetSummary(uint currentTick)
        {
            return
                AIPerformanceTracker.GetBudgetSummary(
                    currentTick,
                    _profile.MaxMapResolvesPerTick,
                    _profile.MaxPathQueriesPerTick,
                    _profile.MaxPathTouchedNodesPerTick) +
                " " +
                AIBudgetCoordinator.GetDebugSummary(currentTick);
        }

        private string BuildTuningSummary()
        {
            string catalog = _activeTuningCatalog != null
                ? _activeTuningCatalog.GetDebugSummary(_difficulty, _personality)
                : "Catalog=None";

            return
                $"{AIProfileTuningUtility.GetPerformanceTierDebugSummary(_performanceTier, _profile)} " +
                $"Difficulty={_difficulty} Personality={_personality} " +
                $"Tuning {catalog} " +
                $"{AITuningRuntimeOverrides.GetDebugSummary()}";
        }

        private void ApplyPerformanceTierSelection()
        {
            if (_performanceTier == AIBotPerformanceTier.Custom)
                return;

            ResolvePerformanceTier(
                _performanceTier,
                out AIDifficultyLevel tierDifficulty,
                out AIPersonalityType tierPersonality);

            _difficulty = tierDifficulty;
            _personality = tierPersonality;
        }

        private static void ResolvePerformanceTier(
            AIBotPerformanceTier tier,
            out AIDifficultyLevel difficulty,
            out AIPersonalityType personality)
        {
            switch (tier)
            {
                case AIBotPerformanceTier.Amateur:
                    difficulty = AIDifficultyLevel.Easy;
                    personality = AIPersonalityType.Cautious;
                    return;

                case AIBotPerformanceTier.Regular:
                    difficulty = AIDifficultyLevel.Normal;
                    personality = AIPersonalityType.Balanced;
                    return;

                case AIBotPerformanceTier.Veteran:
                    difficulty = AIDifficultyLevel.Hard;
                    personality = AIPersonalityType.TeamPlayer;
                    return;

                case AIBotPerformanceTier.Elite:
                    difficulty = AIDifficultyLevel.Hard;
                    personality = AIPersonalityType.Aggressive;
                    return;

                default:
                    difficulty = AIDifficultyLevel.Normal;
                    personality = AIPersonalityType.Balanced;
                    return;
            }
        }

        private void UpdateTacticalIntentSummary()
        {
            if (_brawler == null)
            {
                _lastTacticalIntentSummary = "Intent=None";
                return;
            }

            string role = _profile != null
                ? _profile.Archetype.ToString()
                : _brawler.Definition != null ? _brawler.Definition.Archetype.ToString() : "Unknown";
            string tier = _performanceTier.ToString();
            string phrase = ResolveIntentPhrase();

            _lastTacticalIntentSummary =
                $"Intent={phrase} Role={role} Tier={tier}";
        }

        private string ResolveIntentPhrase()
        {
            if (_brawler != null &&
                _brawler.State != null &&
                _brawler.State.HasStatus(StatusEffectType.Stun))
            {
                return "stunned_waiting";
            }

            switch (_lastChosenAction.ActionType)
            {
                case AIActionType.Evade:
                    return "dodging_immediate_danger";

                case AIActionType.Retreat:
                    return "surviving_and_resetting";

                case AIActionType.Peel:
                    return "protecting_threatened_ally";

                case AIActionType.Regroup:
                    return "regrouping_with_team";

                case AIActionType.UseSuper:
                    return "looking_for_super_value";

                case AIActionType.Objective:
                    return ResolveObjectiveIntentPhrase();

                case AIActionType.Approach:
                    return ResolveApproachIntentPhrase();

                case AIActionType.HoldRange:
                    return ResolveHoldIntentPhrase();

                case AIActionType.Reposition:
                    return ResolveRepositionIntentPhrase();

                case AIActionType.Search:
                    return ResolveSearchIntentPhrase();

                case AIActionType.Wander:
                    return "fallback_patrol";

                default:
                    return "evaluating";
            }
        }

        private string ResolveObjectiveIntentPhrase()
        {
            if (LastObjectiveType == AIObjectiveType.GemMine ||
                TextContains(LastObjectiveName, "gem"))
            {
                return "controlling_gem_area";
            }

            if (LastObjectiveType == AIObjectiveType.LanePressure)
                return "holding_or_breaking_lane";

            if (LastObjectiveType == AIObjectiveType.HotZone)
                return "anchoring_control_zone";

            if (LastObjectiveType == AIObjectiveType.MidControl)
                return "taking_mid_control";

            return "playing_objective";
        }

        private string ResolveApproachIntentPhrase()
        {
            if (TryGetLiveBrawlerTarget(out BrawlerController target))
            {
                float healthRatio = target.State.CurrentHealth /
                                    Mathf.Max(1f, target.State.MaxHealth.Value);

                if (healthRatio <= 0.35f)
                    return "collapsing_on_low_health_target";

                if (CurrentTargetAllyFocusCount > 0)
                    return "joining_focus_fire";

                if (target.State.CarriedGemCount > 0)
                    return "pressuring_gem_carrier";
            }

            return "closing_pressure";
        }

        private string ResolveHoldIntentPhrase()
        {
            if (TextContains(PlaybookDebug, "Lane") ||
                TextContains(TeamRoleDebug, "Lane"))
            {
                return "holding_lane_angle";
            }

            if (TryGetLiveBrawlerTarget(out _))
                return "maintaining_best_range";

            return "holding_safe_position";
        }

        private string ResolveRepositionIntentPhrase()
        {
            if (TextContains(LastMapRouteDebug, "cover") ||
                TextContains(LastTacticalMoveReason, "cover"))
            {
                return "rotating_to_cover";
            }

            if (TextContains(LastMapRouteDebug, "lane"))
                return "rotating_to_lane_angle";

            return "finding_better_angle";
        }

        private string ResolveSearchIntentPhrase()
        {
            if (TextContains(GemPickupDebug, "gem") &&
                !TextContains(GemPickupDebug, "none"))
            {
                return "rotating_to_loose_gems";
            }

            if (TextContains(MacroDebug, "push"))
                return "rotating_for_push";

            if (TextContains(MacroDebug, "reset"))
                return "resetting_map_position";

            return "scouting_next_pressure";
        }

        private bool TryGetLiveBrawlerTarget(out BrawlerController target)
        {
            target = null;

            if (_targetInfo == null ||
                !_targetInfo.HasLiveTarget ||
                _targetInfo.Target == null ||
                !SpatialEntityUtility.IsAlive(_targetInfo.Target) ||
                !(_targetInfo.Target is BrawlerController brawlerTarget) ||
                brawlerTarget.State == null)
            {
                return false;
            }

            target = brawlerTarget;
            return true;
        }

        private static bool TextContains(string text, string value)
        {
            return !string.IsNullOrEmpty(text) &&
                   !string.IsNullOrEmpty(value) &&
                   text.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildNavigationDebug()
        {
            if (_navAgent == null)
                return "Nav=None";

            if (!_navAgent.HasDestination)
            {
                return
                    $"Nav=Idle " +
                    $"Move={FormatVector(_navAgent.LastQueuedMoveDirection)} " +
                    $"Zero={_navAgent.ConsecutiveActiveZeroMoveTicks}";
            }

            float distance = _brawler != null
                ? PlanarDistance(_brawler.Position, _navAgent.Destination)
                : 0f;
            string state = _navAgent.IsRouteBlocked
                ? "Blocked"
                : _navAgent.DebugHasPath ? "Path" : "Direct";
            string waypoint = _navAgent.DebugHasSteeringTarget
                ? FormatVector(_navAgent.DebugSteeringTarget)
                : "None";

            return
                $"Nav={state} " +
                $"Dest={FormatVector(_navAgent.Destination)} " +
                $"Waypoint={waypoint} " +
                $"Dist={distance:0.0} " +
                $"Arrival={_navAgent.DebugArrivalDistance:0.0} " +
                $"Path={_navAgent.DebugPathIndex}/{_navAgent.DebugPathNodeCount} " +
                $"Age={_navAgent.DebugDestinationAgeTicks} " +
                $"High={_navAgent.DebugDestinationHighPriority} " +
                $"Blocked={_navAgent.IsRouteBlocked} " +
                $"Stuck={_navAgent.ConsecutiveStuckSamples} " +
                $"RouteFail={_navAgent.ConsecutiveRouteFailures} " +
                $"Defers={_navAgent.ConsecutivePathBudgetDeferrals} " +
                $"Move={FormatVector(_navAgent.LastQueuedMoveDirection)}";
        }

#if UNITY_EDITOR
        private static readonly Color SceneObjectiveColor = new Color(0.62f, 0.24f, 1f, 0.92f);
        private static readonly Color SceneDestinationColor = new Color(0.00f, 0.96f, 1f, 0.98f);
        private static readonly Color SceneWaypointColor = new Color(0.12f, 1f, 0.46f, 0.98f);
        private static readonly Color SceneBlockedColor = new Color(1f, 0.05f, 0.14f, 0.98f);
        private static readonly Color SceneHighPriorityColor = new Color(1f, 0.12f, 0.92f, 0.98f);
        private static readonly Color ScenePathColor = new Color(0.18f, 0.48f, 1f, 0.82f);
        private static readonly Color SceneFacingColor = new Color(0.88f, 0.88f, 1f, 0.78f);
        private static readonly Color ScenePreferredRangeColor = new Color(0.00f, 0.86f, 1f, 0.48f);
        private static readonly Color SceneTooCloseRangeColor = new Color(1f, 0.04f, 0.24f, 0.42f);
        private static readonly Color SceneMoveColor = new Color(0.22f, 1f, 0.88f, 0.98f);
        private static readonly Color SceneTargetColor = new Color(1f, 0.08f, 0.72f, 0.92f);
        private static readonly Color SceneLabelMutedColor = new Color(0.78f, 0.94f, 1f, 0.96f);
        private const int SceneDebugMaxPathNodes = 28;
        private static GUIStyle _sceneDebugLabelStyle;
        private static readonly System.Collections.Generic.List<Vector3> SceneDebugPathNodes =
            new System.Collections.Generic.List<Vector3>(SceneDebugMaxPathNodes);

        private void UpdateInspectorRuntimeDebug(uint currentTick, string forcedState = null)
        {
            if (!_mirrorRuntimeDebugToInspector)
                return;

            _inspectorDebugTick = currentTick;
            _inspectorDebugBrawler = _brawler != null && _brawler.Definition != null
                ? _brawler.Definition.BrawlerName
                : _brawler != null ? _brawler.name : name;
            _inspectorTacticalIntentSummary = TacticalIntentSummary;
            _inspectorCurrentAction = _lastChosenAction.ActionType;
            _inspectorCurrentActionScore = _lastChosenAction.Score;
            _inspectorDecisionPerformanceScore = EvaluateDecisionPerformance(
                out _inspectorDecisionPerformanceRank,
                out _inspectorDecisionPerformanceBreakdown);
            _inspectorTacticalIntent = LastTacticalMovementIntent;
            _inspectorTarget = ResolveTargetDebugName();
            _inspectorRouteState = string.IsNullOrWhiteSpace(forcedState)
                ? ResolveNavigationSceneDebugState()
                : forcedState;

            if (_navAgent != null && _navAgent.HasDestination)
            {
                _inspectorDestination = _navAgent.Destination;
                _inspectorWaypoint = _navAgent.DebugHasSteeringTarget
                    ? _navAgent.DebugSteeringTarget
                    : _navAgent.Destination;
                _inspectorDistanceToDestination = _brawler != null
                    ? PlanarDistance(_brawler.Position, _navAgent.Destination)
                    : 0f;
                _inspectorPathIndex = _navAgent.DebugPathIndex;
                _inspectorPathNodeCount = _navAgent.DebugPathNodeCount;
                _inspectorRouteBlocked = _navAgent.IsRouteBlocked;
                _inspectorStuckSamples = _navAgent.ConsecutiveStuckSamples;
                _inspectorPathBudgetDeferrals = _navAgent.ConsecutivePathBudgetDeferrals;
            }
            else
            {
                _inspectorDestination = Vector3.zero;
                _inspectorWaypoint = Vector3.zero;
                _inspectorDistanceToDestination = 0f;
                _inspectorPathIndex = 0;
                _inspectorPathNodeCount = 0;
                _inspectorRouteBlocked = false;
                _inspectorStuckSamples = 0;
                _inspectorPathBudgetDeferrals = 0;
            }

            _inspectorMoveDirection = _navAgent != null
                ? _navAgent.LastQueuedMoveDirection
                : Vector3.zero;
            _inspectorDecisionDetails = BuildInspectorDecisionDetails(forcedState);
            _inspectorPerformanceTierDetails =
                AIProfileTuningUtility.GetPerformanceTierDebugSummary(_performanceTier, _profile);
            _inspectorMovementDetails = BuildInspectorMovementDetails();
            _inspectorObjectiveDetails = BuildInspectorObjectiveDetails();
            _inspectorTeamDetails = BuildInspectorTeamDetails();
            _inspectorRecoveryAndPerfDetails = BuildInspectorRecoveryAndPerfDetails(currentTick);
        }

        private string BuildInspectorDecisionDetails(string forcedState)
        {
            return
                $"State={(!string.IsNullOrWhiteSpace(forcedState) ? forcedState : "Running")}\n" +
                $"{TacticalIntentSummary}\n" +
                $"Action={_lastChosenAction.ActionType} score={_lastChosenAction.Score:0.00}\n" +
                $"DecisionRank={_inspectorDecisionPerformanceRank} score={_inspectorDecisionPerformanceScore:0.0}\n" +
                $"Scores={BuildTopActionScoreSummary(5)}\n" +
                $"{DecisionConfidenceDebug}\n" +
                $"Tier={_performanceTier} Difficulty={Difficulty} Personality={Personality} Human={HumanizationDebug}\n" +
                $"Tuning={TuningDebug}";
        }

        private float EvaluateDecisionPerformance(out string rank, out string breakdown)
        {
            float score = 50f;
            string details = "Base=50";

            if (_lastChosenAction.ActionType == AIActionType.None)
            {
                score -= 18f;
                details += " NoAction=-18";
            }
            else if (_lastChosenAction.Score > 0.01f)
            {
                score += 8f;
                details += " ValidAction=+8";
            }
            else
            {
                score -= 8f;
                details += " WeakAction=-8";
            }

            if (TryGetTopTwoActionScores(out float bestScore, out float secondScore))
            {
                float margin = bestScore - secondScore;
                float confidenceBonus = Mathf.Clamp(margin * 0.4f, -10f, 18f);
                score += confidenceBonus;
                details += $" Confidence={confidenceBonus:+0.0;-0.0;0.0}";
            }

            float profileDelta = EvaluateProfileSkillSignal(out string profileSkillDetails);
            score += profileDelta;
            details += profileSkillDetails;

            if (_navAgent != null)
            {
                if (_navAgent.IsRouteBlocked)
                {
                    score -= 22f;
                    details += " Blocked=-22";
                }
                else if (_navAgent.HasDestination && _navAgent.ConsecutiveStuckSamples == 0)
                {
                    score += 7f;
                    details += " RouteHealthy=+7";
                }

                if (_navAgent.ConsecutiveStuckSamples > 0)
                {
                    float stuckPenalty = Mathf.Min(20f, _navAgent.ConsecutiveStuckSamples * 4f);
                    score -= stuckPenalty;
                    details += $" Stuck=-{stuckPenalty:0.0}";
                }

                if (_navAgent.ConsecutivePathBudgetDeferrals > 0)
                {
                    float deferralPenalty = Mathf.Min(12f, _navAgent.ConsecutivePathBudgetDeferrals * 3f);
                    score -= deferralPenalty;
                    details += $" PathBudget=-{deferralPenalty:0.0}";
                }

                if (RequiresDestination(_lastChosenAction.ActionType) && !_navAgent.HasDestination)
                {
                    score -= 8f;
                    details += " NoDestination=-8";
                }
            }

            if (RequiresTarget(_lastChosenAction.ActionType))
            {
                if (_targetInfo != null && _targetInfo.HasLiveTarget && _targetInfo.Target != null)
                {
                    score += 8f;
                    details += " TargetLive=+8";
                }
                else
                {
                    score -= 10f;
                    details += " MissingTarget=-10";
                }

                if (CurrentTargetOverFocusPenalty > 0.01f)
                {
                    float focusPenalty = Mathf.Min(10f, CurrentTargetOverFocusPenalty * 0.35f);
                    score -= focusPenalty;
                    details += $" OverFocus=-{focusPenalty:0.0}";
                }
            }

            if (_lastChosenAction.ActionType == AIActionType.Objective)
            {
                if (HasObjectiveDebug)
                {
                    score += 8f;
                    details += " ObjectiveValid=+8";
                }
                else
                {
                    score -= 8f;
                    details += " ObjectiveMissing=-8";
                }

                if (LastObjectiveCrowdingPenalty > 0.01f)
                {
                    float crowdPenalty = Mathf.Min(10f, LastObjectiveCrowdingPenalty * 0.2f);
                    score -= crowdPenalty;
                    details += $" Crowded=-{crowdPenalty:0.0}";
                }
            }

            float healthRatio = GetHealthRatio();
            if (healthRatio < 0.35f)
            {
                if (IsSurvivalAction(_lastChosenAction.ActionType))
                {
                    score += 8f;
                    details += " LowHpSafe=+8";
                }
                else
                {
                    score -= 8f;
                    details += " LowHpRisk=-8";
                }
            }

            score = Mathf.Clamp(score, 0f, 100f);
            rank = ResolveDecisionPerformanceRank(score);
            breakdown = $"{rank} ({score:0.0}/100)\n{details}";
            return score;
        }

        private float EvaluateProfileSkillSignal(out string details)
        {
            details = string.Empty;
            if (_profile == null)
                return 0f;

            float reactionQuality = Mathf.InverseLerp(18f, 0f, _profile.ReactionDelayTicks + _profile.HumanizationReactionJitterTicks);
            float aimQuality = Mathf.InverseLerp(12f, 1f, _profile.AimErrorDegrees);
            float senseQuality = Mathf.InverseLerp(8f, 2f, _profile.CombatSenseIntervalTicks);
            float teamQuality = Mathf.InverseLerp(0.65f, 1.45f, _profile.TeamRoleCoordinationWeight);
            float objectiveQuality = Mathf.InverseLerp(0.70f, 1.45f, _profile.ObjectiveWeight * _profile.MacroActionBiasWeight);
            float mistakeLoad = Mathf.Clamp01(_profile.HumanizationPressureMistakeChance / 0.14f);

            float delta =
                Mathf.Lerp(-8f, 7f, reactionQuality) +
                Mathf.Lerp(-7f, 6f, aimQuality) +
                Mathf.Lerp(-5f, 5f, senseQuality) +
                Mathf.Lerp(-4f, 5f, teamQuality) +
                Mathf.Lerp(-4f, 5f, objectiveQuality) -
                Mathf.Lerp(0f, 6f, mistakeLoad);

            delta = Mathf.Clamp(delta, -18f, 18f);
            details =
                $" ProfileSkill={delta:+0.0;-0.0;0.0}" +
                $"(react={reactionQuality:0.0} aim={aimQuality:0.0} sense={senseQuality:0.0} team={teamQuality:0.0} obj={objectiveQuality:0.0})";
            return delta;
        }

        private bool TryGetTopTwoActionScores(out float bestScore, out float secondScore)
        {
            bestScore = float.NegativeInfinity;
            secondScore = float.NegativeInfinity;

            if (_debugScores == null || _debugScores.Count == 0)
                return false;

            for (int i = 0; i < _debugScores.Count; i++)
            {
                float score = _debugScores[i].Score;
                if (score > bestScore)
                {
                    secondScore = bestScore;
                    bestScore = score;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                }
            }

            if (float.IsNegativeInfinity(secondScore))
                secondScore = 0f;

            return !float.IsNegativeInfinity(bestScore);
        }

        private static bool RequiresDestination(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                case AIActionType.Retreat:
                case AIActionType.Evade:
                case AIActionType.Regroup:
                case AIActionType.Peel:
                case AIActionType.Objective:
                case AIActionType.Search:
                case AIActionType.Wander:
                    return true;

                default:
                    return false;
            }
        }

        private static bool RequiresTarget(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                case AIActionType.Retreat:
                case AIActionType.Evade:
                case AIActionType.UseSuper:
                case AIActionType.Peel:
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsSurvivalAction(AIActionType actionType)
        {
            return actionType == AIActionType.Retreat ||
                   actionType == AIActionType.Evade ||
                   actionType == AIActionType.Regroup ||
                   actionType == AIActionType.Peel;
        }

        private static string ResolveDecisionPerformanceRank(float score)
        {
            if (score >= 90f)
                return "S - Excellent";

            if (score >= 78f)
                return "A - Strong";

            if (score >= 64f)
                return "B - Good";

            if (score >= 50f)
                return "C - Watch";

            if (score >= 35f)
                return "D - Weak";

            return "F - Critical";
        }

        private string BuildInspectorMovementDetails()
        {
            return
                $"{BuildNavigationDebug()}\n" +
                $"Tactical={LastTacticalMovementIntent} Reason={LastTacticalMoveReason}\n" +
                $"Range target={LastTacticalTargetDistance:0.0} preferred={LastTacticalPreferredRange:0.0} tooClose={LastTacticalTooCloseDistance:0.0}\n" +
                $"Retarget={LastTacticalRetargetTick}->{NextTacticalMoveRetargetTick} Stop={LastTacticalStopDebug}\n" +
                $"Map={LastMapRouteDebug}";
        }

        private string BuildInspectorObjectiveDetails()
        {
            return
                $"Objective={LastObjectiveType} {LastObjectiveName} runtime={LastObjectiveIsRuntime}\n" +
                $"Center={FormatVector(LastObjectiveCenter)} Slot={FormatVector(LastObjectiveSlot)} Dest={FormatVector(LastObjectiveDestination)}\n" +
                $"Radius={LastObjectiveRadius:0.0} Control={LastObjectiveControlState} Presence={LastObjectiveFriendlyPresence}:{LastObjectiveEnemyPresence}\n" +
                $"Role={LastObjectiveSlotRole}->{LastObjectiveDesiredSlotRole} Pressure={LastObjectiveAllyPressure:0.00} Penalty={LastObjectiveCrowdingPenalty:0.0}\n" +
                $"Score raw={LastObjectiveRawScore:0.0} final={LastObjectiveFinalScore:0.0} Reason={LastObjectiveScoreReason}";
        }

        private string BuildInspectorTeamDetails()
        {
            return
                $"{TacticalIntentSummary}\n" +
                $"Target={ResolveTargetDebugName()} Focus={CurrentTargetFocusCount} AllyFocus={CurrentTargetAllyFocusCount} OverFocusPenalty={CurrentTargetOverFocusPenalty:0.0}\n" +
                $"{TargetContextDebug}\n" +
                $"{TeamRoleDebug}\n" +
                $"{MacroDebug}\n" +
                $"{PlaybookDebug}\n" +
                $"{ChaseDebug}\n" +
                $"{ResourceAwarenessDebug}\n" +
                $"{TeamFightDebug}\n" +
                $"{RoleMacroDebug}\n" +
                $"{RoleMatchupDebug}\n" +
                $"{BrawlerIdentityDebug}\n" +
                $"{CoverPeekDebug}\n" +
                $"{ModeClutchDebug}\n" +
                $"{SpacingDebug}\n" +
                $"{AbilityThreatDebug}\n" +
                $"{EngagementRiskDebug}\n" +
                $"{PressureRotationDebug}\n" +
                $"{GemPickupDebug}";
        }

        private string BuildInspectorRecoveryAndPerfDetails(uint currentTick)
        {
            string budget = _profile != null
                ? BuildBudgetSummary(currentTick)
                : "Budget=NoProfile";

            return
                $"{FailureRecoveryDebug}\n" +
                $"{ReactiveDebug}\n" +
                $"{DangerDebug}\n" +
                $"{budget}\n" +
                $"Incident={(_brawler != null ? AIIncidentLogger.GetDebugSummary(_brawler.EntityID) : "Incident=None")}";
        }

        private string BuildTopActionScoreSummary(int maxCount)
        {
            if (_debugScores == null || _debugScores.Count == 0 || maxCount <= 0)
                return "None";

            string summary = string.Empty;
            for (int rank = 0; rank < maxCount; rank++)
            {
                int bestIndex = -1;
                float bestScore = float.NegativeInfinity;
                for (int i = 0; i < _debugScores.Count; i++)
                {
                    if (IsScoreIndexAlreadyUsed(summary, _debugScores[i].ActionType))
                        continue;

                    if (_debugScores[i].Score <= bestScore)
                        continue;

                    bestScore = _debugScores[i].Score;
                    bestIndex = i;
                }

                if (bestIndex < 0)
                    break;

                if (!string.IsNullOrEmpty(summary))
                    summary += " | ";

                AIActionScore score = _debugScores[bestIndex];
                summary += $"{score.ActionType}:{score.Score:0.0}";
            }

            return string.IsNullOrEmpty(summary) ? "None" : summary;
        }

        private static bool IsScoreIndexAlreadyUsed(string summary, AIActionType actionType)
        {
            return !string.IsNullOrEmpty(summary) &&
                   summary.Contains(actionType.ToString());
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying ||
                !_drawSceneDebug ||
                !_drawSceneDebugWhenNotSelected ||
                Selection.Contains(gameObject))
            {
                return;
            }

            DrawAISceneDebug(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || !_drawSceneDebug)
                return;

            DrawAISceneDebug(true);
        }

        private void DrawAISceneDebug(bool selected)
        {
            Vector3 origin = ResolveDebugOrigin();
            Color actionColor = ResolveActionColor(_lastChosenAction.ActionType);
            DrawActionHalo(origin, selected, actionColor);
            DrawRangeSceneDebug(origin, selected);
            DrawFacingRaySceneDebug(origin, selected, actionColor);
            DrawObjectiveSceneDebug(selected);
            DrawNavigationSceneDebug(origin, selected);

            if (_drawSceneDebugTargetLinks)
                DrawTargetSceneDebug(origin, selected);

            if (_drawSceneDebugMoveVector)
                DrawMoveVectorSceneDebug(origin, selected);

            if (_drawSceneDebugLabels)
                DrawSceneDebugLabel(origin, selected);
        }

        private void DrawObjectiveSceneDebug(bool selected)
        {
            if (HasObjectiveDebug)
            {
                Gizmos.color = SceneObjectiveColor;
                Gizmos.DrawWireSphere(
                    LastObjectiveCenter,
                    Mathf.Max(0.35f, LastObjectiveRadius));
                Gizmos.DrawWireSphere(LastObjectiveSlot + Vector3.up * 0.05f, selected ? 0.48f : 0.34f);
                DrawBoldLine(LastObjectiveCenter, LastObjectiveSlot, SceneObjectiveColor, selected ? 3.2f : 2f);

                if (selected)
                {
                    Gizmos.DrawWireCube(
                        LastObjectiveDestination + Vector3.up * 0.06f,
                        new Vector3(0.55f, 0.12f, 0.55f));
                }
            }
        }

        private void DrawNavigationSceneDebug(Vector3 origin, bool selected)
        {
            if (_navAgent == null || !_navAgent.HasDestination)
                return;

            Color destinationColor = _navAgent.IsRouteBlocked
                ? SceneBlockedColor
                : _navAgent.DebugDestinationHighPriority
                    ? SceneHighPriorityColor
                    : SceneDestinationColor;
            Vector3 destination = _navAgent.Destination;
            Vector3 waypoint = _navAgent.DebugHasSteeringTarget
                ? _navAgent.DebugSteeringTarget
                : destination;

            DrawPathBreadcrumbs(selected, destinationColor);

            Color waypointColor = _navAgent.DebugHasSteeringTarget ? SceneWaypointColor : destinationColor;
            Gizmos.color = waypointColor;
            DrawBoldLine(
                origin + Vector3.up * 0.12f,
                waypoint + Vector3.up * 0.12f,
                waypointColor,
                selected ? 3.4f : 2.2f);
            Gizmos.DrawWireSphere(waypoint + Vector3.up * 0.08f, selected ? 0.32f : 0.24f);

            if (PlanarDistance(waypoint, destination) > 0.2f)
            {
                Gizmos.color = destinationColor;
                DrawBoldLine(
                    waypoint + Vector3.up * 0.10f,
                    destination + Vector3.up * 0.10f,
                    destinationColor,
                    selected ? 2.8f : 1.8f);
            }

            Gizmos.color = destinationColor;
            Gizmos.DrawWireCube(
                destination + Vector3.up * 0.08f,
                new Vector3(selected ? 0.55f : 0.42f, 0.14f, selected ? 0.55f : 0.42f));

            if (LastTacticalMovementIntent != AITacticalMovementIntent.None &&
                PlanarDistance(origin, LastTacticalMoveDestination) > 0.2f)
            {
                Color tacticalDestinationColor = WithAlpha(SceneDestinationColor, selected ? 0.68f : 0.46f);
                Gizmos.color = tacticalDestinationColor;
                DrawBoldLine(
                    origin + Vector3.up * 0.20f,
                    LastTacticalMoveDestination + Vector3.up * 0.20f,
                    tacticalDestinationColor,
                    selected ? 2.6f : 1.6f);
                Gizmos.DrawWireSphere(LastTacticalMoveDestination + Vector3.up * 0.12f, selected ? 0.24f : 0.18f);
            }
        }

        private void DrawTargetSceneDebug(Vector3 origin, bool selected)
        {
            if (_targetInfo == null ||
                !_targetInfo.HasLiveTarget ||
                _targetInfo.Target == null)
            {
                return;
            }

            Vector3 targetPosition = _targetInfo.Target.Position;
            Color targetColor = WithAlpha(SceneTargetColor, selected ? 0.92f : 0.54f);
            Gizmos.color = targetColor;
            DrawBoldLine(
                origin + Vector3.up * 0.35f,
                targetPosition + Vector3.up * 0.35f,
                targetColor,
                selected ? 3f : 1.8f);
            if (selected)
                Gizmos.DrawWireSphere(targetPosition + Vector3.up * 0.10f, 0.42f);
        }

        private void DrawActionHalo(Vector3 origin, bool selected, Color actionColor)
        {
            Handles.color = WithAlpha(actionColor, selected ? 0.92f : 0.52f);
            Handles.DrawWireDisc(origin + Vector3.up * 0.04f, Vector3.up, selected ? 0.78f : 0.58f);
            if (selected)
            {
                Handles.color = WithAlpha(actionColor, 0.32f);
                Handles.DrawWireDisc(origin + Vector3.up * 0.045f, Vector3.up, 1.02f);
            }
        }

        private void DrawRangeSceneDebug(Vector3 origin, bool selected)
        {
            if (!selected)
                return;

            if (LastTacticalPreferredRange > 0.05f)
            {
                Handles.color = ScenePreferredRangeColor;
                Handles.DrawWireDisc(origin + Vector3.up * 0.035f, Vector3.up, LastTacticalPreferredRange);
            }

            if (LastTacticalTooCloseDistance > 0.05f)
            {
                Handles.color = SceneTooCloseRangeColor;
                Handles.DrawWireDisc(origin + Vector3.up * 0.04f, Vector3.up, LastTacticalTooCloseDistance);
            }
        }

        private void DrawFacingRaySceneDebug(Vector3 origin, bool selected, Color actionColor)
        {
            Vector3 forward = _brawler != null ? _brawler.transform.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                return;

            DrawArrow(
                origin + Vector3.up * 0.42f,
                forward.normalized * (selected ? 1.65f : 1.05f),
                WithAlpha(SceneFacingColor, selected ? 0.92f : 0.58f));

            if (selected && LastTacticalPreferredRange > 0.05f)
            {
                Handles.color = WithAlpha(actionColor, 0.42f);
                Handles.DrawDottedLine(
                    origin + Vector3.up * 0.16f,
                    origin + forward.normalized * LastTacticalPreferredRange + Vector3.up * 0.16f,
                    4f);
            }
        }

        private void DrawPathBreadcrumbs(bool selected, Color destinationColor)
        {
            if (_navAgent == null ||
                !_navAgent.DebugHasPath ||
                _navAgent.CopyDebugPathNodesNonAlloc(SceneDebugPathNodes, SceneDebugMaxPathNodes) <= 0)
            {
                return;
            }

            Color pathColor = _navAgent.IsRouteBlocked
                ? WithAlpha(SceneBlockedColor, selected ? 0.78f : 0.45f)
                : WithAlpha(ScenePathColor, selected ? 0.86f : 0.48f);
            Gizmos.color = pathColor;

            Vector3 previous = SceneDebugPathNodes[0] + Vector3.up * 0.07f;
            for (int i = 0; i < SceneDebugPathNodes.Count; i++)
            {
                Vector3 current = SceneDebugPathNodes[i] + Vector3.up * 0.07f;
                if (i > 0)
                    DrawBoldLine(previous, current, pathColor, selected ? 2.5f : 1.5f);

                float radius = i == 0
                    ? selected ? 0.18f : 0.12f
                    : selected ? 0.12f : 0.08f;
                Gizmos.DrawWireSphere(current, radius);
                previous = current;
            }

            if (selected && SceneDebugPathNodes.Count >= SceneDebugMaxPathNodes)
            {
                Handles.color = WithAlpha(destinationColor, 0.62f);
                Handles.Label(
                    SceneDebugPathNodes[SceneDebugPathNodes.Count - 1] + Vector3.up * 0.35f,
                    "+",
                    ResolveSceneDebugLabelStyle());
            }
        }

        private void DrawMoveVectorSceneDebug(Vector3 origin, bool selected)
        {
            if (_navAgent == null)
                return;

            Vector3 move = _navAgent.LastQueuedMoveDirection;
            move.y = 0f;
            if (move.sqrMagnitude <= 0.0001f)
                return;

            float length = selected ? 1.35f : 0.92f;
            DrawArrow(
                origin + Vector3.up * 0.26f,
                move.normalized * length,
                SceneMoveColor);
        }

        private void DrawSceneDebugLabel(Vector3 origin, bool selected)
        {
            GUIStyle style = ResolveSceneDebugLabelStyle();
            style.normal.textColor = _navAgent != null && _navAgent.IsRouteBlocked
                ? SceneBlockedColor
                : selected ? Color.white : SceneLabelMutedColor;

            Handles.Label(
                origin + Vector3.up * Mathf.Max(0.6f, _sceneDebugLabelHeight),
                BuildSceneDebugLabel(selected),
                style);
        }

        private string BuildSceneDebugLabel(bool selected)
        {
            string brawlerName = _brawler != null && _brawler.Definition != null
                ? _brawler.Definition.BrawlerName
                : _brawler != null ? _brawler.name : name;
            string targetName = ResolveTargetDebugName();
            string navState = ResolveNavigationSceneDebugState();
            string decisionBreakdown;
            float decisionPerformance = EvaluateDecisionPerformance(
                out string decisionRank,
                out decisionBreakdown);

            if (!selected)
            {
                return
                    $"{SceneRich(brawlerName, Color.white)} | {SceneRich(_lastChosenAction.ActionType.ToString(), ResolveActionColor(_lastChosenAction.ActionType))} " +
                    $"{SceneRich(_lastChosenAction.Score.ToString("0.0"), SceneLabelMutedColor)}\n" +
                    $"{SceneRich("Intent", SceneObjectiveColor)} {TacticalIntentSummary}\n" +
                    $"{SceneRich("Rank", ResolveDecisionPerformanceColor(decisionPerformance))} {decisionRank} {decisionPerformance:0}\n" +
                    $"{SceneRich("Conf", SceneLabelMutedColor)} {DecisionConfidenceDebug}\n" +
                    $"{SceneRich("Move", SceneMoveColor)} {LastTacticalMovementIntent} | {SceneRich(navState, ResolveNavigationColor())}\n" +
                    $"{SceneRich("Target", SceneTargetColor)} {targetName}";
            }

            return
                $"{SceneRich(brawlerName, Color.white)} [{SceneRich(_brawler != null ? _brawler.Team.ToString() : "?", SceneLabelMutedColor)}]\n" +
                $"{SceneRich("Tier", SceneLabelMutedColor)} {_performanceTier} {Difficulty}/{Personality}\n" +
                $"{SceneRich("Intent", SceneObjectiveColor)} {TacticalIntentSummary}\n" +
                $"{SceneRich("Perf", SceneLabelMutedColor)} {BuildScenePerformanceSummary()}\n" +
                $"{SceneRich("Action", ResolveActionColor(_lastChosenAction.ActionType))} {_lastChosenAction.ActionType} score={_lastChosenAction.Score:0.0}\n" +
                $"{SceneRich("Rank", ResolveDecisionPerformanceColor(decisionPerformance))} {decisionRank} {decisionPerformance:0}/100\n" +
                $"{SceneRich("Top", SceneObjectiveColor)} {BuildTopActionScoreSummary(4)}\n" +
                $"{SceneRich("Conf", SceneLabelMutedColor)} {DecisionConfidenceDebug}\n" +
                $"{SceneRich("Match", SceneTargetColor)} {RoleMatchupDebug}\n" +
                $"{SceneRich("Identity", SceneObjectiveColor)} {BrawlerIdentityDebug}\n" +
                $"{SceneRich("Cover", SceneMoveColor)} {CoverPeekDebug}\n" +
                $"{SceneRich("Clutch", SceneObjectiveColor)} {ModeClutchDebug}\n" +
                $"{SceneRich("Space", SceneMoveColor)} {SpacingDebug}\n" +
                $"{SceneRich("Threat", SceneBlockedColor)} {AbilityThreatDebug}\n" +
                $"{SceneRich("Target", SceneTargetColor)} {targetName} focus={CurrentTargetFocusCount}/{CurrentTargetAllyFocusCount} penalty={CurrentTargetOverFocusPenalty:0.0}\n" +
                $"{SceneRich("Move", SceneMoveColor)} {LastTacticalMovementIntent} reason={LastTacticalMoveReason}\n" +
                $"{SceneRich(navState, ResolveNavigationColor())} | {LastTacticalStopDebug}\n" +
                $"{SceneRich("Obj", SceneObjectiveColor)} {LastObjectiveType} {LastObjectiveName} role={LastObjectiveSlotRole}->{LastObjectiveDesiredSlotRole}\n" +
                $"{FailureRecoveryDebug}";
        }

        private string ResolveNavigationSceneDebugState()
        {
            if (_navAgent == null)
                return "Nav=None";

            if (!_navAgent.HasDestination)
                return $"Nav=Idle zero={_navAgent.ConsecutiveActiveZeroMoveTicks}";

            float distance = _brawler != null
                ? PlanarDistance(_brawler.Position, _navAgent.Destination)
                : 0f;
            string state = _navAgent.IsRouteBlocked
                ? "Blocked"
                : _navAgent.DebugHasPath ? "Path" : "Direct";

            return
                $"Nav={state} d={distance:0.0} path={_navAgent.DebugPathIndex}/{_navAgent.DebugPathNodeCount} " +
                $"stuck={_navAgent.ConsecutiveStuckSamples} def={_navAgent.ConsecutivePathBudgetDeferrals}";
        }

        private string BuildScenePerformanceSummary()
        {
            if (_profile == null)
                return "Profile=None";

            return
                $"R={_profile.ReactionDelayTicks}+{_profile.HumanizationReactionJitterTicks} " +
                $"Aim={_profile.AimErrorDegrees:0.0} " +
                $"Obj={(_profile.ObjectiveWeight * _profile.MacroActionBiasWeight):0.00} " +
                $"Team={_profile.TeamRoleCoordinationWeight:0.00} " +
                $"Res={_profile.OpponentResourceAwarenessWeight:0.00} " +
                $"Threat={_profile.AbilityThreatPredictionWeight:0.00} " +
                $"Fight={_profile.TeamFightCoordinationWeight:0.00} " +
                $"Role={_profile.RoleMacroBehaviorWeight:0.00} " +
                $"Match={_profile.RoleMatchupAwarenessWeight:0.00} " +
                $"Cover={_profile.CoverPeekPlannerWeight:0.00} " +
                $"Clutch={_profile.ModeClutchAwarenessWeight:0.00} " +
                $"Space={_profile.SpacingAwarenessWeight:0.00} " +
                $"Risk={_profile.EngagementRiskAwarenessWeight:0.00} " +
                $"Rot={_profile.PressureRotationAwarenessWeight:0.00} " +
                $"Conf={_profile.DecisionAmbiguityScoreWindow:0.0}/{_profile.DecisionAmbiguitySwitchPenalty:0.0}";
        }

        private string ResolveTargetDebugName()
        {
            if (_targetInfo == null ||
                !_targetInfo.HasLiveTarget ||
                _targetInfo.Target == null)
            {
                return "None";
            }

            if (_targetInfo.Target is BrawlerController targetBrawler)
            {
                string nameLabel = targetBrawler.Definition != null
                    ? targetBrawler.Definition.BrawlerName
                    : targetBrawler.name;
                return $"{nameLabel}#{targetBrawler.EntityID}";
            }

            return $"Entity#{_targetInfo.Target.EntityID}";
        }

        private Vector3 ResolveDebugOrigin()
        {
            if (_brawler != null)
                return _brawler.Position;

            return transform.position;
        }

        private Color ResolveNavigationColor()
        {
            if (_navAgent != null && _navAgent.IsRouteBlocked)
                return SceneBlockedColor;

            if (_navAgent != null && _navAgent.DebugHasPath)
                return ScenePathColor;

            return SceneDestinationColor;
        }

        private static Color ResolveActionColor(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return new Color(1f, 0.08f, 0.72f, 0.98f);

                case AIActionType.HoldRange:
                    return new Color(0.00f, 0.92f, 1f, 0.98f);

                case AIActionType.Reposition:
                    return new Color(0.16f, 1f, 0.45f, 0.98f);

                case AIActionType.Retreat:
                    return new Color(1f, 0.05f, 0.14f, 0.98f);

                case AIActionType.Evade:
                    return new Color(0.80f, 0.18f, 1f, 0.98f);

                case AIActionType.UseSuper:
                    return new Color(0.48f, 0.18f, 1f, 0.98f);

                case AIActionType.Regroup:
                    return new Color(0.16f, 0.50f, 1f, 0.98f);

                case AIActionType.Peel:
                    return new Color(0.00f, 1f, 0.78f, 0.98f);

                case AIActionType.Objective:
                    return new Color(0.58f, 0.22f, 1f, 0.98f);

                case AIActionType.Search:
                    return new Color(0.58f, 0.82f, 1f, 0.98f);

                case AIActionType.Wander:
                    return new Color(0.78f, 0.94f, 1f, 0.90f);

                default:
                    return new Color(0.92f, 0.92f, 0.92f, 0.86f);
            }
        }

        private static Color ResolveDecisionPerformanceColor(float score)
        {
            if (score >= 78f)
                return new Color(0.12f, 1f, 0.46f, 0.98f);

            if (score >= 50f)
                return new Color(0.00f, 0.92f, 1f, 0.98f);

            if (score >= 35f)
                return new Color(0.92f, 0.18f, 1f, 0.98f);

            return SceneBlockedColor;
        }

        private static void DrawArrow(Vector3 origin, Vector3 vector, Color color)
        {
            if (vector.sqrMagnitude <= 0.0001f)
                return;

            Vector3 end = origin + vector;
            Vector3 direction = vector.normalized;
            Vector3 right = Quaternion.AngleAxis(28f, Vector3.up) * -direction;
            Vector3 left = Quaternion.AngleAxis(-28f, Vector3.up) * -direction;
            float headLength = Mathf.Min(0.35f, vector.magnitude * 0.35f);

            Gizmos.color = color;
            DrawBoldLine(origin, end, color, 3.2f);
            DrawBoldLine(end, end + right * headLength, color, 3.2f);
            DrawBoldLine(end, end + left * headLength, color, 3.2f);
        }

        private static void DrawBoldLine(Vector3 from, Vector3 to, Color color, float width)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(Mathf.Max(1f, width), from, to);
            Gizmos.color = color;
            Gizmos.DrawLine(from, to);
        }

        private static GUIStyle ResolveSceneDebugLabelStyle()
        {
            if (_sceneDebugLabelStyle != null)
                return _sceneDebugLabelStyle;

            _sceneDebugLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                richText = true,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _sceneDebugLabelStyle.normal.textColor = Color.white;
            return _sceneDebugLabelStyle;
        }

        private static string SceneRich(string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{text}</b></color>";
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
#endif

        private string FormatVector(Vector3 value)
        {
            return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            Vector3 offset = a - b;
            offset.y = 0f;
            return offset.magnitude;
        }

        private void ReportCurrentTargetFocus()
        {
            if (_teamCoordinator == null || _targetInfo == null)
                return;

            if (_targetInfo.HasLiveTarget && _targetInfo.Target != null)
            {
                _teamCoordinator.ReportTargetFocusCount(_targetInfo.Target.EntityID);
                return;
            }

            _teamCoordinator.ClearTargetFocusCount();
        }

        private void DeferSense(uint currentTick)
        {
            uint delay = _profile != null && _profile.BudgetDeferredSenseTicks > 0u
                ? _profile.BudgetDeferredSenseTicks
                : 1u;

            _nextSenseTick = currentTick + delay;
        }

        private void DeferDangerRefresh(uint currentTick)
        {
            uint delay = _profile != null && _profile.BudgetDeferredDangerTicks > 0u
                ? _profile.BudgetDeferredDangerTicks
                : 1u;

            _nextDangerRefreshTick = currentTick + delay;
        }

    }
}
