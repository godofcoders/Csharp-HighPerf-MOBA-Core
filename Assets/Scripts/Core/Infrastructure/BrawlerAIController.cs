using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerAIController : SimulationEntity
    {
        [SerializeField] private BrawlerController _brawler;

        private NavigationAgent _navAgent;
        private AIPerception _perception;
        private AITargetInfo _targetInfo;
        private AITargetScorer _targetScorer;
        private AIAbilityDecider _abilityDecider;
        private AISuperDecider _superDecider;
        private AIUtilityScorer _utilityScorer;
        private AIActionExecutor _actionExecutor;
        private AIObjectiveMemory _objectiveMemory;
        private AITeamCoordinator _teamCoordinator;
        private AICommandSource _commandSource;

        private BrawlerAIProfile _profile;

        private uint _nextSenseTick;
        private bool _brainInitialized;
        private readonly AIDebugSnapshot _debugSnapshot = new AIDebugSnapshot();
        private readonly System.Collections.Generic.List<AIActionScore> _debugScores = new System.Collections.Generic.List<AIActionScore>(16);
        private AIActionScore _lastChosenAction;

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

        public override void Tick(uint currentTick)
        {
            if (!CanRunAI())
                return;

            if (_brawler.State.HasStatus(StatusEffectType.Stun))
            {
                _commandSource?.QueueMove(Vector3.zero);
                return;
            }

            if (currentTick >= _nextSenseTick)
            {
                _perception.UpdateTarget(_brawler, _targetInfo, currentTick);
                ScheduleNextSense(currentTick);
            }

            _teamCoordinator.UpdateTeamSignals(_targetInfo, currentTick);

            _utilityScorer.CollectActionScores(_targetInfo, currentTick, _debugScores);

            AIActionScore bestAction = _utilityScorer.ScoreBestAction(_targetInfo, currentTick);
            _lastChosenAction = bestAction;

            _actionExecutor.Execute(
                bestAction.ActionType,
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
            _debugSnapshot.CurrentAction = _lastChosenAction.ActionType.ToString();
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
            }
            else
            {
                _debugSnapshot.TeamTactic = "None";
            }

            _debugSnapshot.ObjectiveName = _profile != null ? _profile.PreferredObjective.ToString() : "None";

            for (int i = 0; i < _debugScores.Count; i++)
            {
                _debugSnapshot.ActionScores.Add(_debugScores[i]);
            }

            for (int i = 0; i < _brawler.State.ActiveStatusEffects.Count; i++)
            {
                _debugSnapshot.ActiveStatuses.Add(_brawler.State.ActiveStatusEffects[i].Type.ToString());
            }

            AIDebugTracker.UpdateSnapshot(_brawler, _debugSnapshot);
        }

        private void TryInitializeBrain()
        {
            if (_brainInitialized || _brawler == null || _brawler.Definition == null)
                return;

            _profile = ResolveAIProfile(_brawler.Definition);
            _profile.ApplyArchetypeDefaults(_brawler.Definition.Archetype);

            _targetInfo = new AITargetInfo();

            _commandSource = new AICommandSource();
            _brawler.SetCommandSource(_commandSource);

            _navAgent = new NavigationAgent(_brawler, _commandSource);
            _targetScorer = new AITargetScorer(_brawler, _profile);
            _objectiveMemory = new AIObjectiveMemory();
            _teamCoordinator = new AITeamCoordinator(_brawler);

            _perception = new AIPerception(_profile.DetectionRadius, _profile.MemoryDurationTicks, _targetScorer);
            _abilityDecider = new AIAbilityDecider(_brawler, _profile, _commandSource);
            _superDecider = new AISuperDecider(_brawler, _profile, _commandSource);

            _utilityScorer = new AIUtilityScorer(_brawler, _profile, _objectiveMemory, _teamCoordinator);
            _actionExecutor = new AIActionExecutor(_brawler, _profile, _navAgent, _abilityDecider, _superDecider, _objectiveMemory, _teamCoordinator, _commandSource);

            var objectivePoints = FindObjectsOfType<AIObjectivePoint>();
            for (int i = 0; i < objectivePoints.Length; i++)
            {
                _objectiveMemory.Register(objectivePoints[i]);
            }

            _nextSenseTick = (uint)Random.Range(0, 8);
            AIDebugTracker.Register(_brawler);

            _brainInitialized = true;
        }

        private BrawlerAIProfile ResolveAIProfile(BrawlerDefinition definition)
        {
            if (definition != null && definition.AIProfile != null)
                return definition.AIProfile;

            Debug.LogWarning($"Brawler '{definition?.BrawlerName}' has no AIProfile assigned. Using emergency fallback values.");
            return ScriptableObject.CreateInstance<BrawlerAIProfile>();
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
            _nextSenseTick = currentTick + (hot ? _profile.CombatSenseIntervalTicks : _profile.IdleSenseIntervalTicks);
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

            if (_brawler != null)
            {
                AIDebugTracker.Unregister(_brawler);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_brawler == null)
                return;

            var snapshot = AIDebugTracker.GetSnapshot(_brawler.EntityID);
            if (snapshot == null || !snapshot.TargetPosition.HasValue)
                return;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(_brawler.Position, snapshot.TargetPosition.Value);
            Gizmos.DrawSphere(snapshot.TargetPosition.Value, 0.25f);
        }
    }
}