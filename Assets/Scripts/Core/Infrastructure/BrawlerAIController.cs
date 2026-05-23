using UnityEngine;
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
        private AICommandSource _commandSource;

        private BrawlerAIProfile _profile;

        private uint _nextSenseTick;
        private uint _nextDangerRefreshTick;
        private bool _brainInitialized;
        private string _lastReactiveDebug = "Reactive=None";
        private string _lastDangerDebug = "Danger=None";
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
        public string TeamRoleDebug =>
            _utilityScorer != null ? _utilityScorer.LastTeamRoleDebug : "RoleCoord=None";

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
                EnsureReactiveListener();
        }

        public override void Tick(uint currentTick)
        {
            if (!CanRunAI())
            {
                _actionCommitment?.Reset();
                _teamCoordinator?.ClearTargetFocusCount();
                _teamCoordinator?.ClearActionIntent();

                if (currentTick % 30 == 0)
                    Debug.Log($"[AI-{(_brawler != null ? _brawler.name : "?")}] CanRunAI=false brain={_brainInitialized} brawlerNull={_brawler == null} stateNull={(_brawler == null ? "?" : (_brawler.State == null).ToString())} dead={(_brawler == null || _brawler.State == null ? "?" : _brawler.State.IsDead.ToString())} gridNull={SimulationClock.Grid == null}");

                return;
            }

            if (currentTick % 30 == 0)
                Debug.Log($"[AI-{_brawler.name}] tick ok hasTarget={_targetInfo.HasLiveTarget} action={_lastChosenAction.ActionType} score={_lastChosenAction.Score:0.0}");

            if (_brawler.State.HasStatus(StatusEffectType.Stun))
            {
                _actionCommitment?.Reset();
                _teamCoordinator?.ClearTargetFocusCount();
                _teamCoordinator?.ClearActionIntent();
                _commandSource?.QueueMove(Vector3.zero);
                return;
            }

            if (currentTick >= _nextSenseTick)
            {
                _perception.UpdateTarget(_brawler, _targetInfo, currentTick);
                ScheduleNextSense(currentTick);
            }

            _teamCoordinator.UpdateTeamSignals(_targetInfo, currentTick);
            ReportCurrentTargetFocus();
            RefreshDangerIfDue(currentTick);

            _utilityScorer.CollectActionScores(_targetInfo, currentTick, _debugScores);

            AIActionScore chosenAction = _actionCommitment.SelectAction(
                _debugScores,
                currentTick,
                _brawler.name);

            _lastChosenAction = chosenAction;
            _teamCoordinator?.ReportActionIntent(chosenAction.ActionType, currentTick);

            _actionExecutor.Execute(
                chosenAction.ActionType,
                _targetInfo,
                currentTick,
                GetAbilityMaxRange(),
                GetAbilityIdealRange(),
                GetSuperMaxRange());

            UpdateDebugSnapshot(currentTick);

            _navAgent.Tick();
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
            }
            else
            {
                _debugSnapshot.TeamTactic = "None";
                _debugSnapshot.TeamSignalDebug = "Threat=None Hotspot=None";
                _debugSnapshot.TeamRoleDebug = "RoleCoord=None";
            }

            _lastReactiveDebug = _reactiveMemory != null && _profile != null
                ? _reactiveMemory.GetDebugSummary(currentTick, _profile.ReactiveDamageMemoryTicks)
                : "Reactive=None";
            _debugSnapshot.ReactiveDebug = _lastReactiveDebug;
            _lastDangerDebug = _dangerMemory != null
                ? _dangerMemory.GetDebugSummary()
                : "Danger=None";
            _debugSnapshot.DangerDebug = _lastDangerDebug;

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

            AIDebugTracker.UpdateSnapshot(_brawler, _debugSnapshot);
        }

        private void TryInitializeBrain()
        {

            if (_brainInitialized || _brawler == null || _brawler.Definition == null)
                return;

            _profile = ResolveAIProfile(_brawler.Definition);

            _targetInfo = new AITargetInfo();

            _commandSource = new AICommandSource();
            _brawler.SetCommandSource(_commandSource);

            _navAgent = new NavigationAgent(_brawler, _commandSource);
            _targetScorer = new AITargetScorer(_brawler, _profile);
            _objectiveMemory = new AIObjectiveMemory();
            _teamCoordinator = new AITeamCoordinator(_brawler);
            _reactiveMemory = new AIReactiveMemory();
            _dangerMemory = new AIDangerMemory();

            _perception = new AIPerception(_profile.DetectionRadius, _profile.MemoryDurationTicks, _targetScorer);
            _abilityDecider = new AIAbilityDecider(_brawler, _profile, _commandSource);
            _superDecider = new AISuperDecider(_brawler, _profile, _commandSource);

            _utilityScorer = new AIUtilityScorer(_brawler, _profile, _objectiveMemory, _teamCoordinator, _reactiveMemory, _dangerMemory);
            _actionCommitment = new AIActionCommitment(_profile);
            _actionExecutor = new AIActionExecutor(_brawler, _profile, _navAgent, _abilityDecider, _superDecider, _objectiveMemory, _teamCoordinator, _commandSource, _dangerMemory);
            EnsureReactiveListener();

            var objectivePoints = FindObjectsOfType<AIObjectivePoint>();
            Debug.Log(
$"[{_brawler.name}] Registered Objectives: {objectivePoints.Length}");
            for (int i = 0; i < objectivePoints.Length; i++)
            {
                _objectiveMemory.Register(objectivePoints[i]);
            }

            _nextSenseTick = (uint)Random.Range(0, 8);
            _nextDangerRefreshTick = (uint)Random.Range(
                0,
                Mathf.Max(1, (int)_profile.DangerRefreshIntervalTicks));
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

            BrawlerAIProfile runtimeProfile = Instantiate(baseProfile);
            runtimeProfile.name = $"{baseProfile.name}_Runtime_{_difficulty}_{_personality}";

            AIProfileTuningUtility.ApplyRuntimeTuning(
                runtimeProfile,
                _difficulty,
                _personality);

            return runtimeProfile;
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
            _nextSenseTick = currentTick + baseInterval + _profile.ReactionDelayTicks;
        }

        private void RefreshDangerIfDue(uint currentTick)
        {
            if (_dangerMemory == null || _profile == null || currentTick < _nextDangerRefreshTick)
                return;

            _dangerMemory.Refresh(_brawler, _profile, currentTick);

            uint interval = _profile.DangerRefreshIntervalTicks == 0u
                ? 1u
                : _profile.DangerRefreshIntervalTicks;

            _nextDangerRefreshTick = currentTick + interval;
        }

        private float GetAbilityIdealRange()
        {
            var attack = _brawler.Definition?.MainAttack;
            return attack != null ? attack.GetAIIdealRange() : 6f;
        }

        private float GetAbilityMaxRange()
        {
            var attack = _brawler.Definition?.MainAttack;
            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private float GetSuperMaxRange()
        {
            var super = _brawler.Definition?.SuperAbility;
            return super != null ? super.GetAIMaxRange() : 6f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _teamCoordinator?.ClearTargetFocusCount();
            _teamCoordinator?.ClearActionIntent();
            _reactiveListener?.Dispose();
            _reactiveListener = null;

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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
                return;

            if (HasObjectiveDebug)
            {
                Gizmos.DrawWireSphere(LastObjectiveCenter, 0.35f);
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

    }
}
