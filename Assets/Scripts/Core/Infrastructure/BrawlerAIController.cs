using UnityEngine;
using UnityEngine.SceneManagement;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;

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
        [SerializeField] private AIDifficultyLevel _difficulty = AIDifficultyLevel.Normal;
        [SerializeField] private AIPersonalityType _personality = AIPersonalityType.Balanced;
        [SerializeField] private AITuningCatalog _tuningCatalog;

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
        private AIHumanizationController _humanization;
        private AICommandSource _commandSource;

        private BrawlerAIProfile _baseProfileSource;
        private BrawlerAIProfile _profile;
        private AITuningCatalog _activeTuningCatalog;
        private int _runtimeTuningVersion = -1;

        private uint _nextSenseTick;
        private uint _nextDangerRefreshTick;
        private uint _nextDebugSnapshotTick;
        private uint _nextBudgetWarningTick;
        private bool _brainInitialized;
        private string _lastReactiveDebug = "Reactive=None";
        private string _lastDangerDebug = "Danger=None";
        private string _lastFailureRecoveryDebug = "Recovery=None";
        private string _lastTuningDebug = "Tuning=None";
        private string _lastOpponentModelDebug = "Opponent=None";
        private string _lastBudgetDebug = "Budget=OK map=0/0 paths=0/0 nodes=0/0 maxNodes=0";
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

        public AIDifficultyLevel Difficulty => _profile != null ? _profile.Difficulty : _difficulty;
        public AIPersonalityType Personality => _profile != null ? _profile.Personality : _personality;
        public string ReactiveDebug => _lastReactiveDebug;
        public string DangerDebug => _lastDangerDebug;
        public string FailureRecoveryDebug => _lastFailureRecoveryDebug;
        public string TeamRoleDebug =>
            _utilityScorer != null ? _utilityScorer.LastTeamRoleDebug : "RoleCoord=None";
        public string MacroDebug =>
            _utilityScorer != null ? _utilityScorer.LastMacroDebug : "Macro=None";
        public string PlaybookDebug =>
            _utilityScorer != null ? _utilityScorer.LastPlaybookDebug : "Playbook=None";
        public string OpponentModelDebug => _lastOpponentModelDebug;
        public string HumanizationDebug =>
            _humanization != null ? _humanization.DebugSummary : "Human=None";
        public string TuningDebug => _lastTuningDebug;

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
                _actionCommitment?.Reset();
                _humanization?.Reset();
                _teamCoordinator?.ClearTargetFocusCount();
                _teamCoordinator?.ClearActionIntent();

                _commandSource?.ClearQueuedCommands();

                if (_profile != null && _profile.LogDecisionTicks && currentTick % 30 == 0)
                    Debug.Log($"[AI-{(_brawler != null ? _brawler.name : "?")}] CanRunAI=false brain={_brainInitialized} brawlerNull={_brawler == null} stateNull={(_brawler == null ? "?" : (_brawler.State == null).ToString())} dead={(_brawler == null || _brawler.State == null ? "?" : _brawler.State.IsDead.ToString())} gridNull={SimulationClock.Grid == null}");

                return;
            }

            if (_profile.LogDecisionTicks && currentTick % 30 == 0)
                Debug.Log($"[AI-{_brawler.name}] tick ok hasTarget={_targetInfo.HasLiveTarget} action={_lastChosenAction.ActionType} score={_lastChosenAction.Score:0.0}");

            RefreshRuntimeTuningIfNeeded(currentTick);

            if (_brawler.State.HasStatus(StatusEffectType.Stun))
            {
                _actionCommitment?.Reset();
                _humanization?.Reset();
                _teamCoordinator?.ClearTargetFocusCount();
                _teamCoordinator?.ClearActionIntent();
                _commandSource?.QueueMove(Vector3.zero);
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

            UpdateFailureRecovery(currentTick);

            UpdateProductionBudget(currentTick);
            UpdateDebugSnapshotIfDue(currentTick);
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
            _debugSnapshot.IsRevealed = _brawler.State.HasStatus(StatusEffectType.Reveal);

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
            }
            else
            {
                _debugSnapshot.TeamTactic = "None";
                _debugSnapshot.TeamSignalDebug = "Threat=None Hotspot=None";
                _debugSnapshot.TeamRoleDebug = "RoleCoord=None";
                _debugSnapshot.MacroDebug = "Macro=None";
                _debugSnapshot.PlaybookDebug = "Playbook=None";
            }

            _lastReactiveDebug = _reactiveMemory != null && _profile != null
                ? _reactiveMemory.GetDebugSummary(currentTick, _profile.ReactiveDamageMemoryTicks)
                : "Reactive=None";
            _debugSnapshot.ReactiveDebug = _lastReactiveDebug;
            _lastDangerDebug = _dangerMemory != null
                ? _dangerMemory.GetDebugSummary()
                : "Danger=None";
            _debugSnapshot.DangerDebug = _lastDangerDebug;
            _debugSnapshot.FailureRecoveryDebug = _lastFailureRecoveryDebug;
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
                    $"Center={FormatVector(LastObjectiveCenter)} " +
                    $"Slot={FormatVector(LastObjectiveSlot)} " +
                    $"Dest={FormatVector(LastObjectiveDestination)} " +
                    $"Pressure={LastObjectiveAllyPressure:0.00} " +
                    $"Penalty={LastObjectiveCrowdingPenalty:0.0} " +
                    $"Raw={LastObjectiveRawScore:0.0} " +
                    $"Final={LastObjectiveFinalScore:0.0} " +
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
$"Map={LastMapRouteDebug}";

            _debugSnapshot.PerformanceDebug = AIPerformanceTracker.GetDebugSummary(currentTick);
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
            _debugSnapshot.MacroDebug = MacroDebug;
            _debugSnapshot.PlaybookDebug = PlaybookDebug;

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

            _commandSource = new AICommandSource();
            _brawler.SetCommandSource(_commandSource);

            _navAgent = new NavigationAgent(_brawler, _commandSource, _profile);
            _targetScorer = new AITargetScorer(_brawler, _profile);
            _objectiveMemory = new AIObjectiveMemory();
            _teamCoordinator = new AITeamCoordinator(_brawler);
            _reactiveMemory = new AIReactiveMemory();
            _dangerMemory = new AIDangerMemory();
            _failureRecovery = new AIFailureRecoveryMemory();
            _humanization = new AIHumanizationController(_profile, _brawler.EntityID);

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
                _activeTuningCatalog);
            _runtimeTuningVersion = AITuningRuntimeOverrides.Version;
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
                   SimulationClock.Grid != null;
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
                _baseProfileSource == null ||
                (_runtimeTuningVersion == AITuningRuntimeOverrides.Version &&
                 _activeTuningCatalog == resolvedCatalog))
            {
                return;
            }

            _activeTuningCatalog = resolvedCatalog;
            AIProfileTuningUtility.RebuildRuntimeTuning(
                _baseProfileSource,
                _profile,
                _difficulty,
                _personality,
                _activeTuningCatalog);
            _runtimeTuningVersion = AITuningRuntimeOverrides.Version;
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
            _nextSenseTick = currentTick;
            _nextDangerRefreshTick = currentTick;
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
                _actionExecutor?.HandleFailureRecovery(request, currentTick);

                bool recovered = _navAgent.TryRequestRecoveryDestination(
                    request,
                    _profile,
                    out Vector3 recoveryDestination);
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

        private void UpdateProductionBudget(uint currentTick)
        {
            if (_profile == null)
            {
                _lastBudgetDebug = "Budget=NoProfile";
                return;
            }

            bool overBudget = AIPerformanceTracker.IsOverBudget(
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
                $"Tuning {catalog} " +
                $"{AITuningRuntimeOverrides.GetDebugSummary()}";
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                return;

            if (HasObjectiveDebug)
            {
                Gizmos.DrawWireSphere(
                    LastObjectiveCenter,
                    Mathf.Max(0.35f, LastObjectiveRadius));
                Gizmos.DrawWireSphere(LastObjectiveSlot, 0.45f);
                Gizmos.DrawLine(LastObjectiveCenter, LastObjectiveSlot);
            }

            if (LastTacticalMovementIntent != AITacticalMovementIntent.None)
            {
                Gizmos.DrawWireSphere(LastTacticalMoveDestination, 0.3f);
                Gizmos.DrawLine(transform.position, LastTacticalMoveDestination);
            }
        }
#endif

        private string FormatVector(Vector3 value)
        {
            return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
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
