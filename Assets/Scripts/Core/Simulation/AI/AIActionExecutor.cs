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
        private readonly AIDangerMemory _dangerMemory;
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
        private Vector3 _lastTacticalTargetPosition;
        private uint _nextTacticalMoveRetargetTick;
        private int _lastStrafeSide = 1;
        public AITacticalMovementIntent LastTacticalMovementIntent => _lastTacticalMovementIntent;
        public Vector3 LastTacticalMoveDestination => _lastTacticalMoveDestination;

        private float _lastTacticalTargetDistance;
        private float _lastTacticalPreferredRange;
        private float _lastTacticalTooCloseDistance;
        private uint _lastTacticalRetargetTick;
        private string _lastTacticalMoveReason;
        private string _pendingTacticalRefreshReason;
        private string _lastMapRouteDebug;
        private Vector3 _lastRawMapDestination;
        private Vector3 _lastResolvedMapDestination;
        private AIMapRouteIntent _lastMapRequestIntent;
        private Vector3 _lastMapRequestThreatPosition;
        private float _lastMapRequestPreferredThreatDistance;
        private bool _lastMapRequestHadThreatPosition;
        private bool _hasMapRouteCache;
        private uint _currentExecuteTick;

        public float LastTacticalTargetDistance => _lastTacticalTargetDistance;
        public float LastTacticalPreferredRange => _lastTacticalPreferredRange;
        public float LastTacticalTooCloseDistance => _lastTacticalTooCloseDistance;
        public uint LastTacticalRetargetTick => _lastTacticalRetargetTick;
        public uint NextTacticalMoveRetargetTick => _nextTacticalMoveRetargetTick;
        public string LastTacticalMoveReason => _lastTacticalMoveReason;
        public string LastMapRouteDebug => _lastMapRouteDebug;
        public Vector3 LastRawMapDestination => _lastRawMapDestination;
        public Vector3 LastResolvedMapDestination => _lastResolvedMapDestination;

        public AIActionExecutor(
            BrawlerController brawler,
            BrawlerAIProfile profile,
            NavigationAgent navAgent,
            AIAbilityDecider abilityDecider,
            AISuperDecider superDecider,
            AIObjectiveMemory objectiveMemory,
            AITeamCoordinator teamCoordinator,
            AICommandSource commandSource,
            AIDangerMemory dangerMemory = null)
        {
            _brawler = brawler;
            _profile = profile;
            _navAgent = navAgent;
            _abilityDecider = abilityDecider;
            _superDecider = superDecider;
            _objectiveMemory = objectiveMemory;
            _teamCoordinator = teamCoordinator;
            _dangerMemory = dangerMemory;
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
            _currentExecuteTick = currentTick;

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

                case AIActionType.Evade:
                    RunEvade(targetInfo, currentTick, attackRange, superRange);
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

        private void RequestMapAwareDestination(
            Vector3 destination,
            float arrivalDistance,
            AIMapRouteIntent routeIntent,
            bool hasThreatPosition = false,
            Vector3 threatPosition = default,
            float preferredThreatDistance = 0f)
        {
            Vector3 resolvedDestination = ResolveMapAwareDestination(
                destination,
                routeIntent,
                hasThreatPosition,
                threatPosition,
                preferredThreatDistance);

            _navAgent.RequestDestination(resolvedDestination, arrivalDistance);
        }

        private Vector3 ResolveMapAwareDestination(
            Vector3 destination,
            AIMapRouteIntent routeIntent,
            bool hasThreatPosition = false,
            Vector3 threatPosition = default,
            float preferredThreatDistance = 0f)
        {
            if (CanReuseMapResolvedDestination(
                destination,
                routeIntent,
                hasThreatPosition,
                threatPosition,
                preferredThreatDistance))
            {
                _lastMapRouteDebug =
                    $"Route={routeIntent} " +
                    $"Raw={FormatVector(destination)} " +
                    $"Resolved={FormatVector(_lastResolvedMapDestination)} " +
                    $"Score=0.0 " +
                    $"Reason=cached";

                AIPerformanceTracker.RecordMapResolve(
                    _currentExecuteTick,
                    routeIntent,
                    true,
                    0,
                    0);

                return _lastResolvedMapDestination;
            }

            AIMapNavigationRequest request = BuildMapNavigationRequest(
                destination,
                routeIntent,
                hasThreatPosition,
                threatPosition,
                preferredThreatDistance);

            Vector3 resolvedDestination = AIMapNavigationUtility.ResolveDestination(
                _brawler,
                _profile,
                request,
                out AIMapNavigationDecision decision);

            _lastRawMapDestination = decision.RawDestination;
            _lastResolvedMapDestination = decision.ResolvedDestination;
            _lastMapRequestIntent = routeIntent;
            _lastMapRequestThreatPosition = threatPosition;
            _lastMapRequestPreferredThreatDistance = preferredThreatDistance;
            _lastMapRequestHadThreatPosition = hasThreatPosition;
            _hasMapRouteCache = true;
            _lastMapRouteDebug =
                $"Route={decision.Intent} " +
                $"Raw={FormatVector(decision.RawDestination)} " +
                $"Resolved={FormatVector(decision.ResolvedDestination)} " +
                $"Score={decision.Score:0.0} " +
                $"Reason={decision.Reason} " +
                $"Candidates={decision.CandidateCount} " +
                $"Validations={decision.PathValidationCount}";

            AIPerformanceTracker.RecordMapResolve(
                _currentExecuteTick,
                routeIntent,
                false,
                decision.CandidateCount,
                decision.PathValidationCount);

            if (_profile.LogMapIntelligence)
            {
                Debug.Log($"[AIMap-{_brawler.name}] {_lastMapRouteDebug}");
            }

            return resolvedDestination;
        }

        private bool CanReuseMapResolvedDestination(
            Vector3 destination,
            AIMapRouteIntent routeIntent,
            bool hasThreatPosition,
            Vector3 threatPosition,
            float preferredThreatDistance)
        {
            if (!_hasMapRouteCache || routeIntent == AIMapRouteIntent.None)
                return false;

            if (_lastMapRequestIntent != routeIntent ||
                _lastMapRequestHadThreatPosition != hasThreatPosition)
            {
                return false;
            }

            float destinationTolerance = Mathf.Max(0.5f, _profile.TacticalDestinationStaleDistance * 0.5f);
            if ((destination - _lastRawMapDestination).sqrMagnitude > destinationTolerance * destinationTolerance)
                return false;

            if (hasThreatPosition)
            {
                float threatTolerance = Mathf.Max(0.5f, destinationTolerance);
                if ((threatPosition - _lastMapRequestThreatPosition).sqrMagnitude > threatTolerance * threatTolerance)
                    return false;

                if (Mathf.Abs(preferredThreatDistance - _lastMapRequestPreferredThreatDistance) > 0.25f)
                    return false;
            }

            return true;
        }

        private AIMapNavigationRequest BuildMapNavigationRequest(
            Vector3 destination,
            AIMapRouteIntent routeIntent,
            bool hasThreatPosition,
            Vector3 threatPosition,
            float preferredThreatDistance)
        {
            bool fragile = AIMapNavigationUtility.IsFragileArchetype(_profile.Archetype);
            bool combatRoute =
                routeIntent == AIMapRouteIntent.CombatAdvance ||
                routeIntent == AIMapRouteIntent.CombatReposition ||
                routeIntent == AIMapRouteIntent.CombatRetreat ||
                routeIntent == AIMapRouteIntent.Evade ||
                routeIntent == AIMapRouteIntent.Peel;

            bool stealthRoute =
                routeIntent == AIMapRouteIntent.Search ||
                routeIntent == AIMapRouteIntent.Objective ||
                routeIntent == AIMapRouteIntent.Wander ||
                _profile.Archetype == BrawlerArchetype.Assassin;

            bool defensiveRoute =
                routeIntent == AIMapRouteIntent.CombatRetreat ||
                routeIntent == AIMapRouteIntent.Evade ||
                routeIntent == AIMapRouteIntent.Regroup ||
                routeIntent == AIMapRouteIntent.Objective ||
                routeIntent == AIMapRouteIntent.Search;

            bool artilleryCoverAngle =
                _profile.Archetype == BrawlerArchetype.Artillery &&
                routeIntent == AIMapRouteIntent.CombatReposition;

            bool directFireRoute =
                _profile.Archetype != BrawlerArchetype.Artillery &&
                routeIntent != AIMapRouteIntent.CombatRetreat &&
                routeIntent != AIMapRouteIntent.Evade &&
                (routeIntent == AIMapRouteIntent.CombatAdvance ||
                 routeIntent == AIMapRouteIntent.CombatReposition ||
                 routeIntent == AIMapRouteIntent.Peel);

            return new AIMapNavigationRequest
            {
                DesiredDestination = destination,
                Intent = routeIntent,
                HasThreatPosition = hasThreatPosition,
                ThreatPosition = threatPosition,
                PreferredThreatDistance = preferredThreatDistance,
                PreferBush = stealthRoute || fragile,
                PreferCover = combatRoute || fragile || _profile.Archetype == BrawlerArchetype.Controller,
                PreferLineOfSightCover = hasThreatPosition && (defensiveRoute || artilleryCoverAngle),
                PreferOpenShot = hasThreatPosition && directFireRoute,
                AvoidChokepoints = fragile ||
                                   routeIntent == AIMapRouteIntent.CombatRetreat ||
                                   routeIntent == AIMapRouteIntent.Evade ||
                                   routeIntent == AIMapRouteIntent.Search ||
                                   routeIntent == AIMapRouteIntent.Objective ||
                                   routeIntent == AIMapRouteIntent.Regroup,
                PreferFlank = routeIntent != AIMapRouteIntent.Evade &&
                              (routeIntent == AIMapRouteIntent.CombatAdvance ||
                               routeIntent == AIMapRouteIntent.CombatReposition ||
                               _profile.Archetype == BrawlerArchetype.Assassin),
                SearchRadius = routeIntent == AIMapRouteIntent.Evade
                    ? Mathf.Min(_profile.MapDestinationSearchRadius, _profile.DangerMapSearchRadius)
                    : _profile.MapDestinationSearchRadius,
                BushWeight = _profile.MapBushPreference,
                CoverWeight = _profile.MapCoverPreference,
                LineOfSightCoverWeight = _profile.MapLineOfSightCoverPreference,
                ExposedPositionPenalty = _profile.MapExposedPositionPenalty,
                OpenShotWeight = _profile.MapOpenShotPreference,
                ChokepointPenalty = _profile.MapChokepointPenalty,
                ThreatWeight = _profile.MapThreatAvoidanceWeight,
                PathCostWeight = _profile.MapPathCostWeight
            };
        }

        private string FormatVector(Vector3 value)
        {
            return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
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

            Vector3 destination = ResolveMapAwareDestination(
                slotPosition,
                AIMapRouteIntent.Objective);

            _lastObjectiveCenter = objectivePosition;
            _lastObjectiveSlot = slotPosition;
            _lastObjectiveDestination = destination;
            _lastObjectiveName = objective.name;
            _hasObjectiveDebug = true;

            _navAgent.RequestDestination(destination, 1f);
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

            if (ShouldRefreshTacticalMove(targetInfo, currentTick, out string refreshReason))
            {
                BeginTacticalRefresh(refreshReason);

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
                if (ShouldRefreshTacticalMove(targetInfo, currentTick, out string holdRefreshReason))
                {
                    BeginTacticalRefresh(holdRefreshReason);

                    float preferredRange =
                        GetTacticalPreferredRange(idealRange) +
                        _profile.PreferredCombatOffset;

                    Vector3 preferredPoint = _spacingUtility.GetPreferredRangePosition(
                        targetInfo.Target.Position,
                        preferredRange,
                        _profile.AllyAvoidanceRadius,
                        _profile.AllyAvoidanceWeight);

                    _lastTacticalTargetPosition = targetInfo.Target.Position;
                    _lastTacticalTargetDistance = Vector3.Distance(_brawler.Position, targetInfo.Target.Position);
                    _lastTacticalPreferredRange = preferredRange;
                    _lastTacticalTooCloseDistance = _profile.GetTooCloseDistance(idealRange);

                    Vector3 destination = CommitTacticalMove(
                        AITacticalMovementIntent.HoldPosition,
                        preferredPoint,
                        currentTick,
                        "hold_no_strafe");

                    _navAgent.RequestDestination(destination, 0.5f);
                }
                else
                {
                    _navAgent.RequestDestination(_lastTacticalMoveDestination, 0.5f);
                }

                return;
            }

            // Tactical retargeting:
            // Do not pick a new micro-position every frame.
            if (ShouldRefreshTacticalMove(targetInfo, currentTick, out string refreshReason))
            {
                BeginTacticalRefresh(refreshReason);

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

            if (ShouldRefreshTacticalMove(targetInfo, currentTick, out string refreshReason))
            {
                BeginTacticalRefresh(refreshReason);

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

            RequestMapAwareDestination(
                retreatPoint,
                0.5f,
                AIMapRouteIntent.CombatRetreat,
                true,
                targetInfo.Target.Position,
                GetTacticalPreferredRange(GetAbilityIdealRange()));
        }

        private void RunEvade(
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float superRange)
        {
            if (_dangerMemory == null || !_dangerMemory.HasDanger)
            {
                _navAgent.Stop();
                return;
            }

            if (targetInfo.HasLiveTarget && targetInfo.Target != null)
            {
                _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
                _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
                _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
            }

            if (!ShouldRefreshEvadeMove(currentTick, out string refreshReason))
            {
                _navAgent.RequestDestination(_lastTacticalMoveDestination, 0.4f);
                return;
            }

            _pendingTacticalRefreshReason = refreshReason;
            _lastTacticalTargetPosition = _dangerMemory.ThreatPosition;
            _lastTacticalTargetDistance = Vector3.Distance(_brawler.Position, _dangerMemory.ThreatPosition);
            _lastTacticalPreferredRange = _profile.DangerEvadeDistance;
            _lastTacticalTooCloseDistance = Mathf.Max(0.5f, _profile.DangerEvadeDistance * 0.5f);

            Vector3 destination = _dangerMemory.GetEvadeDestination(
                _brawler.Position,
                _profile.DangerEvadeDistance);

            destination = EnsureMeaningfulTacticalDestination(
                destination,
                AITacticalMovementIntent.EmergencyRetreat);

            Vector3 resolvedDestination = ResolveMapAwareDestination(
                destination,
                AIMapRouteIntent.Evade,
                true,
                _dangerMemory.ThreatPosition,
                _profile.DangerEvadeDistance);

            _lastTacticalMovementIntent = AITacticalMovementIntent.EmergencyRetreat;
            _lastTacticalMoveDestination = resolvedDestination;
            _lastTacticalRetargetTick = currentTick;
            _lastTacticalMoveReason = string.IsNullOrEmpty(_pendingTacticalRefreshReason)
                ? "danger_evade"
                : $"danger_evade|{_pendingTacticalRefreshReason}";
            _pendingTacticalRefreshReason = string.Empty;

            uint retargetTicks = _profile.DangerEvadeRetargetTicks == 0
                ? 1u
                : _profile.DangerEvadeRetargetTicks;

            _nextTacticalMoveRetargetTick = currentTick + retargetTicks;

            _navAgent.RequestDestination(resolvedDestination, 0.4f);
        }

        private bool ShouldRefreshEvadeMove(uint currentTick, out string refreshReason)
        {
            refreshReason = string.Empty;

            if (!_navAgent.HasDestination)
            {
                refreshReason = "missing_destination";
                return true;
            }

            if (_lastTacticalMovementIntent != AITacticalMovementIntent.EmergencyRetreat ||
                string.IsNullOrEmpty(_lastTacticalMoveReason) ||
                !_lastTacticalMoveReason.StartsWith("danger_evade"))
            {
                refreshReason = "new_evade";
                return true;
            }

            uint retargetTicks = _profile.DangerEvadeRetargetTicks == 0u
                ? 1u
                : _profile.DangerEvadeRetargetTicks;

            if ((currentTick - _lastTacticalRetargetTick) >= retargetTicks)
            {
                refreshReason = "evade_retarget_window";
                return true;
            }

            float staleDistance = Mathf.Max(0.25f, _profile.DangerThreatStaleDistance);
            float threatMoveSq = (_dangerMemory.ThreatPosition - _lastTacticalTargetPosition).sqrMagnitude;
            if (threatMoveSq >= staleDistance * staleDistance)
            {
                refreshReason = $"threat_moved_{Mathf.Sqrt(threatMoveSq):0.0}";
                return true;
            }

            uint heartbeatTicks = _profile.TacticalMoveHeartbeatTicks == 0u
                ? 1u
                : _profile.TacticalMoveHeartbeatTicks;

            if ((currentTick - _lastTacticalRetargetTick) >= heartbeatTicks &&
                IsNearLastTacticalDestination())
            {
                refreshReason = "evade_heartbeat";
                return true;
            }

            return false;
        }

        private void RunSearch(AITargetInfo targetInfo, uint currentTick)
        {
            if (targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
            {
                RequestMapAwareDestination(
                    targetInfo.LastKnownPosition,
                    1.0f,
                    AIMapRouteIntent.Search);
                return;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetThreatCenter(currentTick, out var threatCenter, out _))
            {
                RequestMapAwareDestination(
                    threatCenter,
                    1.0f,
                    AIMapRouteIntent.Search,
                    true,
                    threatCenter,
                    GetTacticalPreferredRange(GetAbilityIdealRange()));
                return;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetEnemyHotspot(currentTick, out var enemyHotspot, out _))
            {
                RequestMapAwareDestination(
                    enemyHotspot,
                    1.0f,
                    AIMapRouteIntent.Search,
                    true,
                    enemyHotspot,
                    GetTacticalPreferredRange(GetAbilityIdealRange()));
                return;
            }

            if (AITeamMemory.TryGetRecentHotspot(
                _brawler.Team,
                currentTick,
                _profile.SharedHotspotMemoryTicks,
                out var destination))
            {
                RequestMapAwareDestination(
                    destination,
                    1.0f,
                    AIMapRouteIntent.Search);
                return;
            }

            if (_objectiveMemory != null)
            {
                var objective = _objectiveMemory.GetBestObjective(
                    _brawler.Position,
                    _profile.PreferredObjective);

                if (objective != null)
                {
                    RequestMapAwareDestination(
                        objective.transform.position,
                        1.0f,
                        AIMapRouteIntent.Objective);

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
                RequestMapAwareDestination(
                    _fallbackWanderPoint,
                    0.5f,
                    AIMapRouteIntent.Wander);
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

            RequestMapAwareDestination(
                destination,
                0.8f,
                AIMapRouteIntent.CombatReposition,
                true,
                targetInfo.Target.Position,
                preferredRange);

            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
        }

        private void RunRegroup(uint currentTick)
        {
            if (_teamCoordinator != null && _teamCoordinator.TryGetRegroupPoint(currentTick, out var point))
            {
                RequestMapAwareDestination(
                    point,
                    1.0f,
                    AIMapRouteIntent.Regroup);
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

                        RequestMapAwareDestination(
                            destination,
                            1.0f,
                            AIMapRouteIntent.Peel,
                            true,
                            attacker.Position,
                            preferredRange);
                        _abilityDecider.TryUseMainAttack(attacker, currentTick, attackRange);
                        _superDecider.TryUseSuper(attacker, currentTick, superRange);
                        return;
                    }
                }
            }

            RequestMapAwareDestination(
                ally.Position,
                1.0f,
                AIMapRouteIntent.Peel);
        }

        private bool ShouldRefreshTacticalMove(
            AITargetInfo targetInfo,
            uint currentTick,
            out string refreshReason)
        {
            refreshReason = string.Empty;

            if (!_navAgent.HasDestination)
            {
                refreshReason = "missing_destination";
                return true;
            }

            if (currentTick >= _nextTacticalMoveRetargetTick)
            {
                refreshReason = "retarget_window";
                return true;
            }

            if (targetInfo.HasLiveTarget && targetInfo.Target != null)
            {
                float staleDistance = Mathf.Max(0.25f, _profile.TacticalDestinationStaleDistance);
                float targetMoveSq = (targetInfo.Target.Position - _lastTacticalTargetPosition).sqrMagnitude;

                if (targetMoveSq >= staleDistance * staleDistance)
                {
                    refreshReason = $"target_moved_{Mathf.Sqrt(targetMoveSq):0.0}";
                    return true;
                }
            }

            uint heartbeatTicks = _profile.TacticalMoveHeartbeatTicks == 0
                ? 1u
                : _profile.TacticalMoveHeartbeatTicks;

            if ((currentTick - _lastTacticalRetargetTick) >= heartbeatTicks &&
                IsNearLastTacticalDestination())
            {
                refreshReason = "movement_heartbeat";
                return true;
            }

            return false;
        }

        private void BeginTacticalRefresh(string refreshReason)
        {
            _pendingTacticalRefreshReason = refreshReason;

            if (refreshReason == "movement_heartbeat" ||
                refreshReason == "missing_destination")
            {
                _lastStrafeSide = -GetStableStrafeSide();
            }
        }

        private Vector3 CommitTacticalMove(
     AITacticalMovementIntent intent,
     Vector3 destination,
     uint currentTick,
     string reason = "")
        {
            destination = EnsureMeaningfulTacticalDestination(destination, intent);
            destination = ResolveMapAwareDestination(
                destination,
                GetMapRouteIntent(intent),
                intent != AITacticalMovementIntent.None,
                _lastTacticalTargetPosition,
                _lastTacticalPreferredRange);

            _lastTacticalMovementIntent = intent;
            _lastTacticalMoveDestination = destination;
            _lastTacticalRetargetTick = currentTick;
            _lastTacticalMoveReason = string.IsNullOrEmpty(_pendingTacticalRefreshReason)
                ? reason
                : $"{reason}|refresh={_pendingTacticalRefreshReason}";
            _pendingTacticalRefreshReason = string.Empty;

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
                    $"Reason={_lastTacticalMoveReason} " +
                    $"NextRetarget={_nextTacticalMoveRetargetTick}");
            }

            return destination;
        }

        private AIMapRouteIntent GetMapRouteIntent(AITacticalMovementIntent intent)
        {
            switch (intent)
            {
                case AITacticalMovementIntent.CloseGap:
                    return AIMapRouteIntent.CombatAdvance;

                case AITacticalMovementIntent.Kite:
                case AITacticalMovementIntent.EmergencyRetreat:
                    return AIMapRouteIntent.CombatRetreat;

                case AITacticalMovementIntent.Strafe:
                case AITacticalMovementIntent.RepositionAngle:
                case AITacticalMovementIntent.HoldPosition:
                    return AIMapRouteIntent.CombatReposition;

                case AITacticalMovementIntent.Regroup:
                    return AIMapRouteIntent.Regroup;

                default:
                    return AIMapRouteIntent.None;
            }
        }

        private bool IsNearLastTacticalDestination()
        {
            float minimumStep = Mathf.Max(0.25f, _profile.TacticalMinimumStepDistance);
            float distSq = (_lastTacticalMoveDestination - _brawler.Position).sqrMagnitude;
            return distSq <= minimumStep * minimumStep;
        }

        private Vector3 EnsureMeaningfulTacticalDestination(
            Vector3 destination,
            AITacticalMovementIntent intent)
        {
            Vector3 offset = destination - _brawler.Position;
            offset.y = 0f;

            float minimumStep = Mathf.Max(0.25f, _profile.TacticalMinimumStepDistance);
            if (offset.sqrMagnitude >= minimumStep * minimumStep)
                return destination;

            Vector3 direction;
            if (intent == AITacticalMovementIntent.Kite ||
                intent == AITacticalMovementIntent.EmergencyRetreat)
            {
                direction = offset.sqrMagnitude > 0.001f
                    ? offset.normalized
                    : -_brawler.transform.forward;
            }
            else
            {
                direction = GetLastTacticalSideDirection();
            }

            Vector3 adjusted = _brawler.Position + direction * minimumStep;
            adjusted.y = _brawler.Position.y;

            AppendPendingTacticalRefreshReason("min_step");

            return adjusted;
        }

        private void AppendPendingTacticalRefreshReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return;

            _pendingTacticalRefreshReason = string.IsNullOrEmpty(_pendingTacticalRefreshReason)
                ? reason
                : $"{_pendingTacticalRefreshReason}+{reason}";
        }

        private Vector3 GetLastTacticalSideDirection()
        {
            Vector3 toTarget = _lastTacticalTargetPosition - _brawler.Position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= 0.001f)
                toTarget = _brawler.transform.forward;

            toTarget.Normalize();

            Vector3 side = new Vector3(toTarget.z, 0f, -toTarget.x) * GetStableStrafeSide();
            return side.sqrMagnitude > 0.001f ? side.normalized : _brawler.transform.right;
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

        private float GetAbilityIdealRange()
        {
            AbilityDefinition attack = _brawler.Definition?.MainAttack;
            return attack != null ? attack.GetAIIdealRange() : 6f;
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
            _lastTacticalTargetPosition = targetPos;

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

                return CommitTacticalMove(
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

                return CommitTacticalMove(
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

                return CommitTacticalMove(
                    intent,
                    destination,
                    currentTick,
                    $"combat_good_range dist={dist:0.0} preferred={preferredRange:0.0}");
            }
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
            _lastTacticalTargetPosition = targetPos;

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

            return CommitTacticalMove(
                intent,
                destination,
                currentTick,
                $"reposition_{_profile.Archetype} dist={dist:0.0} preferred={preferredRange:0.0} tooClose={tooCloseDistance:0.0}");
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
            _lastTacticalTargetPosition = targetPos;

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

            return CommitTacticalMove(
                intent,
                destination,
                currentTick,
                $"approach_{_profile.Archetype} dist={dist:0.0} preferred={preferredRange:0.0} tooClose={tooCloseDistance:0.0}");
        }
    }
}
