using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Infrastructure
{
    public class BrawlerAIController : SimulationEntity
    {
        [SerializeField] private BrawlerController _brawler;
        private AISuperDecider _superDecider;
        private AIReactiveListener _reactiveListener;

        [Header("Patrol")]
        [SerializeField] private AIPatrolMode _patrolMode = AIPatrolMode.Loop;
        [SerializeField] private Transform[] _patrolPoints;

        private NavigationAgent _navAgent;
        private AIPerception _perception;
        private AITargetInfo _targetInfo;
        private AITargetScorer _targetScorer;
        private AIAbilityDecider _abilityDecider;

        private AICombatState _combatState = AICombatState.Idle;
        private BrawlerAIProfile _profile;

        private uint _nextSenseTick;
        private uint _nextFallbackWanderTick;
        private uint _nextStrafeTick;

        private Vector3 _fallbackWanderPoint;
        private int _patrolIndex;
        private int _patrolDirection = 1;

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

            if (currentTick >= _nextSenseTick)
            {
                _perception.UpdateTarget(_brawler, _targetInfo, currentTick);
                ScheduleNextSense(currentTick);
            }

            DecideState(currentTick);
            RunState(currentTick);
            _navAgent.Tick();
        }

        private void TryInitializeBrain()
        {
            if (_brainInitialized || _brawler == null || _brawler.Definition == null)
                return;

            _profile = ResolveAIProfile(_brawler.Definition);
            _targetInfo = new AITargetInfo();
            _reactiveListener = new AIReactiveListener(_brawler, _targetInfo);
            _navAgent = new NavigationAgent(_brawler);
            _targetScorer = new AITargetScorer(_brawler, _profile);
            _perception = new AIPerception(_profile.DetectionRadius, _profile.MemoryDurationTicks, _targetScorer);
            _abilityDecider = new AIAbilityDecider(_brawler, _profile);
            _superDecider = new AISuperDecider(_brawler, _profile);
            _reactiveListener = new AIReactiveListener(_brawler, _targetInfo);

            _nextSenseTick = (uint)Random.Range(0, 8);
            _brainInitialized = true;
        }

        private float GetSuperMaxRange()
        {
            var super = _brawler.Definition?.SuperAbility;
            return super != null ? super.GetAIMaxRange() : 6f;
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
            bool hot = _targetInfo.HasLiveTarget || _combatState == AICombatState.Search;
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

        private void DecideState(uint currentTick)
        {
            if (_targetInfo.HasLiveTarget && IsTargetStillUsable(_targetInfo.Target))
            {
                _targetInfo.RefreshLastKnownPosition(currentTick);

                Vector3 toTarget = _targetInfo.Target.Position - _brawler.Position;
                float distSq = toTarget.sqrMagnitude;

                float idealRange = GetAbilityIdealRange();
                float attackRange = GetAbilityMaxRange();
                float preferredRange = _profile.GetPreferredAttackRange(idealRange);
                float tooCloseDistance = _profile.GetTooCloseDistance(idealRange);

                float attackRangeSq = attackRange * attackRange;
                float preferredRangeSq = preferredRange * preferredRange;
                float tooCloseSq = tooCloseDistance * tooCloseDistance;
                float leashSq = _profile.LeashDistance * _profile.LeashDistance;

                if (distSq > leashSq)
                {
                    _targetInfo.LoseLiveTarget();
                }
                else
                {
                    float healthRatio = _brawler.State.CurrentHealth / Mathf.Max(1f, _brawler.State.MaxHealth.Value);

                    if (healthRatio <= _profile.LowHealthRetreatRatio && distSq <= tooCloseSq)
                    {
                        _combatState = AICombatState.Retreat;
                        return;
                    }

                    if (distSq < tooCloseSq)
                    {
                        _combatState = AICombatState.Reposition;
                        return;
                    }

                    float bufferedAttackRange = attackRange + _profile.AttackRangeBuffer;
                    float bufferedAttackRangeSq = bufferedAttackRange * bufferedAttackRange;

                    if (distSq > bufferedAttackRangeSq)
                    {
                        _combatState = AICombatState.Approach;
                        return;
                    }

                    if (distSq <= attackRangeSq && distSq >= preferredRangeSq * 0.60f)
                    {
                        _combatState = AICombatState.HoldRange;
                        return;
                    }

                    if (distSq <= attackRangeSq)
                    {
                        _combatState = AICombatState.Reposition;
                        return;
                    }

                    _combatState = AICombatState.Approach;
                    return;
                }
            }

            if (_targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
            {
                _combatState = AICombatState.Search;
                return;
            }

            if (AITeamMemory.TryGetRecentHotspot(_brawler.Team, currentTick, _profile.SharedHotspotMemoryTicks, out _))
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
                    RunApproach(currentTick);
                    break;

                case AICombatState.HoldRange:
                    RunHoldRange(currentTick);
                    break;

                case AICombatState.Reposition:
                    RunReposition(currentTick);
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

        private void RunApproach(uint currentTick)
        {
            if (!_targetInfo.HasLiveTarget)
                return;

            float desiredArrival = Mathf.Max(0.8f, _profile.GetPreferredAttackRange(GetAbilityIdealRange()) * 0.9f);
            _navAgent.RequestDestination(_targetInfo.Target.Position, desiredArrival);

            float maxRange = GetAbilityMaxRange();
            _abilityDecider.TryUseMainAttack(_targetInfo.Target, currentTick, maxRange);
            _abilityDecider.TryUseGadget(_targetInfo.Target, currentTick);

            float superRange = GetSuperMaxRange();
            _superDecider.TryUseSuper(_targetInfo.Target, currentTick, superRange);
        }

        private void RunHoldRange(uint currentTick)
        {
            if (!_targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            float maxRange = GetAbilityMaxRange();
            _abilityDecider.TryUseMainAttack(_targetInfo.Target, currentTick, maxRange);
            _abilityDecider.TryUseGadget(_targetInfo.Target, currentTick);

            float superRange = GetSuperMaxRange();
            _superDecider.TryUseSuper(_targetInfo.Target, currentTick, superRange);

            if (!_profile.UseStrafe)
            {
                _navAgent.Stop();
                return;
            }

            if (currentTick >= _nextStrafeTick || !_navAgent.HasDestination)
            {
                Vector3 toTarget = (_targetInfo.Target.Position - _brawler.Position).normalized;
                Vector3 right = new Vector3(toTarget.z, 0f, -toTarget.x);

                if (right.sqrMagnitude < 0.0001f)
                    right = _brawler.transform.right;

                float side = Random.value < 0.5f ? -1f : 1f;
                Vector3 strafePoint = _brawler.Position + (right * side * _profile.StrafeDistance);

                _navAgent.RequestDestination(strafePoint, 0.4f);
                _nextStrafeTick = currentTick + _profile.StrafeRetargetTicks;
            }
        }

        private void RunReposition(uint currentTick)
        {
            if (!_targetInfo.HasLiveTarget)
                return;

            Vector3 away = (_brawler.Position - _targetInfo.Target.Position).normalized;
            if (away.sqrMagnitude < 0.0001f)
                away = _brawler.transform.forward;

            Vector3 repositionPoint = _brawler.Position + away * _profile.RepositionStepDistance;
            _navAgent.RequestDestination(repositionPoint, 0.4f);

            float maxRange = GetAbilityMaxRange();
            _abilityDecider.TryUseMainAttack(_targetInfo.Target, currentTick, maxRange);

            float superRange = GetSuperMaxRange();
            _superDecider.TryUseSuper(_targetInfo.Target, currentTick, superRange);
        }

        private void RunRetreat()
        {
            if (!_targetInfo.HasLiveTarget)
                return;

            Vector3 away = (_brawler.Position - _targetInfo.Target.Position).normalized;
            if (away.sqrMagnitude < 0.0001f)
                away = _brawler.transform.forward;

            Vector3 retreatPoint = _brawler.Position + away * _profile.RetreatStepDistance;
            _navAgent.RequestDestination(retreatPoint, 0.5f);
        }

        private void RunSearch(uint currentTick)
        {
            if (_targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
            {
                _navAgent.RequestDestination(_targetInfo.LastKnownPosition, 1.0f);
                return;
            }

            if (AITeamMemory.TryGetRecentHotspot(_brawler.Team, currentTick, _profile.SharedHotspotMemoryTicks, out var destination))
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

            _navAgent.RequestDestination(point.position, _profile.PatrolArrivalDistance);

            float distSq = (point.position - _brawler.Position).sqrMagnitude;
            if (distSq <= (_profile.PatrolArrivalDistance * _profile.PatrolArrivalDistance))
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
                Vector2 random2D = Random.insideUnitCircle * _profile.FallbackWanderRadius;
                _fallbackWanderPoint = _brawler.Position + new Vector3(random2D.x, 0f, random2D.y);
                _navAgent.RequestDestination(_fallbackWanderPoint, 0.5f);
                _nextFallbackWanderTick = currentTick + _profile.FallbackWanderRetargetTicks;
            }
        }

        private bool IsTargetStillUsable(ISpatialEntity target)
        {
            if (target == null || target.Team == _brawler.Team)
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

        protected override void OnDisable()
        {
            base.OnDisable();
            _reactiveListener?.Dispose();
        }
    }
}