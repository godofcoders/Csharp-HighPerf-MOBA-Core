using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerAIController : SimulationEntity
    {
        [SerializeField] private BrawlerController _brawler;

        [Header("Perception")]
        [SerializeField] private float _detectionRadius = 40f;
        [SerializeField] private uint _combatSenseIntervalTicks = 4;
        [SerializeField] private uint _idleSenseIntervalTicks = 12;
        [SerializeField] private uint _memoryDurationTicks = 60;
        [SerializeField] private uint _sharedHotspotMemoryTicks = 120;

        [Header("Combat")]
        [SerializeField] private float _attackRange = 6f;
        [SerializeField] private float _attackRangeBuffer = 1.0f;
        [SerializeField] private float _minPreferredDistance = 3.0f;
        [SerializeField] private float _retreatDistance = 2.0f;
        [SerializeField] private float _lowHealthRetreatRatio = 0.30f;
        [SerializeField] private uint _attackCadenceTicks = 30;
        [SerializeField] private float _leashDistance = 30f;

        [Header("Patrol")]
        [SerializeField] private AIPatrolMode _patrolMode = AIPatrolMode.Loop;
        [SerializeField] private Transform[] _patrolPoints;
        [SerializeField] private float _patrolArrivalDistance = 1.0f;
        [SerializeField] private float _fallbackWanderRadius = 8f;
        [SerializeField] private uint _fallbackWanderRetargetTicks = 120;

        private NavigationAgent _navAgent;
        private AIPerception _perception;
        private AITargetInfo _targetInfo;
        private AICombatState _combatState = AICombatState.Idle;

        private uint _nextSenseTick;
        private uint _nextAttackTick;
        private uint _nextFallbackWanderTick;
        private Vector3 _fallbackWanderPoint;

        private int _patrolIndex;
        private int _patrolDirection = 1;
        private bool _brainInitialized;

        public void SetTarget(BrawlerController brawler)
        {
            _brawler = brawler;
            InitializeBrain();
        }

        protected override void Awake()
        {
            base.Awake();

            if (_brawler == null)
                _brawler = GetComponent<BrawlerController>();

            InitializeBrain();
        }

        public override void Tick(uint currentTick)
        {
            if (!CanRunAI())
                return;

            if (currentTick >= _nextSenseTick)
            {
                _perception.UpdateTarget(_brawler, _targetInfo, currentTick);
                ScheduleNextSense(currentTick);
            }

            DecideState(currentTick);
            RunState(currentTick);
            _navAgent.Tick();
        }

        private void InitializeBrain()
        {
            if (_brainInitialized || _brawler == null)
                return;

            _navAgent = new NavigationAgent(_brawler);
            _perception = new AIPerception(_detectionRadius, _memoryDurationTicks);
            _targetInfo = new AITargetInfo();

            // Stagger first sense tick so all bots do not spike on same frame
            _nextSenseTick = (uint)Random.Range(0, 8);

            _brainInitialized = true;
        }

        private bool CanRunAI()
        {
            if (!_brainInitialized)
                return false;

            if (_brawler == null || _brawler.State == null || _brawler.State.IsDead)
                return false;

            if (SimulationClock.Grid == null)
                return false;

            return true;
        }

        private void ScheduleNextSense(uint currentTick)
        {
            bool hot = _targetInfo.HasLiveTarget || _combatState == AICombatState.Search;
            _nextSenseTick = currentTick + (hot ? _combatSenseIntervalTicks : _idleSenseIntervalTicks);
        }

        private void DecideState(uint currentTick)
        {
            if (_targetInfo.HasLiveTarget && IsTargetStillUsable(_targetInfo.Target))
            {
                _targetInfo.RefreshLastKnownPosition(currentTick);

                Vector3 toTarget = _targetInfo.Target.Position - _brawler.Position;
                float distSq = toTarget.sqrMagnitude;
                float attackRangeSq = _attackRange * _attackRange;
                float minPreferredDistSq = _minPreferredDistance * _minPreferredDistance;
                float retreatDistSq = _retreatDistance * _retreatDistance;
                float leashSq = _leashDistance * _leashDistance;

                if ((_targetInfo.Target.Position - _brawler.Position).sqrMagnitude > leashSq)
                {
                    _targetInfo.LoseLiveTarget();
                }
                else
                {
                    float healthRatio = _brawler.State.CurrentHealth / Mathf.Max(1f, _brawler.State.MaxHealth.Value);

                    if (healthRatio <= _lowHealthRetreatRatio && distSq <= retreatDistSq)
                    {
                        _combatState = AICombatState.Retreat;
                        return;
                    }

                    if (distSq < minPreferredDistSq)
                    {
                        _combatState = AICombatState.Reposition;
                        return;
                    }

                    float bufferedAttackRange = _attackRange + _attackRangeBuffer;
                    float bufferedAttackRangeSq = bufferedAttackRange * bufferedAttackRange;

                    if (distSq > bufferedAttackRangeSq)
                    {
                        _combatState = AICombatState.Approach;
                        return;
                    }

                    if (distSq <= attackRangeSq)
                    {
                        _combatState = AICombatState.HoldRange;
                        return;
                    }

                    _combatState = AICombatState.Approach;
                    return;
                }
            }

            if (_targetInfo.HasRecentMemory(currentTick, _memoryDurationTicks))
            {
                _combatState = AICombatState.Search;
                return;
            }

            if (AITeamMemory.TryGetRecentHotspot(_brawler.Team, currentTick, _sharedHotspotMemoryTicks, out _))
            {
                _combatState = AICombatState.Search;
                return;
            }

            _combatState = AICombatState.Wander;
        }

        private void RunState(uint currentTick)
        {
            switch (_combatState)
            {
                case AICombatState.Approach:
                    RunApproach();
                    TryAttack(currentTick);
                    break;

                case AICombatState.HoldRange:
                    RunHoldRange();
                    TryAttack(currentTick);
                    break;

                case AICombatState.Reposition:
                    RunReposition();
                    TryAttack(currentTick);
                    break;

                case AICombatState.Retreat:
                    RunRetreat();
                    break;

                case AICombatState.Search:
                    RunSearch(currentTick);
                    break;

                case AICombatState.Wander:
                    RunPatrolOrFallbackWander(currentTick);
                    break;

                default:
                    _navAgent.Stop();
                    break;
            }
        }

        private void RunApproach()
        {
            if (!_targetInfo.HasLiveTarget)
                return;

            _navAgent.RequestDestination(_targetInfo.Target.Position, _attackRange * 0.9f);
        }

        private void RunHoldRange()
        {
            _navAgent.Stop();
        }

        private void RunReposition()
        {
            if (!_targetInfo.HasLiveTarget)
                return;

            Vector3 away = (_brawler.Position - _targetInfo.Target.Position).normalized;
            if (away.sqrMagnitude < 0.0001f)
                away = _brawler.transform.forward;

            Vector3 repositionPoint = _brawler.Position + away * 2.5f;
            _navAgent.RequestDestination(repositionPoint, 0.4f);
        }

        private void RunRetreat()
        {
            if (!_targetInfo.HasLiveTarget)
                return;

            Vector3 away = (_brawler.Position - _targetInfo.Target.Position).normalized;
            if (away.sqrMagnitude < 0.0001f)
                away = _brawler.transform.forward;

            Vector3 retreatPoint = _brawler.Position + away * 5f;
            _navAgent.RequestDestination(retreatPoint, 0.5f);
        }

        private void RunSearch(uint currentTick)
        {
            Vector3 destination;

            if (_targetInfo.HasRecentMemory(currentTick, _memoryDurationTicks))
            {
                destination = _targetInfo.LastKnownPosition;
                _navAgent.RequestDestination(destination, 1.0f);
                return;
            }

            if (AITeamMemory.TryGetRecentHotspot(_brawler.Team, currentTick, _sharedHotspotMemoryTicks, out destination))
            {
                _navAgent.RequestDestination(destination, 1.0f);
                return;
            }

            _combatState = AICombatState.Wander;
        }

        private void RunPatrolOrFallbackWander(uint currentTick)
        {
            if (_patrolPoints != null && _patrolPoints.Length > 0)
            {
                RunPatrol();
                return;
            }

            RunFallbackWander(currentTick);
        }

        private void RunPatrol()
        {
            Transform point = _patrolPoints[_patrolIndex];
            if (point == null)
                return;

            _navAgent.RequestDestination(point.position, _patrolArrivalDistance);

            float distSq = (point.position - _brawler.Position).sqrMagnitude;
            if (distSq <= (_patrolArrivalDistance * _patrolArrivalDistance))
            {
                AdvancePatrolIndex();
            }
        }

        private void AdvancePatrolIndex()
        {
            int length = _patrolPoints.Length;
            if (length <= 1)
                return;

            switch (_patrolMode)
            {
                case AIPatrolMode.Loop:
                    _patrolIndex++;
                    if (_patrolIndex >= length)
                        _patrolIndex = 0;
                    break;

                case AIPatrolMode.PingPong:
                    _patrolIndex += _patrolDirection;

                    if (_patrolIndex >= length)
                    {
                        _patrolDirection = -1;
                        _patrolIndex = length - 2;
                    }
                    else if (_patrolIndex < 0)
                    {
                        _patrolDirection = 1;
                        _patrolIndex = 1;
                    }
                    break;

                case AIPatrolMode.Random:
                    _patrolIndex = Random.Range(0, length);
                    break;
            }
        }

        private void RunFallbackWander(uint currentTick)
        {
            if (currentTick >= _nextFallbackWanderTick || !_navAgent.HasDestination)
            {
                Vector2 random2D = Random.insideUnitCircle * _fallbackWanderRadius;
                _fallbackWanderPoint = _brawler.Position + new Vector3(random2D.x, 0f, random2D.y);
                _navAgent.RequestDestination(_fallbackWanderPoint, 0.5f);
                _nextFallbackWanderTick = currentTick + _fallbackWanderRetargetTicks;
            }
        }

        private void TryAttack(uint currentTick)
        {
            if (!_targetInfo.HasLiveTarget)
                return;

            if (currentTick < _nextAttackTick)
                return;

            Vector3 toTarget = _targetInfo.Target.Position - _brawler.Position;
            if (toTarget.sqrMagnitude > (_attackRange * _attackRange))
                return;

            _brawler.BufferAttack(InputCommandType.MainAttack, toTarget.normalized);
            _nextAttackTick = currentTick + _attackCadenceTicks;
        }

        private bool IsTargetStillUsable(ISpatialEntity target)
        {
            if (target == null)
                return false;

            if (target.Team == _brawler.Team)
                return false;

            if (target is BrawlerController targetBrawler)
            {
                if (targetBrawler.State == null || targetBrawler.State.IsDead)
                    return false;

                if (targetBrawler.State.IsHiddenTo(_brawler.Team))
                    return false;
            }

            return true;
        }
    }
}