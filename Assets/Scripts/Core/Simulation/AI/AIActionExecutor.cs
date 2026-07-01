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
        private readonly AIObjectiveSlotCommitment _objectiveSlotCommitment =
            new AIObjectiveSlotCommitment();

        private uint _nextFallbackWanderTick;
        private Vector3 _fallbackWanderPoint;
        private uint _tacticalStopStartedTick;
        private string _lastTacticalStopDebug = "Stop=None";

        private Vector3 _lastObjectiveCenter;
        private Vector3 _lastObjectiveSlot;
        private Vector3 _lastObjectiveDestination;
        private string _lastObjectiveName;
        private AIObjectiveType _lastObjectiveType;
        private float _lastObjectiveRadius;
        private bool _lastObjectiveIsRuntime;
        private AIObjectiveControlState _lastObjectiveControlState;
        private int _lastObjectiveFriendlyPresence;
        private int _lastObjectiveEnemyPresence;
        private AIObjectiveSlotRole _lastObjectiveSlotRole;
        private AIObjectiveSlotRole _lastObjectiveDesiredSlotRole;
        private bool _hasObjectiveDebug;

        public bool HasObjectiveDebug => _hasObjectiveDebug;
        public Vector3 LastObjectiveCenter => _lastObjectiveCenter;
        public Vector3 LastObjectiveSlot => _lastObjectiveSlot;
        public Vector3 LastObjectiveDestination => _lastObjectiveDestination;
        public string LastObjectiveName => _lastObjectiveName;
        public AIObjectiveType LastObjectiveType => _lastObjectiveType;
        public float LastObjectiveRadius => _lastObjectiveRadius;
        public bool LastObjectiveIsRuntime => _lastObjectiveIsRuntime;
        public AIObjectiveControlState LastObjectiveControlState => _lastObjectiveControlState;
        public int LastObjectiveFriendlyPresence => _lastObjectiveFriendlyPresence;
        public int LastObjectiveEnemyPresence => _lastObjectiveEnemyPresence;
        public AIObjectiveSlotRole LastObjectiveSlotRole => _lastObjectiveSlotRole;
        public AIObjectiveSlotRole LastObjectiveDesiredSlotRole => _lastObjectiveDesiredSlotRole;
        public string LastObjectiveSlotCommitmentDebug =>
            _objectiveSlotCommitment.LastDebugSummary;
        private AITacticalMovementIntent _lastTacticalMovementIntent;
        private Vector3 _lastTacticalMoveDestination;
        private Vector3 _lastTacticalMoveDirection;
        private Vector3 _lastTacticalTargetPosition;
        private uint _nextTacticalMoveRetargetTick;
        private uint _lastTacticalDirectionFlipTick;
        private int _lastStrafeSide = 1;
        private bool _hasTacticalMoveDirection;
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
        public string LastTacticalStopDebug => _lastTacticalStopDebug;
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

            if (TryExecuteBrawlBallIntent(
                    actionType,
                    targetInfo,
                    currentTick,
                    attackRange,
                    idealRange,
                    superRange))
            {
                return;
            }

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
                    RunRetreat(targetInfo, currentTick);
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
                    RunObjective(targetInfo, currentTick, attackRange, idealRange, superRange);
                    break;

                case AIActionType.None:
                    RunTacticalStopOrFallback(currentTick, "none_action", isStopLegal: false);
                    break;

                default:
                    RunTacticalStopOrFallback(currentTick, $"unknown_{actionType}", isStopLegal: false);
                    break;
            }
        }

        public void HandleFailureRecovery(
            AIFailureRecoveryRequest request,
            uint currentTick)
        {
            _hasMapRouteCache = false;
            _nextTacticalMoveRetargetTick = currentTick;
            _lastStrafeSide = -GetStableStrafeSide();
            _hasTacticalMoveDirection = false;

            string reason = $"recovery_{request.Reason}";
            _pendingTacticalRefreshReason = string.IsNullOrEmpty(_pendingTacticalRefreshReason)
                ? reason
                : $"{_pendingTacticalRefreshReason}+{reason}";

            _lastTacticalMoveReason = string.IsNullOrEmpty(_lastTacticalMoveReason)
                ? reason
                : $"{_lastTacticalMoveReason}|{reason}";
        }

        public void HandleIdleHesitation(
            AITargetInfo targetInfo,
            uint currentTick)
        {
            _hasMapRouteCache = false;
            _nextFallbackWanderTick = currentTick;
            _nextTacticalMoveRetargetTick = currentTick;
            _hasTacticalMoveDirection = false;

            _lastTacticalMoveReason = string.IsNullOrEmpty(_lastTacticalMoveReason)
                ? "idle_hesitation"
                : $"{_lastTacticalMoveReason}|idle_hesitation";

            if (targetInfo != null)
            {
                RunSearch(targetInfo, currentTick);
                return;
            }

            RunFallbackWander(currentTick);
        }

        public void HandleNavigationRecoveryFallback(
            AITargetInfo targetInfo,
            uint currentTick,
            AIFailureRecoveryReason reason)
        {
            _hasMapRouteCache = false;
            _nextFallbackWanderTick = currentTick;
            _nextTacticalMoveRetargetTick = currentTick;
            _hasTacticalMoveDirection = false;

            string debugReason = $"recovery_fallback_{reason}";
            _lastTacticalMoveReason = string.IsNullOrEmpty(_lastTacticalMoveReason)
                ? debugReason
                : $"{_lastTacticalMoveReason}|{debugReason}";

            if (targetInfo != null)
            {
                RunSearch(targetInfo, currentTick);
                return;
            }

            RunFallbackWander(currentTick);
        }

        private void RequestMapAwareDestination(
            Vector3 destination,
            float arrivalDistance,
            AIMapRouteIntent routeIntent,
            bool hasThreatPosition = false,
            Vector3 threatPosition = default,
            float preferredThreatDistance = 0f)
        {
            ResetTacticalStop("destination_requested");

            Vector3 resolvedDestination = ResolveMapAwareDestination(
                destination,
                routeIntent,
                hasThreatPosition,
                threatPosition,
                preferredThreatDistance);

            _navAgent.RequestDestination(
                resolvedDestination,
                arrivalDistance,
                IsCriticalRoute(routeIntent));
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

            if (!AIBudgetCoordinator.TryAcquireMapResolve(
                    _currentExecuteTick,
                    _profile,
                    IsCriticalRoute(routeIntent)))
            {
                Vector3 budgetSafeDestination =
                    AIMapNavigationUtility.ResolveBudgetSafeDestination(
                        _profile,
                        destination);

                _lastRawMapDestination = destination;
                _lastResolvedMapDestination = budgetSafeDestination;
                _lastMapRequestIntent = routeIntent;
                _lastMapRequestThreatPosition = threatPosition;
                _lastMapRequestPreferredThreatDistance = preferredThreatDistance;
                _lastMapRequestHadThreatPosition = hasThreatPosition;
                _hasMapRouteCache = false;
                _lastMapRouteDebug =
                    $"Route={routeIntent} " +
                    $"Raw={FormatVector(destination)} " +
                    $"Resolved={FormatVector(budgetSafeDestination)} " +
                    $"Score=0.0 " +
                    $"Reason=budget_deferred_safe";

                return budgetSafeDestination;
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

        private bool IsCriticalRoute(AIMapRouteIntent routeIntent)
        {
            return routeIntent == AIMapRouteIntent.Evade ||
                   routeIntent == AIMapRouteIntent.CombatRetreat ||
                   routeIntent == AIMapRouteIntent.Peel;
        }

        private bool IsCriticalTacticalIntent(AITacticalMovementIntent intent)
        {
            return intent == AITacticalMovementIntent.Kite ||
                   intent == AITacticalMovementIntent.EmergencyRetreat;
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

            bool mapControlRoute =
                routeIntent == AIMapRouteIntent.CombatAdvance ||
                routeIntent == AIMapRouteIntent.CombatReposition ||
                routeIntent == AIMapRouteIntent.Peel ||
                routeIntent == AIMapRouteIntent.Objective ||
                routeIntent == AIMapRouteIntent.Search ||
                routeIntent == AIMapRouteIntent.Regroup;

            bool geometryRoute =
                combatRoute ||
                mapControlRoute ||
                defensiveRoute;

            bool controlRole =
                _profile.Archetype == BrawlerArchetype.Controller ||
                _profile.Archetype == BrawlerArchetype.Artillery ||
                _profile.Archetype == BrawlerArchetype.Support ||
                _profile.Archetype == BrawlerArchetype.Tank;

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
                PathCostWeight = _profile.MapPathCostWeight,
                PreferCoverPeek = hasThreatPosition && directFireRoute,
                PreferLaneControl = mapControlRoute,
                PreferChokeControl = mapControlRoute &&
                                     (controlRole ||
                                      routeIntent == AIMapRouteIntent.Objective ||
                                      routeIntent == AIMapRouteIntent.Search),
                PreferThrowerSafePosition = hasThreatPosition &&
                                            _profile.Archetype == BrawlerArchetype.Artillery &&
                                            combatRoute,
                PreferWallAwarePressure = hasThreatPosition &&
                                          (combatRoute ||
                                           routeIntent == AIMapRouteIntent.Objective ||
                                           routeIntent == AIMapRouteIntent.Search),
                PenalizeWallHug = geometryRoute,
                PreferEscapeSpace = geometryRoute,
                PreferCoverDance = hasThreatPosition && combatRoute,
                PreferFireLanePressure = hasThreatPosition && directFireRoute,
                PreferThrowerSpacing = hasThreatPosition &&
                                       _profile.Archetype == BrawlerArchetype.Artillery &&
                                       combatRoute,
                CoverPeekWeight = _profile.MapCoverPeekPreference,
                LaneControlWeight = _profile.MapLaneControlPreference,
                ChokeControlWeight = _profile.MapChokeControlPreference,
                ThrowerSafePositionWeight = _profile.MapThrowerSafePositionPreference,
                WallPressureWeight = _profile.MapWallPressurePreference,
                WallHugPenalty = _profile.MapWallHugPenalty,
                EscapeSpaceWeight = _profile.MapEscapeSpacePreference,
                CoverDanceWeight = _profile.MapCoverDancePreference,
                FireLanePressureWeight = _profile.MapFireLanePressurePreference,
                ThrowerSpacingWeight = _profile.MapThrowerSpacingPreference,
                CurrentTick = _currentExecuteTick,
                HighPriority = IsCriticalRoute(routeIntent)
            };
        }

        private string FormatVector(Vector3 value)
        {
            return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
        }

        private bool TryExecuteBrawlBallIntent(
            AIActionType actionType,
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float idealRange,
            float superRange)
        {
            BrawlBallMode mode = BrawlBallMode.Instance;
            if (mode == null || mode.IsMatchResolved)
                return false;

            if (mode.CanKickBall(_brawler))
            {
                RunBrawlBallCarrierIntent(mode, currentTick);
                return true;
            }

            if (ShouldPreserveEmergencyBrawlBallAction(actionType))
                return false;

            BrawlerController carrier = mode.BallCarrier;
            if (SpatialEntityUtility.IsAlive(carrier))
            {
                if (carrier.Team != _brawler.Team)
                {
                    RunBrawlBallEnemyCarrierIntent(
                        carrier,
                        currentTick,
                        attackRange,
                        idealRange,
                        superRange);
                    return true;
                }

                RunBrawlBallFriendlyCarrierSupportIntent(
                    mode,
                    carrier,
                    targetInfo,
                    currentTick,
                    attackRange,
                    idealRange,
                    superRange);
                return true;
            }

            if (actionType == AIActionType.UseSuper)
                return false;

            RunBrawlBallLooseBallIntent(
                mode,
                targetInfo,
                currentTick,
                attackRange,
                superRange);
            return true;
        }

        private bool ShouldPreserveEmergencyBrawlBallAction(AIActionType actionType)
        {
            if (actionType == AIActionType.Evade &&
                _dangerMemory != null &&
                _dangerMemory.HasDanger)
            {
                return true;
            }

            if (actionType != AIActionType.Retreat)
                return false;

            return GetSelfHealthRatio() <= 0.22f;
        }

        private void RunBrawlBallCarrierIntent(
            BrawlBallMode mode,
            uint currentTick)
        {
            Vector3 goalPosition = mode.TryGetScoringGoalPosition(
                    _brawler.Team,
                    out Vector3 scoringGoal)
                ? scoringGoal
                : mode.BallPosition;
            Vector3 goalMouthPosition = mode.TryGetScoringGoalMouthPosition(
                    _brawler.Team,
                    out Vector3 scoringMouth)
                ? scoringMouth
                : goalPosition;

            Vector3 toGoal = Flatten(goalPosition - _brawler.Position);
            if (toGoal.sqrMagnitude <= 0.001f)
                toGoal = Flatten(_brawler.transform.forward);

            float distanceToGoal = toGoal.magnitude;
            Vector3 kickDirection = toGoal.sqrMagnitude > 0.001f
                ? toGoal.normalized
                : Vector3.forward;

            BrawlBallController ball = mode.Ball;
            float normalKickRange = ball != null ? ball.NormalKickRange : 8f;
            float superKickRange = ball != null ? ball.SuperKickRange : normalKickRange * 1.4f;
            float ballRadius = ball != null ? ball.CollisionRadius : 0.32f;
            float approachDistance = Mathf.Clamp(normalKickRange * 0.28f, 1.1f, 2.6f);
            Vector3 approachPosition = mode.TryGetScoringGoalApproachPosition(
                    _brawler.Team,
                    approachDistance,
                    out Vector3 resolvedApproach)
                ? resolvedApproach
                : goalMouthPosition;
            bool hasKickLane = HasBrawlBallKickLane(
                goalMouthPosition,
                approachPosition,
                ballRadius);
            float distanceToApproach = Vector3.Distance(_brawler.Position, approachPosition);
            bool hasReachedShootingPocket = distanceToApproach <= 1.25f;
            bool canMainKick =
                distanceToGoal <= Mathf.Max(0.5f, normalKickRange - 0.2f) &&
                (hasKickLane || hasReachedShootingPocket);
            bool canSuperKick =
                _brawler.State != null &&
                _brawler.State.SuperCharge.IsReady &&
                _brawler.State.CanUseSuper(currentTick) &&
                distanceToGoal <= Mathf.Max(normalKickRange, superKickRange - 0.2f) &&
                distanceToGoal > normalKickRange * 0.85f &&
                (hasKickLane || hasReachedShootingPocket);
            bool shouldReleaseStalledBall =
                !canSuperKick &&
                !canMainKick &&
                ShouldReleaseStalledBrawlBall(
                    hasKickLane,
                    hasReachedShootingPocket,
                    distanceToGoal,
                    normalKickRange,
                    superKickRange);

            if (canSuperKick)
            {
                _commandSource.QueueSuper(kickDirection, goalPosition, true);
            }
            else if (canMainKick)
            {
                _commandSource.QueueMainAttack(kickDirection, goalPosition, true);
            }
            else if (shouldReleaseStalledBall)
            {
                Vector3 releaseDirection = hasKickLane
                    ? kickDirection
                    : ResolveBrawlBallOutletDirection(
                        kickDirection,
                        normalKickRange,
                        ballRadius);
                _commandSource.QueueMainAttack(
                    releaseDirection,
                    _brawler.Position + releaseDirection * normalKickRange,
                    true);
            }

            RequestDirectBrawlBallScoringDestination(approachPosition);

            _lastTacticalMovementIntent = AITacticalMovementIntent.CloseGap;
            _lastTacticalTargetPosition = approachPosition;
            _lastTacticalTargetDistance = distanceToGoal;
            _lastTacticalPreferredRange = 0f;
            _lastTacticalTooCloseDistance = 0f;
            _lastTacticalMoveReason = canSuperKick
                ? "brawl_ball_super_kick"
                : canMainKick
                    ? "brawl_ball_kick"
                    : shouldReleaseStalledBall
                        ? "brawl_ball_forced_release"
                        : hasKickLane
                            ? "brawl_ball_score_pocket"
                            : "brawl_ball_score_lane_blocked";
        }

        private void RequestDirectBrawlBallScoringDestination(Vector3 approachPosition)
        {
            ResetTacticalStop("brawl_ball_score_destination");
            _hasMapRouteCache = false;
            _lastRawMapDestination = approachPosition;
            _lastResolvedMapDestination = approachPosition;
            _lastMapRequestIntent = AIMapRouteIntent.Objective;
            _lastMapRequestHadThreatPosition = false;
            _lastMapRouteDebug =
                $"Route={AIMapRouteIntent.Objective} " +
                $"Raw={FormatVector(approachPosition)} " +
                $"Resolved={FormatVector(approachPosition)} " +
                $"Score=0.0 Reason=brawl_ball_direct";

            _navAgent.RequestDestination(
                approachPosition,
                0.65f,
                highPriority: true);
        }

        private bool ShouldReleaseStalledBrawlBall(
            bool hasKickLane,
            bool hasReachedShootingPocket,
            float distanceToGoal,
            float normalKickRange,
            float superKickRange)
        {
            if (hasReachedShootingPocket)
                return true;

            if (hasKickLane && distanceToGoal <= superKickRange + 1.5f)
                return true;

            if (_navAgent.IsRouteBlocked ||
                _navAgent.ConsecutiveRouteFailures > 0)
            {
                return true;
            }

            if (_navAgent.ConsecutiveActiveZeroMoveTicks >= 18)
                return distanceToGoal <= superKickRange + normalKickRange;

            return false;
        }

        private Vector3 ResolveBrawlBallOutletDirection(
            Vector3 preferredDirection,
            float range,
            float ballRadius)
        {
            preferredDirection = Flatten(preferredDirection);
            if (preferredDirection.sqrMagnitude <= 0.001f)
                preferredDirection = Flatten(_brawler.transform.forward);

            if (preferredDirection.sqrMagnitude <= 0.001f)
                preferredDirection = Vector3.forward;

            preferredDirection.Normalize();
            if (IsBrawlBallReleaseDirectionClear(preferredDirection, range, ballRadius))
                return preferredDirection;

            float sideBias = (_brawler.EntityID & 1) == 0 ? 1f : -1f;
            float[] angles = { 25f, -25f, 45f, -45f, 70f, -70f, 95f, -95f };
            for (int i = 0; i < angles.Length; i++)
            {
                float angle = angles[i] * sideBias;
                Vector3 candidate = Quaternion.AngleAxis(angle, Vector3.up) * preferredDirection;
                candidate = Flatten(candidate);
                if (candidate.sqrMagnitude <= 0.001f)
                    continue;

                candidate.Normalize();
                if (IsBrawlBallReleaseDirectionClear(candidate, range, ballRadius))
                    return candidate;
            }

            return preferredDirection;
        }

        private bool IsBrawlBallReleaseDirectionClear(
            Vector3 direction,
            float range,
            float ballRadius)
        {
            if (SimulationClock.Pathfinder == null)
                return true;

            AimLineTraceResult trace = AimLineOfSightUtility.Trace(
                SimulationClock.Pathfinder,
                _brawler.Position,
                direction,
                Mathf.Max(0.5f, range * 0.65f),
                Mathf.Max(0.18f, ballRadius));

            return !trace.IsBlocked;
        }

        private void RunBrawlBallEnemyCarrierIntent(
            BrawlerController carrier,
            uint currentTick,
            float attackRange,
            float idealRange,
            float superRange)
        {
            _abilityDecider.TryUseMainAttack(carrier, currentTick, attackRange);
            _abilityDecider.TryUseGadget(carrier, currentTick);
            _superDecider.TryUseSuper(carrier, currentTick, superRange);

            float preferredRange = Mathf.Min(
                GetTacticalPreferredRange(idealRange),
                Mathf.Max(1.25f, attackRange * 0.45f));

            RequestMapAwareDestination(
                carrier.Position,
                0.75f,
                AIMapRouteIntent.CombatAdvance,
                true,
                carrier.Position,
                preferredRange);

            _lastTacticalMovementIntent = AITacticalMovementIntent.CloseGap;
            _lastTacticalTargetPosition = carrier.Position;
            _lastTacticalTargetDistance = Vector3.Distance(_brawler.Position, carrier.Position);
            _lastTacticalPreferredRange = preferredRange;
            _lastTacticalTooCloseDistance = _profile.GetTooCloseDistance(idealRange);
            _lastTacticalMoveReason = "brawl_ball_enemy_carrier";
        }

        private void RunBrawlBallFriendlyCarrierSupportIntent(
            BrawlBallMode mode,
            BrawlerController carrier,
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float idealRange,
            float superRange)
        {
            TryPressureBrawlBallThreat(
                targetInfo,
                carrier.Position,
                currentTick,
                attackRange,
                superRange);

            Vector3 supportPoint = carrier.Position;
            if (mode.TryGetScoringGoalMouthPosition(_brawler.Team, out Vector3 goalPosition))
            {
                supportPoint = Vector3.Lerp(carrier.Position, goalPosition, 0.45f);
                Vector3 lane = Flatten(goalPosition - carrier.Position);
                if (lane.sqrMagnitude > 0.001f)
                {
                    lane.Normalize();
                    Vector3 lateral = new Vector3(lane.z, 0f, -lane.x);
                    float side = (_brawler.EntityID & 1) == 0 ? 1f : -1f;
                    supportPoint += lateral * side * 1.35f;
                }
            }

            float preferredRange = Mathf.Max(1.5f, GetTacticalPreferredRange(idealRange));
            RequestMapAwareDestination(
                supportPoint,
                1.15f,
                AIMapRouteIntent.Peel,
                true,
                carrier.Position,
                preferredRange);

            _lastTacticalMovementIntent = AITacticalMovementIntent.Regroup;
            _lastTacticalTargetPosition = carrier.Position;
            _lastTacticalTargetDistance = Vector3.Distance(_brawler.Position, carrier.Position);
            _lastTacticalPreferredRange = preferredRange;
            _lastTacticalTooCloseDistance = _profile.GetTooCloseDistance(idealRange);
            _lastTacticalMoveReason = "brawl_ball_support_carrier";
        }

        private void RunBrawlBallLooseBallIntent(
            BrawlBallMode mode,
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float superRange)
        {
            Vector3 ballPosition = mode.BallPosition;
            TryPressureBrawlBallThreat(
                targetInfo,
                ballPosition,
                currentTick,
                attackRange,
                superRange);

            float arrivalDistance = mode.Ball != null
                ? Mathf.Max(0.35f, mode.Ball.PickupRadius * 0.65f)
                : 0.55f;

            RequestMapAwareDestination(
                ballPosition,
                arrivalDistance,
                AIMapRouteIntent.Objective);

            _lastTacticalMovementIntent = AITacticalMovementIntent.CloseGap;
            _lastTacticalTargetPosition = ballPosition;
            _lastTacticalTargetDistance = Vector3.Distance(_brawler.Position, ballPosition);
            _lastTacticalPreferredRange = 0f;
            _lastTacticalTooCloseDistance = 0f;
            _lastTacticalMoveReason = "brawl_ball_loose_ball";
        }

        private void TryPressureBrawlBallThreat(
            AITargetInfo targetInfo,
            Vector3 focusPosition,
            uint currentTick,
            float attackRange,
            float superRange)
        {
            if (targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !SpatialEntityUtility.IsAlive(targetInfo.Target))
            {
                return;
            }

            float targetDistance = Vector3.Distance(_brawler.Position, targetInfo.Target.Position);
            float focusDistance = Vector3.Distance(focusPosition, targetInfo.Target.Position);
            if (targetDistance > attackRange * 1.1f && focusDistance > 4.5f)
                return;

            _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
            _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
        }

        private bool HasBrawlBallKickLane(
            Vector3 laneTarget,
            Vector3 fallbackPocket,
            float ballRadius)
        {
            if (SimulationClock.Pathfinder == null)
                return true;

            Vector3 target = laneTarget;
            Vector2Int targetCoords = SimulationClock.Pathfinder.GetGridCoords(target);
            if (!SimulationClock.Pathfinder.IsInBounds(targetCoords) ||
                !SimulationClock.Pathfinder.IsWalkable(targetCoords))
            {
                target = fallbackPocket;
            }

            Vector3 toTarget = Flatten(target - _brawler.Position);
            float distance = toTarget.magnitude;
            float checkDistance = distance - Mathf.Max(0.75f, ballRadius * 2f);
            if (checkDistance <= 0.1f)
                return true;

            AimLineTraceResult trace = AimLineOfSightUtility.Trace(
                SimulationClock.Pathfinder,
                _brawler.Position,
                toTarget,
                checkDistance,
                Mathf.Max(0.18f, ballRadius));

            return !trace.IsBlocked;
        }

        private float GetSelfHealthRatio()
        {
            if (_brawler == null || _brawler.State == null)
                return 1f;

            return _brawler.State.CurrentHealth /
                   Mathf.Max(1f, _brawler.State.MaxHealth.Value);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            return value;
        }

        private void RunObjective(
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float idealRange,
            float superRange)
        {
            // Combat always overrides objective movement.
            if (targetInfo != null && targetInfo.HasLiveTarget)
            {
                _hasObjectiveDebug = false;
                _objectiveSlotCommitment.Reset();
                RunHoldRange(targetInfo, currentTick, attackRange, idealRange, superRange);
                return;
            }

            if (_objectiveMemory == null)
            {
                RecordObjectiveNeglect(currentTick, "objective_memory_missing");
                _hasObjectiveDebug = false;
                _objectiveSlotCommitment.Reset();
                RunSearch(targetInfo, currentTick);
                return;
            }

            if (!_objectiveMemory.TryGetBestObjective(
                    _brawler.Position,
                    _profile.PreferredObjective,
                    _brawler.Team,
                    out AIObjectiveCandidate objective))
            {
                RecordObjectiveNeglect(currentTick, "objective_candidate_missing");
                _hasObjectiveDebug = false;
                _objectiveSlotCommitment.Reset();
                RunSearch(targetInfo, currentTick);
                return;
            }

            AIIntentValidationResult validation =
                AIIntentValidationUtility.ValidateObjectiveIntent(
                    objective,
                    _profile,
                    SimulationClock.Pathfinder);

            if (!validation.IsValid)
            {
                AIIncidentLogger.Record(
                    _brawler.EntityID,
                    AIIncidentType.ObjectiveIntentInvalid,
                    currentTick,
                    validation.Reason);
                RecordObjectiveNeglect(currentTick, validation.Reason);
                _hasObjectiveDebug = false;
                _objectiveSlotCommitment.Reset();
                RunSearch(targetInfo, currentTick);
                return;
            }

            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.ObjectiveIntent,
                currentTick);

            Vector3 objectivePosition = validation.ResolvedDestination;
            BrawlerArchetype archetype = _profile != null
                ? _profile.Archetype
                : BrawlerArchetype.Fighter;
            AIObjectiveSlotRole slotRole = _objectiveSlotCommitment.SelectRole(
                objective,
                archetype,
                _currentExecuteTick,
                out AIObjectiveSlotRole desiredSlotRole);

            Vector3 slotPosition = AIObjectiveSlotUtility.GetObjectiveSlotPosition(
                _brawler.Team,
                archetype,
                _brawler.EntityID,
                objectivePosition,
                objective.Radius,
                slotRole,
                objective.FriendlyPresence,
                objective.EnemyPresence);

            if (TryResolveLaneHoldPoint(
                    objectivePosition,
                    _currentExecuteTick,
                    out Vector3 lanePosition,
                    out _))
            {
                float laneBlend = Mathf.Clamp01(
                    Mathf.Max(0f, _profile.LaneDisciplineWeight) * 0.45f);
                slotPosition = Vector3.Lerp(slotPosition, lanePosition, laneBlend);
            }

            Vector3 destination = ResolveMapAwareDestination(
                slotPosition,
                AIMapRouteIntent.Objective);

            _lastObjectiveCenter = objectivePosition;
            _lastObjectiveSlot = slotPosition;
            _lastObjectiveDestination = destination;
            _lastObjectiveName = objective.Name;
            _lastObjectiveType = objective.ObjectiveType;
            _lastObjectiveRadius = objective.Radius;
            _lastObjectiveIsRuntime = objective.IsRuntime;
            _lastObjectiveControlState = objective.ControlState;
            _lastObjectiveFriendlyPresence = objective.FriendlyPresence;
            _lastObjectiveEnemyPresence = objective.EnemyPresence;
            _lastObjectiveSlotRole = slotRole;
            _lastObjectiveDesiredSlotRole = desiredSlotRole;
            _hasObjectiveDebug = true;

            _navAgent.RequestDestination(destination, 1f);
        }

        private void RecordObjectiveNeglect(uint currentTick, string reason)
        {
            AIIncidentLogger.Record(
                _brawler.EntityID,
                AIIncidentType.ObjectiveNeglect,
                currentTick,
                reason);
            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.ObjectiveNeglect,
                currentTick);
        }

        private void RunApproach(
       AITargetInfo targetInfo,
       uint currentTick,
       float attackRange,
       float idealRange,
       float superRange)
        {
            if (targetInfo == null || !targetInfo.HasLiveTarget || targetInfo.Target == null)
            {
                _lastTacticalMovementIntent = AITacticalMovementIntent.None;
                RunSearch(targetInfo, currentTick);
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

                _navAgent.RequestDestination(
                    destination,
                    0.5f,
                    IsCriticalTacticalIntent(_lastTacticalMovementIntent));
            }
            else
            {
                _navAgent.RequestDestination(
                    _lastTacticalMoveDestination,
                    0.5f,
                    IsCriticalTacticalIntent(_lastTacticalMovementIntent));
            }
        }

        private void RunHoldRange(
     AITargetInfo targetInfo,
     uint currentTick,
     float attackRange,
     float idealRange,
     float superRange)
        {
            if (targetInfo == null || !targetInfo.HasLiveTarget || targetInfo.Target == null)
            {
                _lastTacticalMovementIntent = AITacticalMovementIntent.None;
                RunSearch(targetInfo, currentTick);
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

                    _navAgent.RequestDestination(
                        destination,
                        0.5f,
                        IsCriticalTacticalIntent(_lastTacticalMovementIntent));
                }
                else
                {
                    _navAgent.RequestDestination(
                        _lastTacticalMoveDestination,
                        0.5f,
                        IsCriticalTacticalIntent(_lastTacticalMovementIntent));
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

                _navAgent.RequestDestination(
                    tacticalDestination,
                    0.4f,
                    IsCriticalTacticalIntent(_lastTacticalMovementIntent));
            }
            else
            {
                _navAgent.RequestDestination(
                    _lastTacticalMoveDestination,
                    0.4f,
                    IsCriticalTacticalIntent(_lastTacticalMovementIntent));
            }
        }

        private void RunReposition(
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float idealRange,
            float superRange)
        {
            if (targetInfo == null || !targetInfo.HasLiveTarget || targetInfo.Target == null)
            {
                _lastTacticalMovementIntent = AITacticalMovementIntent.None;
                RunSearch(targetInfo, currentTick);
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

                _navAgent.RequestDestination(
                    destination,
                    0.45f,
                    IsCriticalTacticalIntent(_lastTacticalMovementIntent));
            }
            else
            {
                _navAgent.RequestDestination(
                    _lastTacticalMoveDestination,
                    0.45f,
                    IsCriticalTacticalIntent(_lastTacticalMovementIntent));
            }
        }

        private void RunRetreat(AITargetInfo targetInfo, uint currentTick)
        {
            if (targetInfo == null || !targetInfo.HasLiveTarget || targetInfo.Target == null)
            {
                if (!TryRunRetreatWithoutTarget(currentTick))
                    RunFallbackWander(currentTick);

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

        private bool TryRunRetreatWithoutTarget(uint currentTick)
        {
            if (TryRunCarrierRefuge(currentTick, AIMapRouteIntent.CombatRetreat, 0.55f))
                return true;

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetRegroupPoint(currentTick, out var regroupPoint))
            {
                RequestMapAwareDestination(
                    regroupPoint,
                    1.0f,
                    AIMapRouteIntent.Regroup);
                return true;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetThreatCenter(currentTick, out var threatCenter, out _))
            {
                Vector3 retreatPoint = _spacingUtility.GetRetreatPosition(
                    threatCenter,
                    _profile.RetreatStepDistance,
                    _profile.AllyAvoidanceRadius,
                    _profile.AllyAvoidanceWeight);

                RequestMapAwareDestination(
                    retreatPoint,
                    0.5f,
                    AIMapRouteIntent.CombatRetreat,
                    true,
                    threatCenter,
                    GetTacticalPreferredRange(GetAbilityIdealRange()));
                return true;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetEnemyHotspot(currentTick, out var enemyHotspot, out _))
            {
                Vector3 retreatPoint = _spacingUtility.GetRetreatPosition(
                    enemyHotspot,
                    _profile.RetreatStepDistance,
                    _profile.AllyAvoidanceRadius,
                    _profile.AllyAvoidanceWeight);

                RequestMapAwareDestination(
                    retreatPoint,
                    0.5f,
                    AIMapRouteIntent.CombatRetreat,
                    true,
                    enemyHotspot,
                    GetTacticalPreferredRange(GetAbilityIdealRange()));
                return true;
            }

            return false;
        }

        private bool TryRunCarrierRefuge(
            uint currentTick,
            AIMapRouteIntent routeIntent,
            float arrivalDistance)
        {
            if (_teamCoordinator == null ||
                !_teamCoordinator.TryGetPlaybookState(currentTick, out AITeamPlaybookState playbookState) ||
                playbookState.Call != AITeamPlaybookCall.EscortCarrier ||
                playbookState.EscortRole != AITeamEscortFormationRole.CarrierAnchor ||
                !playbookState.HasAnchorPoint)
            {
                return false;
            }

            RequestMapAwareDestination(
                playbookState.AnchorPoint,
                arrivalDistance,
                routeIntent,
                playbookState.HasPressurePoint,
                playbookState.PressurePoint,
                GetTacticalPreferredRange(GetAbilityIdealRange()));

            return true;
        }

        private void RunEvade(
            AITargetInfo targetInfo,
            uint currentTick,
            float attackRange,
            float superRange)
        {
            if (_dangerMemory == null || !_dangerMemory.HasDanger)
            {
                if (targetInfo != null && targetInfo.HasLiveTarget)
                {
                    RunRetreat(targetInfo, currentTick);
                    return;
                }

                if (!TryRunRetreatWithoutTarget(currentTick))
                    RunSearch(targetInfo, currentTick);

                return;
            }

            if (targetInfo != null && targetInfo.HasLiveTarget && targetInfo.Target != null)
            {
                _abilityDecider.TryUseMainAttack(targetInfo.Target, currentTick, attackRange);
                _abilityDecider.TryUseGadget(targetInfo.Target, currentTick);
                _superDecider.TryUseSuper(targetInfo.Target, currentTick, superRange);
            }

            if (!ShouldRefreshEvadeMove(currentTick, out string refreshReason))
            {
                _navAgent.RequestDestination(
                    _lastTacticalMoveDestination,
                    0.4f,
                    highPriority: true);
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

            _navAgent.RequestDestination(
                resolvedDestination,
                0.4f,
                highPriority: true);
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
            if (TryRunGemPickupSearch(currentTick))
                return;

            if (targetInfo != null &&
                targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
            {
                RequestMapAwareDestination(
                    targetInfo.LastKnownPosition,
                    1.0f,
                    AIMapRouteIntent.Search);
                return;
            }

            if (TryRunPlaybookPressureSearch(currentTick))
                return;

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

            if (TryRunLaneHold(currentTick))
                return;

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
                if (_objectiveMemory.TryGetBestObjective(
                        _brawler.Position,
                        _profile.PreferredObjective,
                        _brawler.Team,
                        out AIObjectiveCandidate objective))
                {
                    RequestMapAwareDestination(
                        objective.Position,
                        1.0f,
                        AIMapRouteIntent.Objective);

                    return;
                }
            }

            RunFallbackWander(currentTick);
        }

        private bool TryRunGemPickupSearch(uint currentTick)
        {
            if (_profile == null || _profile.GemPickupSearchRadius <= 0f)
                return false;

            bool hasThreatCenter = false;
            Vector3 threatCenter = Vector3.zero;
            float threatPressure = 0f;
            bool hasEnemyHotspot = false;
            Vector3 enemyHotspot = Vector3.zero;
            float hotspotPressure = 0f;

            if (_teamCoordinator != null)
            {
                hasThreatCenter = _teamCoordinator.TryGetThreatCenter(
                    currentTick,
                    out threatCenter,
                    out threatPressure);
                hasEnemyHotspot = _teamCoordinator.TryGetEnemyHotspot(
                    currentTick,
                    out enemyHotspot,
                    out hotspotPressure);
            }

            AIGameModeMacroState macroState =
                AIGameModeMacroStrategy.ResolveCurrentMode(_brawler.Team);

            if (!AIGemGrabObjectiveUtility.TryFindBestPickup(
                    _brawler,
                    _profile,
                    macroState,
                    hasThreatCenter,
                    threatCenter,
                    threatPressure,
                    hasEnemyHotspot,
                    enemyHotspot,
                    hotspotPressure,
                    out AIGemPickupDecision decision))
            {
                return false;
            }

            AIIntentValidationResult validation =
                AIIntentValidationUtility.ValidateGemPickupIntent(
                    decision,
                    _brawler.Position,
                    _profile,
                    SimulationClock.Pathfinder);

            if (!validation.IsValid)
            {
                AIIncidentLogger.Record(
                    _brawler.EntityID,
                    AIIncidentType.GemIntentInvalid,
                    currentTick,
                    validation.Reason);
                return false;
            }

            AIValidationGauntlet.RecordSignal(
                AIValidationGauntletSignal.GemPickupIntent,
                currentTick);

            RequestMapAwareDestination(
                validation.ResolvedDestination,
                0.65f,
                AIMapRouteIntent.Objective);

            return true;
        }

        private bool TryRunLaneHold(uint currentTick)
        {
            if (_profile == null || !_profile.UseLaneDiscipline)
                return false;

            Vector3 anchorPoint = Vector3.zero;
            bool hasAnchor = false;

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetPlaybookState(currentTick, out AITeamPlaybookState playbookState))
            {
                if (playbookState.HasAnchorPoint)
                {
                    anchorPoint = playbookState.AnchorPoint;
                    hasAnchor = true;
                }
                else if (playbookState.HasPressurePoint)
                {
                    anchorPoint = playbookState.PressurePoint;
                    hasAnchor = true;
                }
            }

            if (!hasAnchor &&
                _objectiveMemory != null &&
                _objectiveMemory.TryGetBestObjective(
                    _brawler.Position,
                    _profile.PreferredObjective,
                    _brawler.Team,
                    out AIObjectiveCandidate objective))
            {
                anchorPoint = objective.Position;
                hasAnchor = true;
            }

            if (!hasAnchor ||
                !TryResolveLaneHoldPoint(
                    anchorPoint,
                    currentTick,
                    out Vector3 lanePoint,
                    out _))
            {
                return false;
            }

            RequestMapAwareDestination(
                lanePoint,
                1.0f,
                AIMapRouteIntent.Search);

            return true;
        }

        private bool TryRunPlaybookPressureSearch(uint currentTick)
        {
            if (_teamCoordinator == null ||
                !_teamCoordinator.TryGetPlaybookState(currentTick, out AITeamPlaybookState playbookState) ||
                !playbookState.HasPressurePoint)
            {
                return false;
            }

            RequestMapAwareDestination(
                playbookState.PressurePoint,
                1.0f,
                AIMapRouteIntent.Search,
                true,
                playbookState.PressurePoint,
                GetTacticalPreferredRange(GetAbilityIdealRange()));

            return true;
        }

        private bool TryResolveLaneHoldPoint(
            Vector3 anchorPoint,
            uint currentTick,
            out Vector3 lanePoint,
            out string reason)
        {
            AITeamLaneAssignment lane = AILaneDisciplineUtility.ResolveAssignedLane(
                _brawler.EntityID);

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetLaneOwnership(
                    currentTick,
                    out AITeamLaneOwnershipSnapshot laneOwnership) &&
                laneOwnership.HasRecommendedLane)
            {
                lane = laneOwnership.RecommendedLane;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetPlaybookState(currentTick, out AITeamPlaybookState playbookState) &&
                playbookState.Lane != AITeamLaneAssignment.None)
            {
                lane = playbookState.Lane;
            }

            return AILaneDisciplineUtility.TryResolveLaneHoldPoint(
                _brawler,
                _profile,
                lane,
                anchorPoint,
                out lanePoint,
                out reason);
        }

        private void RunFallbackWander(uint currentTick)
        {
            if (currentTick >= _nextFallbackWanderTick || !_navAgent.HasDestination)
            {
                float minimumStep = Mathf.Max(0.5f, _profile.TacticalMinimumStepDistance);
                float radius = Mathf.Max(minimumStep, _profile.FallbackWanderRadius);
                Vector2 random2D = Random.insideUnitCircle;
                if (random2D.sqrMagnitude <= 0.0001f)
                    random2D = Vector2.right;

                random2D = random2D.normalized * Random.Range(minimumStep, radius);
                _fallbackWanderPoint = _brawler.Position + new Vector3(random2D.x, 0f, random2D.y);
                RequestMapAwareDestination(
                    _fallbackWanderPoint,
                    0.5f,
                    AIMapRouteIntent.Wander);
                _nextFallbackWanderTick = currentTick + _profile.FallbackWanderRetargetTicks;
            }
        }

        private void RunTacticalStopOrFallback(
            uint currentTick,
            string reason,
            bool isStopLegal)
        {
            if (_tacticalStopStartedTick == 0u)
                _tacticalStopStartedTick = currentTick;

            AITacticalStopDecision decision = AITacticalStopPolicy.Evaluate(
                isStopLegal,
                currentTick,
                _tacticalStopStartedTick,
                _profile != null ? _profile.TacticalStopMaxHoldTicks : 1u,
                reason);

            _lastTacticalStopDebug = decision.GetDebugSummary();

            if (decision.CanHoldStop)
            {
                AIIncidentLogger.Record(
                    _brawler.EntityID,
                    AIIncidentType.TacticalStop,
                    currentTick,
                    decision.Reason);
                AIValidationGauntlet.RecordSignal(
                    AIValidationGauntletSignal.TacticalStop,
                    currentTick);
                _navAgent.Stop();
                return;
            }

            ResetTacticalStop(decision.Reason);
            RunFallbackWander(currentTick);
        }

        private void ResetTacticalStop(string reason)
        {
            _tacticalStopStartedTick = 0u;
            _lastTacticalStopDebug = $"Stop=None Reason={reason}";
        }

        private void RunUseSuper(AITargetInfo targetInfo, uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (targetInfo == null || !targetInfo.HasLiveTarget || targetInfo.Target == null)
            {
                RunFallbackWander(currentTick);
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
            if (TryRunCarrierRefuge(currentTick, AIMapRouteIntent.Regroup, 0.75f))
                return;

            if (_teamCoordinator != null && _teamCoordinator.TryGetRegroupPoint(currentTick, out var point))
            {
                RequestMapAwareDestination(
                    point,
                    1.0f,
                    AIMapRouteIntent.Regroup);
                return;
            }

            RunFallbackWander(currentTick);
        }

        private void RunPeel(uint currentTick, float attackRange, float idealRange, float superRange)
        {
            if (_teamCoordinator == null || !_teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) || ally == null)
            {
                if (TryRunPlaybookEscort(currentTick))
                    return;

                RunFallbackWander(currentTick);
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

        private bool TryRunPlaybookEscort(uint currentTick)
        {
            if (_teamCoordinator == null ||
                !_teamCoordinator.TryGetPlaybookState(currentTick, out AITeamPlaybookState playbookState) ||
                playbookState.Call != AITeamPlaybookCall.EscortCarrier ||
                !playbookState.HasEscortTargetPoint)
            {
                return false;
            }

            bool pressureEscort =
                playbookState.EscortRole == AITeamEscortFormationRole.Screen ||
                playbookState.EscortRole == AITeamEscortFormationRole.PressureFlank;
            AIMapRouteIntent routeIntent =
                playbookState.EscortRole == AITeamEscortFormationRole.PressureFlank
                    ? AIMapRouteIntent.Search
                    : AIMapRouteIntent.Peel;

            RequestMapAwareDestination(
                playbookState.EscortTargetPoint,
                playbookState.EscortRole == AITeamEscortFormationRole.PressureFlank
                    ? 0.75f
                    : 1.0f,
                routeIntent,
                pressureEscort && playbookState.HasPressurePoint,
                playbookState.PressurePoint,
                GetTacticalPreferredRange(GetAbilityIdealRange()));

            return true;
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

            if (targetInfo != null && targetInfo.HasLiveTarget && targetInfo.Target != null)
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

            if (refreshReason == "movement_heartbeat")
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
            destination = StabilizeTacticalDestination(intent, destination, currentTick);

            _lastTacticalMovementIntent = intent;
            _lastTacticalMoveDestination = destination;
            _lastTacticalRetargetTick = currentTick;
            RecordTacticalMoveDirection(destination, currentTick);
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
            float minimumStep = GetTacticalMinimumStepDistance();
            Vector3 offset = _lastTacticalMoveDestination - _brawler.Position;
            offset.y = 0f;
            float distSq = offset.sqrMagnitude;
            return distSq <= minimumStep * minimumStep;
        }

        private Vector3 StabilizeTacticalDestination(
            AITacticalMovementIntent intent,
            Vector3 destination,
            uint currentTick)
        {
            if (IsCriticalTacticalIntent(intent) ||
                IsCriticalTacticalIntent(_lastTacticalMovementIntent) ||
                _lastTacticalMovementIntent == AITacticalMovementIntent.None ||
                !_navAgent.HasDestination)
            {
                return destination;
            }

            uint stabilizationMemoryTicks = GetTacticalDirectionFlipCooldownTicks() * 2u;
            if ((currentTick - _lastTacticalRetargetTick) > stabilizationMemoryTicks)
                return destination;

            Vector3 candidateOffset = destination - _brawler.Position;
            candidateOffset.y = 0f;

            Vector3 currentOffset = _lastTacticalMoveDestination - _brawler.Position;
            currentOffset.y = 0f;

            float minimumStep = GetTacticalMinimumStepDistance();
            if (candidateOffset.sqrMagnitude <= minimumStep * minimumStep ||
                currentOffset.sqrMagnitude <= minimumStep * minimumStep)
            {
                return destination;
            }

            Vector3 destinationDelta = destination - _lastTacticalMoveDestination;
            destinationDelta.y = 0f;

            float switchDistance = GetTacticalDestinationSwitchDistance();
            bool stillTravelling = !IsNearLastTacticalDestination();
            if (stillTravelling &&
                destinationDelta.sqrMagnitude < switchDistance * switchDistance)
            {
                AppendPendingTacticalRefreshReason("stabilized_small_delta");
                return _lastTacticalMoveDestination;
            }

            Vector3 candidateDirection = candidateOffset.normalized;
            Vector3 currentDirection = _hasTacticalMoveDirection
                ? _lastTacticalMoveDirection
                : currentOffset.normalized;

            bool reversingDirection = Vector3.Dot(candidateDirection, currentDirection) < -0.35f;
            if (reversingDirection && !CanFlipTacticalDirection(currentTick))
            {
                AppendPendingTacticalRefreshReason("stabilized_flip_cooldown");
                return _lastTacticalMoveDestination;
            }

            float blend = GetTacticalDestinationBlend();
            if (stillTravelling && blend < 0.99f)
            {
                Vector3 blendedDestination = Vector3.Lerp(
                    _lastTacticalMoveDestination,
                    destination,
                    blend);
                blendedDestination.y = _brawler.Position.y;
                AppendPendingTacticalRefreshReason("stabilized_blend");
                return blendedDestination;
            }

            return destination;
        }

        private bool CanFlipTacticalDirection(uint currentTick)
        {
            if (!_hasTacticalMoveDirection)
                return true;

            uint cooldownTicks = GetTacticalDirectionFlipCooldownTicks();
            return (currentTick - _lastTacticalDirectionFlipTick) >= cooldownTicks;
        }

        private void RecordTacticalMoveDirection(Vector3 destination, uint currentTick)
        {
            Vector3 direction = destination - _brawler.Position;
            direction.y = 0f;

            if (direction.sqrMagnitude <= 0.001f)
                return;

            direction.Normalize();

            if (!_hasTacticalMoveDirection ||
                Vector3.Dot(direction, _lastTacticalMoveDirection) < -0.35f)
            {
                _lastTacticalDirectionFlipTick = currentTick;
            }

            _lastTacticalMoveDirection = direction;
            _hasTacticalMoveDirection = true;
        }

        private uint GetTacticalDirectionFlipCooldownTicks()
        {
            return _profile.TacticalDirectionFlipCooldownTicks == 0u
                ? 24u
                : _profile.TacticalDirectionFlipCooldownTicks;
        }

        private float GetTacticalDestinationSwitchDistance()
        {
            return Mathf.Max(
                0.25f,
                _profile.TacticalDestinationSwitchDistance <= 0f
                    ? 1.2f
                    : _profile.TacticalDestinationSwitchDistance);
        }

        private float GetTacticalDestinationBlend()
        {
            float blend = _profile.TacticalDestinationBlend <= 0f
                ? 0.55f
                : _profile.TacticalDestinationBlend;

            return Mathf.Clamp(blend, 0.05f, 1f);
        }

        private float GetTacticalMinimumStepDistance()
        {
            return Mathf.Max(0.25f, _profile.TacticalMinimumStepDistance);
        }

        private Vector3 EnsureMeaningfulTacticalDestination(
            Vector3 destination,
            AITacticalMovementIntent intent)
        {
            Vector3 offset = destination - _brawler.Position;
            offset.y = 0f;

            float minimumStep = GetTacticalMinimumStepDistance();
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
            AbilityDefinition attack = _brawler.State != null
                ? _brawler.State.GetCurrentMainAttackDefinition()
                : _brawler.Definition?.MainAttack;
            return attack != null ? attack.GetAIIdealRange() : 6f;
        }

        private int GetStableStrafeSide()
        {
            // Keep side stable, but split bots by identity.
            if (_lastStrafeSide == 0)
                _lastStrafeSide = (_brawler.EntityID % 2 == 0) ? 1 : -1;

            return _lastStrafeSide;
        }

        private Vector3 BuildTacticalCombatDestination(
    AITargetInfo targetInfo,
    uint currentTick,
    float idealRange)
        {
            if (targetInfo == null || !targetInfo.HasLiveTarget || targetInfo.Target == null)
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

            AICombatMicroMovementDecision microDecision =
                ResolveCombatMicroMovement(
                    dist,
                    preferredRange,
                    tooCloseDistance,
                    currentTick);

            AITacticalMovementIntent intent;
            Vector3 destination;

            if (microDecision.Style == AICombatMicroMoveStyle.ThrowerSpacing)
            {
                intent = AITacticalMovementIntent.RepositionAngle;
                destination =
                    selfPos +
                    awayFromTarget * (_profile.TacticalKiteDistance * 0.95f) +
                    side * (_profile.TacticalStrafeDistance * 0.45f);

                return CommitTacticalMove(
                    intent,
                    destination,
                    currentTick,
                    $"micro_thrower_spacing dist={dist:0.0} preferred={preferredRange:0.0}");
            }

            if (microDecision.Style == AICombatMicroMoveStyle.ReloadBait &&
                dist >= tooCloseDistance)
            {
                intent = AITacticalMovementIntent.RepositionAngle;
                destination =
                    selfPos +
                    awayFromTarget * (_profile.TacticalKiteDistance * 0.45f) +
                    side * (_profile.TacticalStrafeDistance * 0.65f);

                return CommitTacticalMove(
                    intent,
                    destination,
                    currentTick,
                    $"micro_reload_bait dist={dist:0.0} preferred={preferredRange:0.0}");
            }

            if (microDecision.Style == AICombatMicroMoveStyle.DodgeFeint)
            {
                intent = AITacticalMovementIntent.Strafe;
                destination =
                    selfPos -
                    side * (_profile.TacticalStrafeDistance * 0.65f) +
                    awayFromTarget * (_profile.TacticalKiteDistance * 0.15f);

                return CommitTacticalMove(
                    intent,
                    destination,
                    currentTick,
                    $"micro_dodge_feint dist={dist:0.0} preferred={preferredRange:0.0}");
            }

            if (microDecision.Style == AICombatMicroMoveStyle.PeekTiming)
            {
                intent = AITacticalMovementIntent.Strafe;
                destination =
                    selfPos +
                    side * (_profile.TacticalStrafeDistance * 0.85f) +
                    toTarget * 0.35f;

                return CommitTacticalMove(
                    intent,
                    destination,
                    currentTick,
                    $"micro_peek_timing dist={dist:0.0} preferred={preferredRange:0.0}");
            }

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

        private AICombatMicroMovementDecision ResolveCombatMicroMovement(
            float targetDistance,
            float preferredRange,
            float tooCloseDistance,
            uint currentTick)
        {
            if (_brawler == null || _brawler.State == null || _brawler.State.Ammo == null)
                return new AICombatMicroMovementDecision(AICombatMicroMoveStyle.None, "ammo_unknown");

            return AICombatMicroUtility.ResolveMovementStyle(
                _brawler.State.Ammo.AvailableBars,
                _brawler.State.Ammo.MaxAmmo,
                _brawler.State.Ammo.CurrentAmmo,
                targetDistance,
                preferredRange,
                tooCloseDistance,
                _profile.Archetype == BrawlerArchetype.Artillery,
                _dangerMemory != null && _dangerMemory.HasDanger,
                currentTick,
                _brawler.EntityID);
        }

        private Vector3 BuildTacticalRepositionDestination(
      AITargetInfo targetInfo,
      uint currentTick,
      float idealRange)
        {
            if (targetInfo == null || !targetInfo.HasLiveTarget || targetInfo.Target == null)
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
            if (targetInfo == null || !targetInfo.HasLiveTarget || targetInfo.Target == null)
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
