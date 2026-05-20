using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIActionExecutor
    {
        private readonly BrawlerController _brawler;
        private readonly BrawlerAIProfile _profile;
        private readonly NavigationAgent _navAgent;
        private readonly AIAbilityDecider _abilityDecider;
        private readonly AISuperDecider _superDecider;
        private readonly AIObjectiveMemory _objectiveMemory;
        private readonly AITeamCoordinator _teamCoordinator;
        private readonly AISpacingUtility _spacingUtility;
        private readonly AICommandSource _commandSource;

        private uint _nextFallbackWanderTick;
        private Vector3 _fallbackWanderPoint;

        private Vector3 _lastObjectiveCenter;
        private Vector3 _lastObjectiveSlot;
        private Vector3 _lastObjectiveDestination;
        private string _lastObjectiveName;
        private bool _hasObjectiveDebug;

        public bool HasObjectiveDebug => _hasObjectiveDebug;
        public Vector3 LastObjectiveCenter => _lastObjectiveCenter;
        public Vector3 LastObjectiveSlot => _lastObjectiveSlot;
        public Vector3 LastObjectiveDestination => _lastObjectiveDestination;
        public string LastObjectiveName => _lastObjectiveName;
        private AITacticalMovementIntent _lastTacticalMovementIntent;
        private Vector3 _lastTacticalMoveDestination;
        private uint _nextTacticalMoveRetargetTick;
        private int _lastStrafeSide = 1;
        public AITacticalMovementIntent LastTacticalMovementIntent => _lastTacticalMovementIntent;
        public Vector3 LastTacticalMoveDestination => _lastTacticalMoveDestination;

        private float _lastTacticalTargetDistance;
        private float _lastTacticalPreferredRange;
        private float _lastTacticalTooCloseDistance;
        private uint _lastTacticalRetargetTick;
        private string _lastTacticalMoveReason;

        public float LastTacticalTargetDistance => _lastTacticalTargetDistance;
        public float LastTacticalPreferredRange => _lastTacticalPreferredRange;
        public float LastTacticalTooCloseDistance => _lastTacticalTooCloseDistance;
        public uint LastTacticalRetargetTick => _lastTacticalRetargetTick;
        public uint NextTacticalMoveRetargetTick => _nextTacticalMoveRetargetTick;
        public string LastTacticalMoveReason => _lastTacticalMoveReason;

        public AIActionExecutor(
            BrawlerController brawler,
            BrawlerAIProfile profile,
            NavigationAgent navAgent,
            AIAbilityDecider abilityDecider,
            AISuperDecider superDecider,
            AIObjectiveMemory objectiveMemory,
            AITeamCoordinator teamCoordinator,
            AICommandSource commandSource)
        {
            _brawler = brawler;
            _profile = profile;
            _navAgent = navAgent;
            _abilityDecider = abilityDecider;
            _superDecider = superDecider;
            _objectiveMemory = objectiveMemory;
            _teamCoordinator = teamCoordinator;
            _spacingUtility = new AISpacingUtility(brawler);
            _commandSource = commandSource;
        }

        public void Execute(
            AIActionType actionType,
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float idealRange,
            float superRange)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    RunApproach(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.HoldRange:
                    RunHoldRange(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.Reposition:
                    RunReposition(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.Retreat:
                    RunRetreat(targetInfo);
                    break;

                case AIActionType.Search:
                    RunSearch(targetInfo, currentTick);
                    break;

                case AIActionType.Wander:
                    RunFallbackWander(currentTick);
                    break;

                case AIActionType.UseSuper:
                    RunUseSuper(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.Regroup:
                    RunRegroup(currentTick);
                    break;

                case AIActionType.Peel:
                    RunPeel(currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.Objective:
                    RunObjective(targetInfo);
                    break;

                default:
                    _navAgent.Stop();
                    break;
            }
        }

        private void RunObjective(AITargetInfo targetInfo)
        {
            // Combat always overrides objective movement.
            if (targetInfo.HasLiveTarget)
            {
                _hasObjectiveDebug = false;
                _navAgent.Stop();
                return;
            }

            if (_objectiveMemory == null)
            {
                _hasObjectiveDebug = false;
                _navAgent.Stop();
                return;
            }

            var objective = _objectiveMemory.GetBestObjective(
                _brawler.Position,
                _profile.PreferredObjective);

            if (objective == null)
            {
                _hasObjectiveDebug = false;
                _navAgent.Stop();
                return;
            }

            Vector3 objectivePosition = objective.transform.position;

            Vector3 slotPosition = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                _brawler,
                _profile,
                objectivePosition);

            _lastObjectiveCenter = objectivePosition;
            _lastObjectiveSlot = slotPosition;
            _lastObjectiveDestination = slotPosition;
            _lastObjectiveName = objective.name;
            _hasObjectiveDebug = true;

            _navAgent.RequestDestination(
                slotPosition,
                1f);
        }

        private void RunApproach(
       AITargetInfo targetInfo,
       uint currentTick,
       float attackRange,
       float idealRange,
       float superRange)
        {
            if (!targetInfo.HasLiveTarget || targetInfo.Target == null)
            {
                _lastTacticalMovementIntent = AITacticalMovementIntent.None;
                _navAgent.Stop();
                return;
            }

            // Even while approaching, try abilities if the target becomes valid.
            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);

            if (CanRetargetTacticalMove(currentTick) || !_navAgent.HasDestination)
            {
                Vector3 destination = BuildTacticalApproachDestination(
                    targetInfo,
                    currentTick,
                    idealRange);

                _navAgent.RequestDestination(destination, 0.5f);
            }
            else
            {
                _navAgent.RequestDestination(_lastTacticalMoveDestination, 0.5f);
            }
        }

        private void RunHoldRange(
     AITargetInfo targetInfo,
     uint currentTick,
     float attackRange,
     float idealRange,
     float superRange)
        {
            if (!targetInfo.HasLiveTarget || targetInfo.Target == null)
            {
                _lastTacticalMovementIntent = AITacticalMovementIntent.None;
                _navAgent.Stop();
                return;
            }

            // Combat usage stays the same.
            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);

            // If strafe is disabled, still use your existing preferred-range logic.
            if (!_profile.UseStrafe)
            {
                float preferredRange =
                    GetTacticalPreferredRange(idealRange) +
                    _profile.PreferredCombatOffset;

                Vector3 preferredPoint = _spacingUtility.GetPreferredRangePosition(
                    targetInfo.Target.Position,
                    preferredRange,
                    _profile.AllyAvoidanceRadius,
                    _profile.AllyAvoidanceWeight);

                _lastTacticalMovementIntent = AITacticalMovementIntent.HoldPosition;
                _lastTacticalMoveDestination = preferredPoint;

                _navAgent.RequestDestination(preferredPoint, 0.5f);
                return;
            }

            // Tactical retargeting:
            // Do not pick a new micro-position every frame.
            if (CanRetargetTacticalMove(currentTick) || !_navAgent.HasDestination)
            {
                Vector3 tacticalDestination = BuildTacticalCombatDestination(
                    targetInfo,
                    currentTick,
                    idealRange);

                _navAgent.RequestDestination(tacticalDestination, 0.4f);
            }
            else
            {
                _navAgent.RequestDestination(_lastTacticalMoveDestination, 0.4f);
            }
        }

        private void RunReposition(
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float idealRange,
            float superRange)
        {
            if (!targetInfo.HasLiveTarget || targetInfo.Target == null)
            {
                _lastTacticalMovementIntent = AITacticalMovementIntent.None;
                _navAgent.Stop();
                return;
            }

            // Reposition is still a combat action.
            // The bot should keep attacking/using abilities while moving to a better angle.
            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);

            if (CanRetargetTacticalMove(currentTick) || !_navAgent.HasDestination)
            {
                Vector3 destination = BuildTacticalRepositionDestination(
                    targetInfo,
                    currentTick,
                    idealRange);

                _navAgent.RequestDestination(destination, 0.45f);
            }
            else
            {
                _navAgent.RequestDestination(_lastTacticalMoveDestination, 0.45f);
            }
        }

        private void RunRetreat(AITargetInfo targetInfo)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            Vector3 retreatPoint = _spacingUtility.GetRetreatPosition(
                targetInfo.Target.Position,
                _profile.RetreatStepDistance,
                _profile.AllyAvoidanceRadius,
                _profile.AllyAvoidanceWeight);

            _navAgent.RequestDestination(retreatPoint, 0.5f);
        }

        private void RunSearch(AITargetInfo targetInfo, uint currentTick)
        {
            if (targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
            {
                _navAgent.RequestDestination(targetInfo.LastKnownPosition, 1.0f);
                return;
            }

            if (AITeamMemory.TryGetRecentHotspot(
                _brawler.Team,
                currentTick,
                _profile.SharedHotspotMemoryTicks,
                out var destination))
            {
                _navAgent.RequestDestination(destination, 1.0f);
                return;
            }

            if (_objectiveMemory != null)
            {
                var objective = _objectiveMemory.GetBestObjective(
                    _brawler.Position,
                    _profile.PreferredObjective);

                if (objective != null)
                {
                    _navAgent.RequestDestination(
                        objective.transform.position,
                        1.0f);

                    return;
                }
            }
            _navAgent.Stop();
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

        private void RunUseSuper(AITargetInfo targetInfo, uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _navAgent.Stop();
                return;
            }

            float preferredRange = _profile.GetPreferredAttackRange(idealRange) + _profile.PreferredCombatOffset;

            Vector3 destination = _spacingUtility.GetPreferredRangePosition(
                targetInfo.Target.Position,
                preferredRange,
                _profile.AllyAvoidanceRadius,
                _profile.AllyAvoidanceWeight);

            _navAgent.RequestDestination(destination, 0.8f);

            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
        }

        private void RunRegroup(uint currentTick)
        {
            if (_teamCoordinator != null && _teamCoordinator.TryGetRegroupPoint(currentTick, out var point))
            {
                _navAgent.RequestDestination(point, 1.0f);
                return;
            }

            _navAgent.Stop();
        }

        private void RunPeel(uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (_teamCoordinator == null || !_teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) || ally == null)
            {
                _navAgent.Stop();
                return;
            }

            if (ally.State != null && ally.State.ThreatTracker != null)
            {
                int attackerId = ally.State.ThreatTracker.GetHighestThreatTarget(currentTick, 240);

                if (attackerId != 0)
                {
                    var entity = CombatRegistry.GetEntity(attackerId);

                    if (entity is BrawlerController attacker && attacker.State != null && !attacker.State.IsDead)
                    {
                        float preferredRange = _profile.GetPreferredAttackRange(idealRange) + _profile.PreferredCombatOffset;

                        Vector3 destination = _spacingUtility.GetPreferredRangePosition(
                            attacker.Position,
                            preferredRange,
                            _profile.AllyAvoidanceRadius,
                            _profile.AllyAvoidanceWeight);

                        _navAgent.RequestDestination(destination, 1.0f);
                        _abilityDecider.TryUseMainAttack(attacker, currentTick, attackRange);
                        _superDecider.TryUseSuper(attacker, currentTick, superRange);
                        return;
                    }
                }
            }

            _navAgent.RequestDestination(ally.Position, 1.0f);
        }

        private bool CanRetargetTacticalMove(uint currentTick)
        {
            return currentTick >= _nextTacticalMoveRetargetTick;
        }

        private void CommitTacticalMove(
     AITacticalMovementIntent intent,
     Vector3 destination,
     uint currentTick,
     string reason = "")
        {
            _lastTacticalMovementIntent = intent;
            _lastTacticalMoveDestination = destination;
            _lastTacticalRetargetTick = currentTick;
            _lastTacticalMoveReason = reason;

            uint retargetTicks = _profile.TacticalMoveRetargetTicks == 0
                ? 1u
                : _profile.TacticalMoveRetargetTicks;

            _nextTacticalMoveRetargetTick = currentTick + retargetTicks;

            if (_profile.LogTacticalMovement)
            {
                Debug.Log(
                    $"[AITacticalMove-{_brawler.name}] " +
                    $"Intent={intent} " +
                    $"Dest={destination} " +
                    $"Reason={reason} " +
                    $"NextRetarget={_nextTacticalMoveRetargetTick}");
            }
        }

        private bool IsFragileArchetype()
        {
            return _profile.Archetype == BrawlerArchetype.Sniper ||
                   _profile.Archetype == BrawlerArchetype.Support ||
                   _profile.Archetype == BrawlerArchetype.Artillery;
        }

        private float GetTacticalPreferredRange(float idealRange)
        {
            float preferred = _profile.GetPreferredAttackRange(idealRange);

            if (IsFragileArchetype())
                preferred += _profile.FragileRangePadding;

            return preferred;
        }

        private int GetStableStrafeSide()
        {
            // Keep side stable, but split bots by identity.
            if (_lastStrafeSide == 0)
                _lastStrafeSide = (_brawler.EntityID % 2u == 0u) ? 1 : -1;

            return _lastStrafeSide;
        }

        private Vector3 BuildTacticalCombatDestination(
    AITargetInfo targetInfo,
    uint currentTick,
    float idealRange)
        {
            if (!targetInfo.HasLiveTarget || targetInfo.Target == null)
                return _brawler.Position;

            Vector3 selfPos = _brawler.Position;
            Vector3 targetPos = targetInfo.Target.Position;

            Vector3 awayFromTarget = selfPos - targetPos;
            awayFromTarget.y = 0f;

            if (awayFromTarget.sqrMagnitude < 0.01f)
                awayFromTarget = -_brawler.transform.forward;

            awayFromTarget.Normalize();

            Vector3 toTarget = -awayFromTarget;
            Vector3 side = new Vector3(toTarget.z, 0f, -toTarget.x) * GetStableStrafeSide();

            float dist = Vector3.Distance(selfPos, targetPos);
            float tooCloseDistance = _profile.GetTooCloseDistance(idealRange);
            float preferredRange =
    GetTacticalPreferredRange(idealRange) +
    _profile.PreferredCombatOffset;

            _lastTacticalTargetDistance = dist;
            _lastTacticalPreferredRange = preferredRange;
            _lastTacticalTooCloseDistance = tooCloseDistance;

            AITacticalMovementIntent intent;
            Vector3 destination;

            if (dist < tooCloseDistance || dist < preferredRange * 0.65f)
            {
                intent = AITacticalMovementIntent.Kite;
                destination = selfPos + awayFromTarget * _profile.TacticalKiteDistance;

                CommitTacticalMove(
                    intent,
                    destination,
                    currentTick,
                    $"combat_too_close dist={dist:0.0} preferred={preferredRange:0.0}");
            }
            else if (dist < preferredRange)
            {
                intent = AITacticalMovementIntent.RepositionAngle;
                destination =
                    selfPos +
                    awayFromTarget * (_profile.TacticalKiteDistance * 0.5f) +
                    side * (_profile.TacticalStrafeDistance * 0.75f);

                CommitTacticalMove(
                    intent,
                    destination,
                    currentTick,
                    $"combat_inside_preferred dist={dist:0.0} preferred={preferredRange:0.0}");
            }
            else
            {
                intent = AITacticalMovementIntent.Strafe;
                destination =
                    selfPos +
                    side * _profile.TacticalStrafeDistance;

                CommitTacticalMove(
                    intent,
                    destination,
                    currentTick,
                    $"combat_good_range dist={dist:0.0} preferred={preferredRange:0.0}");
            }

            return destination;
        }

        private Vector3 BuildTacticalRepositionDestination(
      AITargetInfo targetInfo,
      uint currentTick,
      float idealRange)
        {
            if (!targetInfo.HasLiveTarget || targetInfo.Target == null)
                return _brawler.Position;

            Vector3 selfPos = _brawler.Position;
            Vector3 targetPos = targetInfo.Target.Position;

            Vector3 awayFromTarget = selfPos - targetPos;
            awayFromTarget.y = 0f;

            if (awayFromTarget.sqrMagnitude < 0.01f)
                awayFromTarget = -_brawler.transform.forward;

            awayFromTarget.Normalize();

            Vector3 toTarget = -awayFromTarget;
            Vector3 side = new Vector3(toTarget.z, 0f, -toTarget.x) * GetStableStrafeSide();

            float dist = Vector3.Distance(selfPos, targetPos);

            float preferredRange =
                GetTacticalPreferredRange(idealRange) +
                _profile.PreferredCombatOffset;

            float tooCloseDistance = _profile.GetTooCloseDistance(idealRange);

            _lastTacticalTargetDistance = dist;
            _lastTacticalPreferredRange = preferredRange;
            _lastTacticalTooCloseDistance = tooCloseDistance;

            AITacticalMovementIntent intent;
            Vector3 destination;

            if (IsFragileArchetype())
            {
                // Sniper / Support / Artillery:
                // create safer distance while also changing angle.
                intent = AITacticalMovementIntent.RepositionAngle;

                destination =
                    selfPos +
                    awayFromTarget * (_profile.TacticalKiteDistance * 0.9f) +
                    side * (_profile.TacticalStrafeDistance * 0.8f);
            }
            else if (_profile.Archetype == BrawlerArchetype.Tank)
            {
                // Tank:
                // do not run too far away; mostly side-step while keeping pressure.
                intent = AITacticalMovementIntent.RepositionAngle;

                destination =
                    selfPos +
                    awayFromTarget * (_profile.TacticalKiteDistance * 0.25f) +
                    side * (_profile.TacticalStrafeDistance * 0.8f);
            }
            else if (_profile.Archetype == BrawlerArchetype.Assassin)
            {
                // Assassin:
                // prefer side/flank angle instead of backing off.
                intent = AITacticalMovementIntent.RepositionAngle;

                destination =
                    targetPos -
                    awayFromTarget * Mathf.Max(1.2f, preferredRange * 0.65f) +
                    side * (_profile.TacticalStrafeDistance * 1.4f);
            }
            else
            {
                // Fighter / Controller:
                // balanced back-side reposition.
                intent = AITacticalMovementIntent.RepositionAngle;

                destination =
                    selfPos +
                    awayFromTarget * (_profile.TacticalKiteDistance * 0.55f) +
                    side * _profile.TacticalStrafeDistance;
            }

            CommitTacticalMove(
                intent,
                destination,
                currentTick,
                $"reposition_{_profile.Archetype} dist={dist:0.0} preferred={preferredRange:0.0} tooClose={tooCloseDistance:0.0}");

            return destination;
        }

        private Vector3 BuildTacticalApproachDestination(
      AITargetInfo targetInfo,
      uint currentTick,
      float idealRange)
        {
            if (!targetInfo.HasLiveTarget || targetInfo.Target == null)
                return _brawler.Position;

            Vector3 selfPos = _brawler.Position;
            Vector3 targetPos = targetInfo.Target.Position;

            Vector3 awayFromTarget = selfPos - targetPos;
            awayFromTarget.y = 0f;

            if (awayFromTarget.sqrMagnitude < 0.01f)
                awayFromTarget = -_brawler.transform.forward;

            awayFromTarget.Normalize();

            Vector3 toTarget = -awayFromTarget;
            Vector3 side = new Vector3(toTarget.z, 0f, -toTarget.x) * GetStableStrafeSide();

            float dist = Vector3.Distance(selfPos, targetPos);

            float preferredRange =
                GetTacticalPreferredRange(idealRange) +
                _profile.PreferredCombatOffset;

            float tooCloseDistance = _profile.GetTooCloseDistance(idealRange);

            _lastTacticalTargetDistance = dist;
            _lastTacticalPreferredRange = preferredRange;
            _lastTacticalTooCloseDistance = tooCloseDistance;

            AITacticalMovementIntent intent;
            Vector3 destination;

            if (_profile.Archetype == BrawlerArchetype.Assassin)
            {
                // Assassin:
                // approach from an angle, not straight down the middle.
                intent = AITacticalMovementIntent.CloseGap;

                destination =
                    targetPos -
                    awayFromTarget * Mathf.Max(1.0f, preferredRange * 0.7f) +
                    side * (_profile.TacticalStrafeDistance * 1.5f);
            }
            else if (_profile.Archetype == BrawlerArchetype.Tank)
            {
                // Tank:
                // more direct pressure, but still slightly angled.
                intent = AITacticalMovementIntent.CloseGap;

                destination =
                    targetPos -
                    awayFromTarget * Mathf.Max(0.8f, preferredRange * 0.55f) +
                    side * (_profile.TacticalStrafeDistance * 0.35f);
            }
            else if (IsFragileArchetype())
            {
                // Sniper / Support / Artillery:
                // approach only to a safer outer range and use a side angle.
                intent = AITacticalMovementIntent.CloseGap;

                destination =
                    targetPos -
                    awayFromTarget * preferredRange +
                    side * (_profile.TacticalStrafeDistance * 0.9f);
            }
            else
            {
                // Fighter / Controller:
                // balanced angled approach.
                intent = AITacticalMovementIntent.CloseGap;

                destination =
                    targetPos -
                    awayFromTarget * Mathf.Max(1.0f, preferredRange * 0.8f) +
                    side * (_profile.TacticalStrafeDistance * 0.75f);
            }

            CommitTacticalMove(
                intent,
                destination,
                currentTick,
                $"approach_{_profile.Archetype} dist={dist:0.0} preferred={preferredRange:0.0} tooClose={tooCloseDistance:0.0}");

            return destination;
        }
    }
}