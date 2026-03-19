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

        private BrawlerAIProfile _profile;

        private uint _nextSenseTick;
        private bool _brainInitialized;

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
                _brawler.SetMoveInput(Vector3.zero);
                return;
            }

            if (currentTick >= _nextSenseTick)
            {
                _perception.UpdateTarget(_brawler, _targetInfo, currentTick);
                ScheduleNextSense(currentTick);
            }
            _teamCoordinator.UpdateTeamSignals(_targetInfo, currentTick);

            AIActionScore bestAction = _utilityScorer.ScoreBestAction(_targetInfo, currentTick);

            _actionExecutor.Execute(
                bestAction.ActionType,
                _targetInfo,
                currentTick,
                GetAbilityMaxRange(),
                GetAbilityIdealRange(),
                GetSuperMaxRange());

            _navAgent.Tick();
        }

        private void TryInitializeBrain()
        {
            if (_brainInitialized || _brawler == null || _brawler.Definition == null)
                return;

            _profile = ResolveAIProfile(_brawler.Definition);
            _profile.ApplyArchetypeDefaults();

            _targetInfo = new AITargetInfo();
            _navAgent = new NavigationAgent(_brawler);
            _targetScorer = new AITargetScorer(_brawler, _profile);
            _objectiveMemory = new AIObjectiveMemory();
            _teamCoordinator = new AITeamCoordinator(_brawler);

            _perception = new AIPerception(_profile.DetectionRadius, _profile.MemoryDurationTicks, _targetScorer);
            _abilityDecider = new AIAbilityDecider(_brawler, _profile);
            _superDecider = new AISuperDecider(_brawler, _profile);
            _utilityScorer = new AIUtilityScorer(_brawler, _profile, _objectiveMemory, _teamCoordinator);
            _actionExecutor = new AIActionExecutor(_brawler, _profile, _navAgent, _abilityDecider, _superDecider, _objectiveMemory, _teamCoordinator);

            var objectivePoints = FindObjectsOfType<AIObjectivePoint>();
            for (int i = 0; i < objectivePoints.Length; i++)
            {
                _objectiveMemory.Register(objectivePoints[i]);
            }

            _nextSenseTick = (uint)Random.Range(0, 8);
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
    }
}