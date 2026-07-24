using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using System.Collections.Generic;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIUtilityScorer
    {
        private readonly BrawlerController _self;
        private readonly BrawlerAIProfile _profile;
        private readonly AIObjectiveMemory _objectiveMemory;
        private readonly AITeamCoordinator _teamCoordinator;
        private readonly AIReactiveMemory _reactiveMemory;
        private readonly AIDangerMemory _dangerMemory;
        private readonly AIChaseDisengageMemory _chaseDisengageMemory =
            new AIChaseDisengageMemory();

        private readonly uint _threatForgetTicks = 240;

        private bool IsSniper => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Sniper;
        private bool IsTank => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Tank;
        private bool IsAssassin => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Assassin;
        private bool IsSupport => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Support;
        private bool IsFighter => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Fighter;
        private bool IsController => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Controller;
        private bool IsArtillery => _self.Definition != null && _self.Definition.Archetype == BrawlerArchetype.Artillery;

        private const float MinActionScore = 0f;
        private const float MaxNormalActionScore = 100f;
        private const float MaxEmergencyActionScore = 120f;
        private readonly List<AIActionScore> _scoreBuffer = new List<AIActionScore>(16);
        private readonly List<ISpatialEntity> _nearbyAllyBuffer = new List<ISpatialEntity>(16);

        private float _lastObjectiveAllyPressure;
        private float _lastObjectiveCrowdingPenalty;
        private float _lastObjectiveRawScore;
        private float _lastObjectiveFinalScore;
        private string _lastObjectiveScoreReason;
        private string _lastTeamRoleDebug = "RoleCoord=None";
        private string _lastMacroDebug = "Macro=None";
        private string _lastPlaybookDebug = "Playbook=None";
        private string _lastChaseDebug = "Chase=None";
        private string _lastGemPickupDebug = "GemPickup=None";
        private string _lastResourceAwarenessDebug = "ResAware=None";
        private string _lastTeamFightDebug = "TeamFight=None";
        private string _lastRoleMacroDebug = "RoleMacro=None";
        private string _lastRoleMatchupDebug = "Matchup=None";
        private string _lastCoverPeekDebug = "CoverPeek=None";
        private string _lastModeClutchDebug = "Clutch=None";
        private string _lastSpacingDebug = "Spacing=None";
        private string _lastAbilityThreatDebug = "ThreatPred=None";
        private string _lastEngagementRiskDebug = "EngageRisk=None";
        private string _lastPressureRotationDebug = "PressureRot=None";
        private string _lastObjectiveIntentDebug = "ObjIntent=None";
        private string _lastWinConditionDebug = "Win=None";
        private string _lastBrawlerIdentityDebug = "Identity=None";
        private uint _lastLaneEvaluationTick;
        private bool _hasLaneEvaluation;
        private bool _lastCanHoldLane;
        private string _lastLaneHoldReason = "lane_not_evaluated";
        private bool _hasGemPickupDecisionCache;
        private uint _lastGemPickupDecisionTick;
        private AIGemPickupDecision _lastGemPickupDecision;
        private bool _lastGemPickupShouldPickup;

        public float LastObjectiveAllyPressure => _lastObjectiveAllyPressure;
        public float LastObjectiveCrowdingPenalty => _lastObjectiveCrowdingPenalty;
        public float LastObjectiveRawScore => _lastObjectiveRawScore;
        public float LastObjectiveFinalScore => _lastObjectiveFinalScore;
        public string LastObjectiveScoreReason => _lastObjectiveScoreReason;
        public string LastTeamRoleDebug => _lastTeamRoleDebug;
        public string LastMacroDebug => _lastMacroDebug;
        public string LastPlaybookDebug => _lastPlaybookDebug;
        public string LastChaseDebug => _lastChaseDebug;
        public string LastGemPickupDebug => _lastGemPickupDebug;
        public string LastResourceAwarenessDebug => _lastResourceAwarenessDebug;
        public string LastTeamFightDebug => _lastTeamFightDebug;
        public string LastRoleMacroDebug => _lastRoleMacroDebug;
        public string LastRoleMatchupDebug => _lastRoleMatchupDebug;
        public string LastCoverPeekDebug => _lastCoverPeekDebug;
        public string LastModeClutchDebug => _lastModeClutchDebug;
        public string LastSpacingDebug => _lastSpacingDebug;
        public string LastAbilityThreatDebug => _lastAbilityThreatDebug;
        public string LastEngagementRiskDebug => _lastEngagementRiskDebug;
        public string LastPressureRotationDebug => _lastPressureRotationDebug;
        public string LastObjectiveIntentDebug => _lastObjectiveIntentDebug;
        public string LastWinConditionDebug => _lastWinConditionDebug;
        public string LastBrawlerIdentityDebug => _lastBrawlerIdentityDebug;

        public AIUtilityScorer(
            BrawlerController self,
            BrawlerAIProfile profile,
            AIObjectiveMemory objectiveMemory,
            AITeamCoordinator teamCoordinator,
            AIReactiveMemory reactiveMemory = null,
            AIDangerMemory dangerMemory = null)
        {
            _self = self;
            _profile = profile;
            _objectiveMemory = objectiveMemory;
            _teamCoordinator = teamCoordinator;
            _reactiveMemory = reactiveMemory;
            _dangerMemory = dangerMemory;
        }

        public AIActionScore ScoreBestAction(AITargetInfo targetInfo, uint currentTick)
        {
            CollectActionScores(targetInfo, currentTick, _scoreBuffer);

            AIActionScore best = new AIActionScore(AIActionType.Wander, 0f);
            for (int i = 0; i < _scoreBuffer.Count; i++)
            {
                ScoreAndReplace(ref best, _scoreBuffer[i]);
            }

            return best;
        }

        public void CollectActionScores(AITargetInfo targetInfo, uint currentTick, List<AIActionScore> results)
        {
            results.Clear();

            AIGameModeMacroState macroState = ResolveMacroState();
            AITeamPlaybookState playbookState = ResolvePlaybookState(
                targetInfo,
                currentTick,
                macroState);

            results.Add(ScoreEvade());
            results.Add(ScoreRetreat(targetInfo, currentTick, macroState));
            results.Add(ScoreUseSuper(targetInfo, macroState));
            results.Add(ScoreHoldRange(targetInfo));
            results.Add(ScoreReposition(targetInfo, currentTick, macroState));
            results.Add(ScoreApproach(targetInfo, currentTick, macroState));
            results.Add(ScorePeel(currentTick, macroState));
            results.Add(ScoreRegroup(targetInfo, currentTick, macroState));
            results.Add(ScoreSearch(targetInfo, currentTick, macroState));
            results.Add(ScoreWander());
            results.Add(ScoreObjective(targetInfo, currentTick, macroState));

            ApplyObjectiveIntentArbitration(
                targetInfo,
                currentTick,
                macroState,
                playbookState,
                results);
            ApplyWinConditionPressure(targetInfo, currentTick, macroState, results);
            ApplyModeClutchLogic(targetInfo, macroState, results);
            ApplyOpponentResourceAwareness(targetInfo, currentTick, results);
            ApplyAbilityThreatPrediction(targetInfo, currentTick, results);
            ApplyAdvancedTeamFightCoordination(
                targetInfo,
                currentTick,
                playbookState,
                results);
            ApplyRoleSpecificMacroBehavior(
                targetInfo,
                currentTick,
                macroState,
                playbookState,
                results);
            ApplyRoleMatchupBrain(
                targetInfo,
                currentTick,
                macroState,
                results);
            ApplyCoverPeekCombatPlanner(
                targetInfo,
                results);
            ApplyEngagementRiskAwareness(
                targetInfo,
                macroState,
                currentTick,
                results);
            ApplyPressureRotationAwareness(
                targetInfo,
                currentTick,
                results);
            ApplyAntiClumpSpacing(
                targetInfo,
                macroState,
                currentTick,
                results);
            ApplyTeamRoleCoordination(targetInfo, currentTick, results);
            ApplyPlaybookCoordination(targetInfo, playbookState, results);
            ApplyBrawlerTacticalIdentity(
                targetInfo,
                currentTick,
                macroState,
                results);
        }

        private AIGameModeMacroState ResolveMacroState()
        {
            AIGameModeMacroState state =
                AIGameModeMacroStrategy.ResolveCurrentMode(_self.Team);

            _lastMacroDebug = state.GetDebugSummary();
            return state;
        }

        private AITeamPlaybookState ResolvePlaybookState(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (_teamCoordinator == null)
            {
                _lastPlaybookDebug = "Playbook=None";
                return AITeamPlaybookState.None(currentTick);
            }

            AITeamPlaybookState state = _teamCoordinator.UpdatePlaybook(
                targetInfo,
                macroState,
                currentTick);

            _lastPlaybookDebug = _teamCoordinator.LastPlaybookDebug;
            return state;
        }

        private void ScoreAndReplace(ref AIActionScore best, AIActionScore candidate)
        {
            if (candidate.Score > best.Score)
                best = candidate;
        }

        private AIActionScore MakeScore(
    AIActionType actionType,
    float rawScore,
    float weight = 1f,
    bool allowEmergencyScore = false)
        {
            float weightedScore = rawScore * Mathf.Max(0f, weight);

            float maxScore = allowEmergencyScore
                ? MaxEmergencyActionScore
                : MaxNormalActionScore;

            return new AIActionScore(
                actionType,
                Mathf.Clamp(weightedScore, MinActionScore, maxScore));
        }

        private void ApplyTeamRoleCoordination(AITargetInfo targetInfo, uint currentTick, List<AIActionScore> results)
        {
            _lastTeamRoleDebug = "RoleCoord=Off";

            if (_profile == null ||
                !_profile.UseTeamRoleCoordination ||
                _teamCoordinator == null ||
                results == null ||
                results.Count == 0)
            {
                return;
            }

            int approachAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Approach, currentTick);
            int holdAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.HoldRange, currentTick);
            int repositionAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Reposition, currentTick);
            int peelAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Peel, currentTick);
            int regroupAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Regroup, currentTick);
            int objectiveAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Objective, currentTick);
            int searchAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(AIActionType.Search, currentTick);

            string deltaDebug = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateTeamRoleDelta(
                    actionScore,
                    targetInfo,
                    approachAllies,
                    peelAllies,
                    regroupAllies,
                    objectiveAllies,
                    searchAllies);

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);

                results[i] = new AIActionScore(actionScore.ActionType, adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{delta:+0.0;-0.0}");
            }

            _lastTeamRoleDebug =
                $"A={approachAllies} H={holdAllies} R={repositionAllies} " +
                $"P={peelAllies} G={regroupAllies} O={objectiveAllies} S={searchAllies}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastTeamRoleDebug += $" Delta={deltaDebug}";
        }

        private void ApplyPlaybookCoordination(
            AITargetInfo targetInfo,
            AITeamPlaybookState playbookState,
            List<AIActionScore> results)
        {
            if (!playbookState.IsActive || results == null || results.Count == 0)
                return;

            float weight = GetPlaybookWeight();
            if (weight <= 0f)
                return;

            string deltaDebug = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculatePlaybookDelta(actionScore.ActionType, playbookState);

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreatePlaybookScore(actionScore.ActionType, targetInfo, playbookState))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta * weight * playbookState.Urgency);

                results[i] = new AIActionScore(actionScore.ActionType, adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{adjustedScore - actionScore.Score:+0.0;-0.0}");
            }

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastPlaybookDebug += $" Delta={deltaDebug}";
        }

        private void ApplyBrawlerTacticalIdentity(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState,
            List<AIActionScore> results)
        {
            _lastBrawlerIdentityDebug = "Identity=None";

            if (_profile == null ||
                _self == null ||
                results == null ||
                results.Count == 0)
            {
                return;
            }

            AIBrawlerTacticalIdentityContext context =
                BuildBrawlerTacticalIdentityContext(
                    targetInfo,
                    currentTick,
                    macroState);

            if (context.Identity == BrawlerTacticalIdentity.Auto)
                return;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                AIBrawlerTacticalIdentityEvaluation evaluation =
                    AIBrawlerTacticalIdentityUtility.EvaluateAction(
                        actionScore.ActionType,
                        _profile,
                        context);

                if (!evaluation.HasDelta)
                    continue;

                if (actionScore.Score <= 0f &&
                    evaluation.Delta > 0f &&
                    !CanCreateBrawlerIdentityScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + evaluation.Delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(actionScore.ActionType, adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}:{evaluation.Reason}");
            }

            _lastBrawlerIdentityDebug =
                $"Identity={AIBrawlerTacticalIdentityUtility.GetIdentityLabel(context.Identity)} " +
                $"discipline={AIBrawlerTacticalIdentityUtility.GetDiscipline(_profile):0.00} " +
                $"dist={context.TargetDistance:0.0} " +
                $"range={context.OwnAttackRange:0.0}/{context.TargetAttackRange:0.0} " +
                $"cover self:{context.SelfNearCover} between:{context.HasCoverBetween} " +
                $"cluster:{context.EnemyClusterCount} allyLow:{context.WoundedAllyCount}/{context.CriticalAllyCount} " +
                $"super:{context.SuperReady}/{context.SuperCanReachTarget}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastBrawlerIdentityDebug += $" Delta={deltaDebug}";
        }

        private AIBrawlerTacticalIdentityContext BuildBrawlerTacticalIdentityContext(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            BrawlerTacticalIdentity identity =
                AIBrawlerTacticalIdentityUtility.ResolveIdentity(
                    _self != null ? _self.Definition : null);
            bool hasTarget =
                targetInfo != null &&
                targetInfo.HasLiveTarget &&
                SpatialEntityUtility.IsAlive(targetInfo.Target);
            float ownRange = Mathf.Max(1f, GetAbilityMaxRange());
            float ownIdealRange = Mathf.Max(1f, GetAbilityIdealRange());
            float preferredRange = _profile.GetPreferredAttackRange(ownIdealRange);
            float tooClose = _profile.GetTooCloseDistance(ownIdealRange);
            float targetDistance = 0f;
            float targetRange = 6f;
            float targetHealthRatio = 1f;
            int targetCarriedGems = 0;
            int enemyClusterCount = 0;
            bool targetNearCover = false;
            bool hasCoverBetween = false;

            if (hasTarget)
            {
                targetDistance = Vector3.Distance(_self.Position, targetInfo.Target.Position);
                enemyClusterCount = CountEnemyBrawlersNear(
                    targetInfo.Target.Position,
                    Mathf.Max(2.75f, ownRange * 0.45f));
                hasCoverBetween = AIMapNavigationUtility.HasCoverBetween(
                    SimulationClock.Pathfinder,
                    _self.Position,
                    targetInfo.Target.Position);
                targetNearCover = HasNearbyMapCover(targetInfo.Target.Position);

                if (targetInfo.Target is BrawlerController targetBrawler)
                {
                    targetRange = Mathf.Max(1f, GetTargetAbilityMaxRange(targetBrawler));
                    if (targetBrawler.State != null)
                    {
                        targetHealthRatio = Mathf.Clamp01(
                            targetBrawler.State.CurrentHealth /
                            Mathf.Max(1f, targetBrawler.State.MaxHealth.Value));
                        targetCarriedGems = targetBrawler.State.CarriedGemCount;
                    }
                }
            }

            CountAllyHealthPressure(
                Mathf.Max(5f, ownRange * 0.75f),
                out int woundedAllies,
                out int criticalAllies);

            bool superReady =
                _self.State != null &&
                _self.State.SuperCharge != null &&
                _self.State.SuperCharge.IsReady;

            return new AIBrawlerTacticalIdentityContext(
                identity,
                _profile.Difficulty,
                _profile.Personality,
                hasTarget,
                targetDistance,
                ownRange,
                preferredRange,
                tooClose,
                targetRange,
                targetHealthRatio,
                targetCarriedGems,
                enemyClusterCount,
                woundedAllies,
                criticalAllies,
                superReady,
                hasTarget && CanUseSuperToCloseGap(targetDistance),
                HasNearbyMapCover(_self.Position),
                targetNearCover,
                hasCoverBetween,
                IsCurrentMainAttackDirectFire(),
                IsObjectivePressure(macroState),
                macroState.IsBehind,
                macroState.EnemyTeamHasCountdown);
        }

        private bool CanCreateBrawlerIdentityScore(
            AIActionType actionType,
            in AIBrawlerTacticalIdentityContext context)
        {
            switch (actionType)
            {
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                case AIActionType.Approach:
                case AIActionType.UseSuper:
                    return context.HasTarget;

                case AIActionType.Peel:
                case AIActionType.Regroup:
                    return context.CriticalAllyCount > 0 ||
                           context.WoundedAllyCount > 0 ||
                           context.Behind;

                case AIActionType.Search:
                case AIActionType.Objective:
                    return !context.HasTarget || context.ObjectivePressure;

                default:
                    return false;
            }
        }

        private static bool IsObjectivePressure(AIGameModeMacroState macroState)
        {
            return macroState.Phase == AIGameModeObjectivePhase.Opening ||
                   macroState.Phase == AIGameModeObjectivePhase.Contest ||
                   macroState.Phase == AIGameModeObjectivePhase.Countdown ||
                   macroState.Phase == AIGameModeObjectivePhase.FinalPressure ||
                   macroState.Call == AIGameModeMacroCall.Push ||
                   macroState.Call == AIGameModeMacroCall.Hold ||
                   macroState.Call == AIGameModeMacroCall.Reset;
        }

        private int CountEnemyBrawlersNear(Vector3 position, float radius)
        {
            if (SimulationClock.Grid == null)
                return 0;

            _nearbyAllyBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                position,
                Mathf.Max(0.1f, radius),
                _nearbyAllyBuffer);

            int count = 0;
            for (int i = 0; i < _nearbyAllyBuffer.Count; i++)
            {
                ISpatialEntity entity = _nearbyAllyBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) ||
                    entity.Team == _self.Team ||
                    !(entity is BrawlerController brawler) ||
                    brawler.State == null ||
                    brawler.State.IsDead)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private void CountAllyHealthPressure(
            float radius,
            out int woundedAllyCount,
            out int criticalAllyCount)
        {
            woundedAllyCount = 0;
            criticalAllyCount = 0;

            if (SimulationClock.Grid == null ||
                _self == null)
            {
                return;
            }

            _nearbyAllyBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                _self.Position,
                Mathf.Max(0.1f, radius),
                _nearbyAllyBuffer);

            for (int i = 0; i < _nearbyAllyBuffer.Count; i++)
            {
                ISpatialEntity entity = _nearbyAllyBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) ||
                    entity.Team != _self.Team ||
                    !(entity is BrawlerController ally) ||
                    ally.State == null ||
                    ally.State.IsDead)
                {
                    continue;
                }

                float healthRatio = Mathf.Clamp01(
                    ally.State.CurrentHealth /
                    Mathf.Max(1f, ally.State.MaxHealth.Value));
                if (healthRatio <= 0.30f)
                    criticalAllyCount++;
                else if (healthRatio <= 0.55f)
                    woundedAllyCount++;
            }
        }

        private static bool HasNearbyMapCover(Vector3 position)
        {
            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
                return false;

            Vector2Int coords = pathfinder.GetGridCoords(position);
            for (int x = coords.x - 1; x <= coords.x + 1; x++)
            {
                for (int y = coords.y - 1; y <= coords.y + 1; y++)
                {
                    if (x == coords.x && y == coords.y)
                        continue;

                    if (x < 0 || x >= pathfinder.Width || y < 0 || y >= pathfinder.Height)
                        continue;

                    if (!pathfinder.IsWalkable(new Vector2Int(x, y)))
                        return true;
                }
            }

            return false;
        }

        private void ApplyObjectiveIntentArbitration(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState,
            AITeamPlaybookState playbookState,
            List<AIActionScore> results)
        {
            _lastObjectiveIntentDebug = "ObjIntent=None";

            if (results == null || results.Count == 0)
                return;

            bool hasLiveTarget = targetInfo != null && targetInfo.HasLiveTarget;
            bool hasGemPickup = false;
            bool shouldPickupGem = false;
            float gemPickupScore = 0f;

            if (!hasLiveTarget &&
                TryResolveGemPickupDecision(
                    macroState,
                    currentTick,
                    out AIGemPickupDecision gemDecision))
            {
                hasGemPickup = gemDecision.HasPickup;
                shouldPickupGem = gemDecision.ShouldPickup;
                gemPickupScore = gemDecision.Score;
            }

            bool hasLaneHold = CanHoldAssignedLane(
                currentTick,
                out _);
            bool isCarrierPlaybook =
                playbookState.IsActive &&
                playbookState.Call == AITeamPlaybookCall.EscortCarrier;
            bool selfIsCarrierAnchor =
                _self != null &&
                playbookState.CarrierEntityId == _self.EntityID &&
                playbookState.EscortRole == AITeamEscortFormationRole.CarrierAnchor;

            var context = new AIObjectiveIntentContext(
                macroState,
                _self != null && _self.State != null
                    ? _self.State.CarriedGemCount
                    : 0,
                hasLiveTarget,
                hasLaneHold,
                hasGemPickup,
                shouldPickupGem,
                gemPickupScore,
                isCarrierPlaybook,
                selfIsCarrierAnchor);

            string deltaDebug = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                AIObjectiveIntentArbitrationResult intent =
                    AIObjectiveIntentArbitrationUtility.Evaluate(
                        actionScore.ActionType,
                        context);

                if (!intent.HasDelta)
                    continue;

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + intent.Delta);

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{adjustedScore - actionScore.Score:+0.0;-0.0}_{intent.Reason}");
            }

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastObjectiveIntentDebug = $"ObjIntent={deltaDebug}";
        }

        private void ApplyWinConditionPressure(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState,
            List<AIActionScore> results)
        {
            _lastWinConditionDebug = "Win=None";

            if (results == null || results.Count == 0)
                return;

            bool hasLiveTarget = targetInfo != null && targetInfo.HasLiveTarget;
            int selfCarriedGems = _self != null && _self.State != null
                ? _self.State.CarriedGemCount
                : 0;
            int targetCarriedGems = 0;
            float targetHealthRatio = 1f;

            if (hasLiveTarget &&
                targetInfo.Target is BrawlerController targetBrawler &&
                targetBrawler.State != null)
            {
                targetCarriedGems = targetBrawler.State.CarriedGemCount;
                targetHealthRatio = Mathf.Clamp01(
                    targetBrawler.State.CurrentHealth /
                    Mathf.Max(1f, targetBrawler.State.MaxHealth.Value));
            }

            var context = new AIWinConditionActionContext(
                macroState,
                selfCarriedGems,
                targetCarriedGems,
                targetHealthRatio,
                hasLiveTarget);

            string deltaDebug = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                AIWinConditionActionEvaluation evaluation =
                    AIWinConditionUtility.EvaluateAction(
                        actionScore.ActionType,
                        context);

                if (!evaluation.HasDelta)
                    continue;

                if (actionScore.Score <= 0f &&
                    evaluation.Delta > 0f &&
                    !CanCreateWinConditionScore(
                        actionScore.ActionType,
                        context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + evaluation.Delta);

                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}_{evaluation.Reason}");
            }

            if (!string.IsNullOrEmpty(deltaDebug))
            {
                _lastWinConditionDebug = $"Win={deltaDebug}";
                AIValidationGauntlet.RecordSignal(
                    AIValidationGauntletSignal.ModeObjectivePriority,
                    currentTick);
            }
        }

        private void ApplyOpponentResourceAwareness(
            AITargetInfo targetInfo,
            uint currentTick,
            List<AIActionScore> results)
        {
            _lastResourceAwarenessDebug = "ResAware=None";

            if (_profile == null ||
                results == null ||
                results.Count == 0 ||
                targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null)
            {
                return;
            }

            AIOpponentResourceSnapshot snapshot =
                AIOpponentResourceUtility.Evaluate(targetBrawler, currentTick);
            if (!snapshot.HasTarget)
                return;

            float awareness = Mathf.Max(0f, _profile.OpponentResourceAwarenessWeight);
            if (awareness <= 0.01f)
            {
                _lastResourceAwarenessDebug = $"{snapshot.GetDebugSummary()} weight=0";
                return;
            }

            float ammoOpportunity =
                snapshot.AmmoPressure * _profile.EnemyLowAmmoOpportunityBonus;
            if (!snapshot.CanUseMainAttack || snapshot.AvailableAmmo <= 0)
                ammoOpportunity += _profile.EnemyNoAttackApproachBonus;

            float superThreat = 0f;
            if (snapshot.SuperReady)
                superThreat = _profile.EnemySuperReadyThreatPenalty;
            else if (snapshot.SuperChargePercent >= 0.75f)
                superThreat = _profile.EnemyNearlySuperThreatPenalty * snapshot.SuperChargePercent;

            float superRespect =
                snapshot.SuperReady
                    ? _profile.EnemySuperRespectBonus
                    : snapshot.SuperChargePercent >= 0.75f
                        ? _profile.EnemySuperRespectBonus * 0.55f * snapshot.SuperChargePercent
                        : 0f;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = GetResourceAwarenessDelta(
                    actionScore.ActionType,
                    ammoOpportunity,
                    superThreat,
                    superRespect);

                delta *= awareness;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateResourceAwarenessScore(actionScore.ActionType))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastResourceAwarenessDebug =
                $"{snapshot.GetDebugSummary()} weight={awareness:0.00} " +
                $"ammoOpp={ammoOpportunity:0.0} superThreat={superThreat:0.0}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastResourceAwarenessDebug += $" Delta={deltaDebug}";
        }

        private static float GetResourceAwarenessDelta(
            AIActionType actionType,
            float ammoOpportunity,
            float superThreat,
            float superRespect)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return ammoOpportunity - superThreat;

                case AIActionType.HoldRange:
                    return superRespect - ammoOpportunity * 0.20f;

                case AIActionType.Reposition:
                    return superRespect * 0.85f - ammoOpportunity * 0.10f;

                case AIActionType.Retreat:
                    return superRespect * 0.45f;

                case AIActionType.Evade:
                    return superRespect * 0.35f;

                case AIActionType.UseSuper:
                    return ammoOpportunity * 0.30f + superThreat * 0.25f;

                case AIActionType.Objective:
                case AIActionType.Search:
                    return ammoOpportunity * 0.18f;

                default:
                    return 0f;
            }
        }

        private static bool CanCreateResourceAwarenessScore(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                case AIActionType.Retreat:
                case AIActionType.Evade:
                    return true;

                default:
                    return false;
            }
        }

        private void ApplyAbilityThreatPrediction(
            AITargetInfo targetInfo,
            uint currentTick,
            List<AIActionScore> results)
        {
            _lastAbilityThreatDebug = "ThreatPred=None";

            if (_profile == null ||
                results == null ||
                results.Count == 0 ||
                targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null ||
                _self == null ||
                _self.State == null)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.AbilityThreatPredictionWeight);
            if (weight <= 0.01f)
            {
                _lastAbilityThreatDebug = "ThreatPred=Off";
                return;
            }

            AIAbilityThreatContext context = BuildAbilityThreatContext(
                targetBrawler,
                currentTick);
            if (!context.HasThreatSignal)
                return;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateAbilityThreatDelta(
                    actionScore.ActionType,
                    context);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateAbilityThreatScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastAbilityThreatDebug =
                $"ThreatPred=super:{context.SuperThreatPressure:0.00} " +
                $"area:{context.AreaThreatPressure:0.00} fire:{context.FiringWindowPressure:0.00} " +
                $"danger:{context.PendingDangerPressure:0.00} total:{context.TotalThreatPressure:0.00} " +
                $"cover:{context.HasCoverBetween} high:{context.HighThreat} punish:{context.CanPunish} " +
                $"type:{context.MainAreaThreat}/{context.SuperAreaThreat}/{context.DirectFireThreat} " +
                $"range:{context.OwnRange:0.0}/{context.TargetRange:0.0} " +
                $"dist:{context.TargetDistance:0.0} w:{weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastAbilityThreatDebug += $" Delta={deltaDebug}";
        }

        private AIAbilityThreatContext BuildAbilityThreatContext(
            BrawlerController target,
            uint currentTick)
        {
            AIOpponentResourceSnapshot resources =
                AIOpponentResourceUtility.Evaluate(target, currentTick);
            AbilityDefinition mainAttack =
                target.Definition != null ? target.Definition.MainAttack : null;
            AbilityDefinition superAbility =
                target.Definition != null ? target.Definition.SuperAbility : null;
            BrawlerArchetype targetArchetype =
                target.Definition != null ? target.Definition.Archetype : BrawlerArchetype.Fighter;

            float targetDistance = Vector3.Distance(_self.Position, target.Position);
            float ownRange = Mathf.Max(1f, GetAbilityMaxRange());
            float targetRange = Mathf.Max(1f, GetTargetAbilityMaxRange(target));
            float respectRange = Mathf.Max(
                _profile.PredictedThreatRespectRange,
                targetRange + _profile.AttackRangeBuffer);
            float distancePressure = 1f - Mathf.Clamp01(targetDistance / respectRange);
            bool inThreatRange = targetDistance <= respectRange;
            bool hasCoverBetween =
                SimulationClock.Pathfinder != null &&
                AIMapNavigationUtility.HasCoverBetween(
                    SimulationClock.Pathfinder,
                    _self.Position,
                    target.Position);
            bool targetAreaRole =
                targetArchetype == BrawlerArchetype.Controller ||
                targetArchetype == BrawlerArchetype.Artillery;
            bool mainAreaThreat = IsAreaThreatAbility(mainAttack) || targetAreaRole;
            bool superAreaThreat = IsAreaThreatAbility(superAbility);
            bool directFireThreat =
                mainAttack != null &&
                mainAttack.DeliveryType == AbilityDeliveryType.Projectile &&
                !hasCoverBetween;

            float superThreat = 0f;
            if (resources.SuperReady && resources.CanUseSuper)
                superThreat = 0.72f + distancePressure * 0.45f;
            else if (resources.SuperChargePercent >= 0.75f)
                superThreat = (resources.SuperChargePercent - 0.65f) * 0.95f;

            if (superAreaThreat && superThreat > 0f)
                superThreat *= 1.15f;

            float areaThreat = 0f;
            if (mainAreaThreat && inThreatRange)
                areaThreat += 0.28f + distancePressure * 0.45f;

            if (superAreaThreat && superThreat > 0f)
                areaThreat += superThreat * 0.55f;

            float firingThreat = 0f;
            if (resources.CanUseMainAttack &&
                resources.AvailableAmmo > 0 &&
                targetDistance <= targetRange + _profile.AttackRangeBuffer)
            {
                firingThreat =
                    (directFireThreat ? 0.35f : 0.18f) +
                    distancePressure * 0.30f;
            }

            float pendingDanger = 0f;
            if (_dangerMemory != null && _dangerMemory.HasDanger)
            {
                float threshold = Mathf.Max(0.01f, _profile.DangerEvadePressureThreshold);
                pendingDanger = Mathf.Clamp01(_dangerMemory.Pressure / threshold) * 0.45f;
            }

            float totalThreat = Mathf.Clamp01(
                superThreat * 0.55f +
                areaThreat * 0.35f +
                firingThreat * 0.25f +
                pendingDanger);
            bool targetLow =
                target.State.CurrentHealth /
                Mathf.Max(1f, target.State.MaxHealth.Value) <=
                Mathf.Max(0.22f, _profile.FinisherHealthThreshold);
            bool selfCanCounter =
                _self.State.SuperCharge != null &&
                _self.State.SuperCharge.IsReady &&
                targetDistance <= ownRange + _profile.AttackRangeBuffer;

            return new AIAbilityThreatContext
            {
                HasThreatSignal =
                    totalThreat >= 0.18f &&
                    (inThreatRange || pendingDanger > 0f),
                HighThreat =
                    superThreat >= 0.55f ||
                    areaThreat >= 0.60f ||
                    pendingDanger >= 0.35f,
                CanPunish = targetLow || selfCanCounter,
                HasCoverBetween = hasCoverBetween,
                MainAreaThreat = mainAreaThreat,
                SuperAreaThreat = superAreaThreat,
                DirectFireThreat = directFireThreat,
                TargetDistance = targetDistance,
                TargetRange = targetRange,
                OwnRange = ownRange,
                SuperThreatPressure = Mathf.Clamp01(superThreat),
                AreaThreatPressure = Mathf.Clamp01(areaThreat),
                FiringWindowPressure = Mathf.Clamp01(firingThreat),
                PendingDangerPressure = Mathf.Clamp01(pendingDanger),
                TotalThreatPressure = totalThreat
            };
        }

        private float CalculateAbilityThreatDelta(
            AIActionType actionType,
            AIAbilityThreatContext context)
        {
            float total = Mathf.Clamp01(context.TotalThreatPressure);
            float superPenalty =
                _profile.PredictedSuperThreatPenalty * context.SuperThreatPressure;
            float areaBonus =
                _profile.PredictedAreaThreatRepositionBonus *
                Mathf.Max(context.AreaThreatPressure, context.PendingDangerPressure * 0.75f);
            float evadeBonus = _profile.PredictedThreatEvadeBonus * total;
            float holdBonus = _profile.PredictedThreatHoldBonus * total;
            float punishFactor = context.CanPunish ? 0.45f : 1f;

            switch (actionType)
            {
                case AIActionType.Approach:
                    return -(superPenalty * 0.85f + areaBonus * 0.55f) * punishFactor;

                case AIActionType.Reposition:
                    return areaBonus +
                           superPenalty * 0.35f +
                           (context.HasCoverBetween ? 0f : evadeBonus * 0.45f);

                case AIActionType.HoldRange:
                    return holdBonus +
                           (context.HasCoverBetween ? holdBonus * 0.35f : 0f) -
                           (context.HighThreat && !context.CanPunish ? superPenalty * 0.12f : 0f);

                case AIActionType.Evade:
                    return context.HighThreat
                        ? evadeBonus * 1.15f
                        : evadeBonus * 0.45f;

                case AIActionType.Retreat:
                    return context.HighThreat && !context.CanPunish
                        ? evadeBonus * 0.45f
                        : 0f;

                case AIActionType.UseSuper:
                    return context.CanPunish
                        ? (superPenalty + areaBonus) * 0.18f
                        : 0f;

                case AIActionType.Objective:
                case AIActionType.Search:
                    return context.HighThreat && !context.HasCoverBetween
                        ? -areaBonus * 0.25f
                        : 0f;

                default:
                    return 0f;
            }
        }

        private static bool CanCreateAbilityThreatScore(
            AIActionType actionType,
            AIAbilityThreatContext context)
        {
            switch (actionType)
            {
                case AIActionType.Reposition:
                case AIActionType.HoldRange:
                    return true;

                case AIActionType.Evade:
                case AIActionType.Retreat:
                    return context.HighThreat;

                case AIActionType.UseSuper:
                    return context.CanPunish;

                default:
                    return false;
            }
        }

        private static bool IsAreaThreatAbility(AbilityDefinition ability)
        {
            if (ability == null)
                return false;

            return ability.DeliveryType == AbilityDeliveryType.Area ||
                   ability.TargetingType == AbilityTargetingType.Area ||
                   ability.Intent == AbilityIntentType.AreaControl ||
                   ability.HasTag(AbilityTag.AoE) ||
                   ability is AoEAbilityDefinition ||
                   ability is MinefieldAbilityDefinition ||
                   ability is LeapAbilityDefinition;
        }

        private void ApplyAdvancedTeamFightCoordination(
            AITargetInfo targetInfo,
            uint currentTick,
            AITeamPlaybookState playbookState,
            List<AIActionScore> results)
        {
            _lastTeamFightDebug = "TeamFight=None";

            if (_profile == null ||
                _teamCoordinator == null ||
                results == null ||
                results.Count == 0)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.TeamFightCoordinationWeight);
            if (weight <= 0.01f)
                return;

            BrawlerController targetBrawler = null;
            bool hasTarget = false;
            if (targetInfo != null &&
                targetInfo.HasLiveTarget &&
                targetInfo.Target is BrawlerController candidateTarget &&
                candidateTarget.State != null)
            {
                targetBrawler = candidateTarget;
                hasTarget = true;
            }
            float targetHealthRatio = 1f;
            int alliedFocusCount = 0;
            bool targetIsVulnerable = false;

            if (hasTarget)
            {
                targetHealthRatio = targetBrawler.State.CurrentHealth /
                                    Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
                alliedFocusCount = AITeamBlackboard.GetTargetFocusCountExcluding(
                    _self.Team,
                    targetBrawler.EntityID,
                    _self.EntityID);
                targetIsVulnerable =
                    targetHealthRatio <= _profile.LowHealthChaseHealthThreshold ||
                    targetBrawler.State.HasStatus(StatusEffectType.Stun) ||
                    targetBrawler.State.HasStatus(StatusEffectType.Slow);
            }

            bool allyUnderThreat =
                _teamCoordinator.TryGetAllyUnderThreat(
                    currentTick,
                    out BrawlerController threatenedAlly) &&
                threatenedAlly != null &&
                threatenedAlly != _self;
            bool baitAndCollapse =
                playbookState.IsActive &&
                playbookState.Call == AITeamPlaybookCall.BaitAndCollapse;
            bool overCommittedApproach =
                hasTarget &&
                alliedFocusCount >= Mathf.Max(1, _profile.MaxTeamApproachers) &&
                !targetIsVulnerable;
            bool collapseWindow =
                hasTarget &&
                alliedFocusCount > 0 &&
                targetIsVulnerable;

            if (!hasTarget && !allyUnderThreat && !baitAndCollapse)
                return;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = GetTeamFightDelta(
                    actionScore.ActionType,
                    collapseWindow,
                    overCommittedApproach,
                    allyUnderThreat,
                    baitAndCollapse);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateTeamFightScore(actionScore.ActionType, hasTarget, allyUnderThreat))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastTeamFightDebug =
                $"TeamFight=focus:{alliedFocusCount} hp:{targetHealthRatio:0.00} " +
                $"collapse:{collapseWindow} over:{overCommittedApproach} " +
                $"peel:{allyUnderThreat} bait:{baitAndCollapse} w:{weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastTeamFightDebug += $" Delta={deltaDebug}";
        }

        private float GetTeamFightDelta(
            AIActionType actionType,
            bool collapseWindow,
            bool overCommittedApproach,
            bool allyUnderThreat,
            bool baitAndCollapse)
        {
            float delta = 0f;

            if (collapseWindow)
            {
                switch (actionType)
                {
                    case AIActionType.Approach:
                        delta += _profile.TeamCollapseFocusBonus;
                        break;

                    case AIActionType.UseSuper:
                        delta += _profile.TeamCollapseFocusBonus * 0.55f;
                        break;

                    case AIActionType.HoldRange:
                        delta += _profile.TeamBaitHoldBonus * 0.50f;
                        break;
                }
            }

            if (overCommittedApproach)
            {
                switch (actionType)
                {
                    case AIActionType.Approach:
                        delta -= _profile.TeamOvercommitApproachPenalty;
                        break;

                    case AIActionType.Reposition:
                        delta += _profile.TeamFlankRepositionBonus;
                        break;

                    case AIActionType.HoldRange:
                        delta += _profile.TeamBaitHoldBonus;
                        break;
                }
            }

            if (allyUnderThreat)
            {
                switch (actionType)
                {
                    case AIActionType.Peel:
                        delta += _profile.TeamPeelAssistBonus;
                        break;

                    case AIActionType.Reposition:
                        delta += _profile.TeamPeelAssistBonus * 0.35f;
                        break;

                    case AIActionType.Approach:
                        delta += _profile.TeamPeelAssistBonus * 0.25f;
                        break;
                }
            }

            if (baitAndCollapse)
            {
                switch (actionType)
                {
                    case AIActionType.HoldRange:
                        delta += _profile.TeamBaitHoldBonus;
                        break;

                    case AIActionType.Reposition:
                        delta += _profile.TeamFlankRepositionBonus;
                        break;

                    case AIActionType.Approach:
                        delta += _profile.TeamCollapseFocusBonus * 0.45f;
                        break;
                }
            }

            return delta;
        }

        private static bool CanCreateTeamFightScore(
            AIActionType actionType,
            bool hasTarget,
            bool allyUnderThreat)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                    return hasTarget;

                case AIActionType.Peel:
                    return allyUnderThreat;

                default:
                    return false;
            }
        }

        private void ApplyRoleSpecificMacroBehavior(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState,
            AITeamPlaybookState playbookState,
            List<AIActionScore> results)
        {
            _lastRoleMacroDebug = "RoleMacro=None";

            if (_profile == null ||
                results == null ||
                results.Count == 0)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.RoleMacroBehaviorWeight);
            if (weight <= 0.01f)
            {
                _lastRoleMacroDebug = "RoleMacro=Off";
                return;
            }

            AIRoleMacroContext context = BuildRoleMacroContext(
                targetInfo,
                currentTick,
                macroState,
                playbookState);

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateRoleMacroDelta(
                    actionScore.ActionType,
                    context);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateRoleMacroScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);

                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastRoleMacroDebug =
                $"RoleMacro={context.Archetype} " +
                $"macro={macroState.Call}/{macroState.Phase} " +
                $"play={playbookState.Call} " +
                $"pick={context.TargetPickWindow} " +
                $"value={context.HighValueTarget} " +
                $"carrier={context.SelfCarriedGems} " +
                $"peel={context.AllyUnderThreat} " +
                $"w={weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastRoleMacroDebug += $" Delta={deltaDebug}";
        }

        private AIRoleMacroContext BuildRoleMacroContext(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState,
            AITeamPlaybookState playbookState)
        {
            AIRoleMacroContext context = new AIRoleMacroContext
            {
                Archetype = _self != null && _self.Definition != null
                    ? _self.Definition.Archetype
                    : _profile.Archetype,
                SelfCarriedGems = _self != null && _self.State != null
                    ? _self.State.CarriedGemCount
                    : 0,
                MacroPush = macroState.Call == AIGameModeMacroCall.Push ||
                            playbookState.Call == AITeamPlaybookCall.Push ||
                            playbookState.Call == AITeamPlaybookCall.Engage ||
                            playbookState.Call == AITeamPlaybookCall.PinchPressure ||
                            playbookState.Call == AITeamPlaybookCall.BaitAndCollapse,
                MacroHold = macroState.Call == AIGameModeMacroCall.Hold ||
                            playbookState.Call == AITeamPlaybookCall.Hold ||
                            playbookState.Call == AITeamPlaybookCall.EscortCarrier,
                MacroReset = macroState.Call == AIGameModeMacroCall.Reset ||
                             playbookState.Call == AITeamPlaybookCall.Disengage ||
                             playbookState.Call == AITeamPlaybookCall.Reset,
                ObjectivePressure = macroState.Phase == AIGameModeObjectivePhase.Opening ||
                                    macroState.Phase == AIGameModeObjectivePhase.Contest ||
                                    macroState.Phase == AIGameModeObjectivePhase.Countdown ||
                                    macroState.Phase == AIGameModeObjectivePhase.FinalPressure ||
                                    playbookState.HasAnchorPoint ||
                                    playbookState.HasPressurePoint ||
                                    playbookState.HasEscortTargetPoint
            };

            if (targetInfo != null &&
                targetInfo.HasLiveTarget &&
                targetInfo.Target is BrawlerController targetBrawler &&
                targetBrawler.State != null)
            {
                context.HasTarget = true;
                context.TargetHealthRatio = Mathf.Clamp01(
                    targetBrawler.State.CurrentHealth /
                    Mathf.Max(1f, targetBrawler.State.MaxHealth.Value));
                context.TargetCarriedGems = targetBrawler.State.CarriedGemCount;
                context.TargetDisabled =
                    targetBrawler.State.HasStatus(StatusEffectType.Stun) ||
                    targetBrawler.State.HasStatus(StatusEffectType.Slow);
                context.TargetDistance = _self != null
                    ? Vector3.Distance(_self.Position, targetBrawler.Position)
                    : 0f;
            }
            else
            {
                context.TargetHealthRatio = 1f;
                context.TargetDistance = 0f;
            }

            context.HighValueTarget =
                context.TargetCarriedGems >= 3 ||
                (macroState.EnemyTeamHasCountdown &&
                 context.TargetCarriedGems > 0);
            context.TargetPickWindow =
                context.HasTarget &&
                (context.TargetHealthRatio <= Mathf.Max(0.22f, _profile.LowHealthChaseHealthThreshold) ||
                 context.TargetDisabled ||
                 context.HighValueTarget);
            context.SelfCarrier =
                context.SelfCarriedGems > 0;
            context.CarrierSafety =
                context.SelfCarrier &&
                (macroState.OwnTeamHasCountdown ||
                 context.MacroHold ||
                 context.MacroReset);

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetAllyUnderThreat(currentTick, out BrawlerController threatenedAlly) &&
                threatenedAlly != null &&
                threatenedAlly != _self)
            {
                context.AllyUnderThreat = true;
            }

            return context;
        }

        private float CalculateRoleMacroDelta(
            AIActionType actionType,
            AIRoleMacroContext context)
        {
            float delta = 0f;

            if (context.CarrierSafety)
                delta += GetCarrierSafetyRoleDelta(actionType);

            switch (context.Archetype)
            {
                case BrawlerArchetype.Tank:
                    delta += GetTankRoleMacroDelta(actionType, context);
                    break;

                case BrawlerArchetype.Assassin:
                    delta += GetAssassinRoleMacroDelta(actionType, context);
                    break;

                case BrawlerArchetype.Sniper:
                    delta += GetBacklineRoleMacroDelta(
                        actionType,
                        context,
                        _profile.RoleBacklineAnchorMacroBonus,
                        "sniper");
                    break;

                case BrawlerArchetype.Support:
                    delta += GetSupportRoleMacroDelta(actionType, context);
                    break;

                case BrawlerArchetype.Controller:
                    delta += GetControllerRoleMacroDelta(actionType, context);
                    break;

                case BrawlerArchetype.Artillery:
                    delta += GetArtilleryRoleMacroDelta(actionType, context);
                    break;

                case BrawlerArchetype.Fighter:
                default:
                    delta += GetFighterRoleMacroDelta(actionType, context);
                    break;
            }

            return delta;
        }

        private float GetCarrierSafetyRoleDelta(AIActionType actionType)
        {
            float bonus = Mathf.Max(0f, _profile.RoleSupportPeelMacroBonus);

            switch (actionType)
            {
                case AIActionType.Retreat:
                    return bonus * 0.45f;
                case AIActionType.Regroup:
                    return bonus * 0.55f;
                case AIActionType.HoldRange:
                    return bonus * 0.35f;
                case AIActionType.Reposition:
                    return bonus * 0.30f;
                case AIActionType.Approach:
                    return -bonus * 0.55f;
                case AIActionType.Search:
                case AIActionType.Objective:
                    return -bonus * 0.30f;
                default:
                    return 0f;
            }
        }

        private float GetTankRoleMacroDelta(
            AIActionType actionType,
            AIRoleMacroContext context)
        {
            float bonus = Mathf.Max(0f, _profile.RoleTankSpaceCreationBonus);
            float delta = 0f;

            if (context.ObjectivePressure || context.MacroPush || context.MacroHold)
            {
                switch (actionType)
                {
                    case AIActionType.Objective:
                        delta += bonus;
                        break;
                    case AIActionType.Approach:
                        delta += bonus * 0.70f;
                        break;
                    case AIActionType.Peel:
                        delta += bonus * 0.45f;
                        break;
                    case AIActionType.HoldRange:
                        delta -= bonus * 0.30f;
                        break;
                }
            }

            if (context.MacroReset || context.AllyUnderThreat)
            {
                switch (actionType)
                {
                    case AIActionType.Peel:
                        delta += bonus * 0.80f;
                        break;
                    case AIActionType.Regroup:
                        delta += bonus * 0.45f;
                        break;
                    case AIActionType.Reposition:
                        delta += bonus * 0.35f;
                        break;
                }
            }

            if (context.TargetPickWindow && actionType == AIActionType.Approach)
                delta += bonus * 0.40f;

            return delta;
        }

        private float GetAssassinRoleMacroDelta(
            AIActionType actionType,
            AIRoleMacroContext context)
        {
            float bonus = Mathf.Max(0f, _profile.RoleAssassinPickPressureBonus);

            if (context.TargetPickWindow)
            {
                switch (actionType)
                {
                    case AIActionType.Approach:
                        return bonus;
                    case AIActionType.UseSuper:
                        return bonus * 0.60f;
                    case AIActionType.Reposition:
                        return bonus * 0.30f;
                    case AIActionType.Objective:
                        return -bonus * 0.25f;
                }
            }

            if (!context.HasTarget || context.MacroHold || context.MacroReset)
            {
                switch (actionType)
                {
                    case AIActionType.Reposition:
                        return bonus * 0.55f;
                    case AIActionType.Search:
                        return bonus * 0.35f;
                    case AIActionType.HoldRange:
                        return bonus * 0.25f;
                    case AIActionType.Approach:
                        return -bonus * 0.45f;
                }
            }

            return context.HighValueTarget && actionType == AIActionType.Approach
                ? bonus * 0.45f
                : 0f;
        }

        private float GetBacklineRoleMacroDelta(
            AIActionType actionType,
            AIRoleMacroContext context,
            float bonus,
            string roleLabel)
        {
            bonus = Mathf.Max(0f, bonus);
            float delta = 0f;

            if (context.MacroHold || context.MacroReset || context.ObjectivePressure)
            {
                switch (actionType)
                {
                    case AIActionType.HoldRange:
                        delta += bonus;
                        break;
                    case AIActionType.Reposition:
                        delta += bonus * 0.60f;
                        break;
                    case AIActionType.Approach:
                        delta -= bonus * (roleLabel == "artillery" ? 0.65f : 0.50f);
                        break;
                    case AIActionType.Objective:
                        delta += !context.HasTarget ? bonus * 0.35f : bonus * 0.20f;
                        break;
                }
            }

            if (context.TargetPickWindow)
            {
                if (actionType == AIActionType.HoldRange)
                    delta += bonus * 0.35f;
                else if (actionType == AIActionType.UseSuper)
                    delta += bonus * 0.25f;
            }

            return delta;
        }

        private float GetSupportRoleMacroDelta(
            AIActionType actionType,
            AIRoleMacroContext context)
        {
            float bonus = Mathf.Max(0f, _profile.RoleSupportPeelMacroBonus);
            float delta = GetBacklineRoleMacroDelta(
                actionType,
                context,
                _profile.RoleBacklineAnchorMacroBonus * 0.65f,
                "support");

            if (context.AllyUnderThreat || context.MacroHold || context.MacroReset)
            {
                switch (actionType)
                {
                    case AIActionType.Peel:
                        delta += bonus;
                        break;
                    case AIActionType.Regroup:
                        delta += bonus * 0.55f;
                        break;
                    case AIActionType.Reposition:
                        delta += bonus * 0.35f;
                        break;
                    case AIActionType.Approach:
                        if (!context.TargetPickWindow)
                            delta -= bonus * 0.30f;
                        break;
                }
            }

            if (context.HighValueTarget && actionType == AIActionType.UseSuper)
                delta += bonus * 0.25f;

            return delta;
        }

        private float GetControllerRoleMacroDelta(
            AIActionType actionType,
            AIRoleMacroContext context)
        {
            float bonus = Mathf.Max(0f, _profile.RoleControllerZoneMacroBonus);

            if (context.ObjectivePressure || context.MacroPush || context.MacroHold)
            {
                switch (actionType)
                {
                    case AIActionType.Objective:
                        return bonus;
                    case AIActionType.Search:
                        return bonus * 0.55f;
                    case AIActionType.HoldRange:
                        return bonus * 0.45f;
                    case AIActionType.Reposition:
                        return bonus * 0.45f;
                    case AIActionType.UseSuper:
                        return context.HasTarget ? bonus * 0.35f : 0f;
                    case AIActionType.Approach:
                        return -bonus * 0.15f;
                }
            }

            if (context.AllyUnderThreat && actionType == AIActionType.Peel)
                return bonus * 0.45f;

            return 0f;
        }

        private float GetArtilleryRoleMacroDelta(
            AIActionType actionType,
            AIRoleMacroContext context)
        {
            float bonus = Mathf.Max(0f, _profile.RoleArtilleryDenialMacroBonus);
            float delta = GetBacklineRoleMacroDelta(
                actionType,
                context,
                bonus,
                "artillery");

            if (context.ObjectivePressure || context.HasTarget)
            {
                switch (actionType)
                {
                    case AIActionType.Reposition:
                        delta += bonus * 0.30f;
                        break;
                    case AIActionType.Objective:
                        delta += bonus * 0.25f;
                        break;
                    case AIActionType.UseSuper:
                        delta += context.HasTarget ? bonus * 0.35f : 0f;
                        break;
                    case AIActionType.Approach:
                        delta -= bonus * 0.20f;
                        break;
                }
            }

            return delta;
        }

        private float GetFighterRoleMacroDelta(
            AIActionType actionType,
            AIRoleMacroContext context)
        {
            float bonus = Mathf.Max(0f, _profile.RoleFighterFlexMacroBonus);
            float delta = 0f;

            if (context.MacroPush && context.TargetPickWindow)
            {
                if (actionType == AIActionType.Approach)
                    delta += bonus * 0.90f;
                else if (actionType == AIActionType.UseSuper)
                    delta += bonus * 0.35f;
            }

            if (context.ObjectivePressure)
            {
                if (actionType == AIActionType.Objective)
                    delta += bonus * 0.70f;
                else if (actionType == AIActionType.Reposition)
                    delta += bonus * 0.35f;
                else if (actionType == AIActionType.HoldRange)
                    delta += bonus * 0.25f;
            }

            if (context.MacroReset)
            {
                if (actionType == AIActionType.Regroup)
                    delta += bonus * 0.60f;
                else if (actionType == AIActionType.HoldRange)
                    delta += bonus * 0.35f;
                else if (actionType == AIActionType.Approach)
                    delta -= bonus * 0.30f;
            }

            if (context.AllyUnderThreat && actionType == AIActionType.Peel)
                delta += bonus * 0.45f;

            return delta;
        }

        private static bool CanCreateRoleMacroScore(
            AIActionType actionType,
            AIRoleMacroContext context)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                    return context.HasTarget;

                case AIActionType.Objective:
                    return context.ObjectivePressure || !context.HasTarget;

                case AIActionType.Search:
                    return !context.HasTarget || context.ObjectivePressure;

                case AIActionType.Peel:
                    return context.AllyUnderThreat;

                case AIActionType.Regroup:
                case AIActionType.Retreat:
                    return context.CarrierSafety || context.MacroReset;

                default:
                    return false;
            }
        }

        private void ApplyRoleMatchupBrain(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState,
            List<AIActionScore> results)
        {
            _lastRoleMatchupDebug = "Matchup=None";

            if (_profile == null ||
                results == null ||
                results.Count == 0 ||
                targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null ||
                _self == null)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.RoleMatchupAwarenessWeight);
            if (weight <= 0.01f)
            {
                _lastRoleMatchupDebug = "Matchup=Off";
                return;
            }

            AIRoleMatchupContext context = BuildRoleMatchupContext(
                targetBrawler,
                currentTick,
                macroState);

            if (!context.HasMatchupPressure)
                return;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateRoleMatchupDelta(
                    actionScore.ActionType,
                    context);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateRoleMatchupScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastRoleMatchupDebug =
                $"Matchup=own:{context.OwnArchetype} target:{context.TargetArchetype} " +
                $"range:{context.OwnRange:0.0}/{context.TargetRange:0.0} " +
                $"dist:{context.TargetDistance:0.0} " +
                $"shortBad:{context.ShortRangeBadChase} kite:{context.RangedKiteWindow} " +
                $"punish:{context.RangedPunishWindow} commit:{context.ObjectiveOverride} " +
                $"superGap:{context.SuperCanCloseGap} wait:{context.WaitingForCloseRangeSuper} " +
                $"w:{weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastRoleMatchupDebug += $" Delta={deltaDebug}";
        }

        private AIRoleMatchupContext BuildRoleMatchupContext(
            BrawlerController target,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            BrawlerArchetype ownArchetype = _self != null && _self.Definition != null
                ? _self.Definition.Archetype
                : _profile.Archetype;
            BrawlerArchetype targetArchetype = target != null && target.Definition != null
                ? target.Definition.Archetype
                : BrawlerArchetype.Fighter;
            float ownRange = Mathf.Max(1f, GetAbilityMaxRange());
            float targetRange = Mathf.Max(1f, GetTargetAbilityMaxRange(target));
            float targetDistance = Vector3.Distance(_self.Position, target.Position);
            float rangeGap = targetRange - ownRange;
            float reverseRangeGap = ownRange - targetRange;
            float meaningfulGap = Mathf.Max(0.85f, ownRange * 0.30f);
            bool ownShortRange = IsCloseRangePressureRole();
            bool targetShortRange = IsShortRangePressureTarget(target, targetRange);
            bool ownBackline = IsBacklineRole() || ownRange >= 6f;
            float targetHealthRatio = target.State != null
                ? Mathf.Clamp01(target.State.CurrentHealth /
                                Mathf.Max(1f, target.State.MaxHealth.Value))
                : 1f;
            int targetCarriedGems = target.State != null
                ? target.State.CarriedGemCount
                : 0;

            bool targetLow =
                targetHealthRatio <= Mathf.Max(0.22f, _profile.LowHealthChaseHealthThreshold);
            bool highValueTarget =
                targetCarriedGems >= 3 ||
                (macroState.EnemyTeamHasCountdown && targetCarriedGems > 0) ||
                IsModeCriticalTarget(target, macroState);
            bool superCanCloseGap = CanUseSuperToCloseGap(targetDistance);
            bool waitingForCloseRangeSuper =
                ownShortRange &&
                !superCanCloseGap &&
                _self.State != null &&
                _self.State.SuperCharge != null &&
                !_self.State.SuperCharge.IsReady &&
                _self.State.SuperCharge.ChargePercent >= 0.50f;
            bool teamCollapse = HasTeamCollapseOnTarget(target, currentTick);
            bool objectiveOverride =
                targetLow ||
                highValueTarget ||
                superCanCloseGap ||
                teamCollapse;

            float catchDistance = Mathf.Max(
                ownRange + _profile.AttackRangeBuffer,
                ownRange * Mathf.Max(1.25f, _profile.CloseRangeCatchDistanceMultiplier));

            bool shortRangeBadChase =
                ownShortRange &&
                rangeGap > meaningfulGap &&
                targetDistance > catchDistance &&
                !objectiveOverride;
            bool rangedKiteWindow =
                ownBackline &&
                targetShortRange &&
                reverseRangeGap > meaningfulGap &&
                targetDistance <= ownRange * 0.90f;
            bool rangedPunishWindow =
                ownBackline &&
                targetShortRange &&
                reverseRangeGap > meaningfulGap &&
                targetDistance > ownRange * 0.45f &&
                targetDistance <= ownRange + _profile.AttackRangeBuffer &&
                !AIMapNavigationUtility.HasCoverBetween(
                    SimulationClock.Pathfinder,
                    _self.Position,
                    target.Position);

            return new AIRoleMatchupContext
            {
                HasMatchupPressure =
                    shortRangeBadChase ||
                    rangedKiteWindow ||
                    rangedPunishWindow ||
                    (ownShortRange && objectiveOverride && rangeGap > meaningfulGap),
                OwnArchetype = ownArchetype,
                TargetArchetype = targetArchetype,
                OwnShortRange = ownShortRange,
                TargetShortRange = targetShortRange,
                ShortRangeBadChase = shortRangeBadChase,
                RangedKiteWindow = rangedKiteWindow,
                RangedPunishWindow = rangedPunishWindow,
                ObjectiveOverride = objectiveOverride,
                SuperCanCloseGap = superCanCloseGap,
                WaitingForCloseRangeSuper = waitingForCloseRangeSuper,
                OwnRange = ownRange,
                TargetRange = targetRange,
                TargetDistance = targetDistance
            };
        }

        private float CalculateRoleMatchupDelta(
            AIActionType actionType,
            AIRoleMatchupContext context)
        {
            float delta = 0f;

            if (context.ShortRangeBadChase)
            {
                switch (actionType)
                {
                    case AIActionType.Approach:
                        delta -= Mathf.Max(0f, _profile.CloseRangeOutrangedChasePenalty) *
                                 (context.WaitingForCloseRangeSuper ? 0.92f : 0.65f);
                        break;
                    case AIActionType.Reposition:
                        delta += Mathf.Max(0f, _profile.CloseRangeCoverRepositionBonus) *
                                 (context.WaitingForCloseRangeSuper ? 1.18f : 1f);
                        break;
                    case AIActionType.HoldRange:
                        delta += Mathf.Max(0f, _profile.CloseRangeCoverRepositionBonus) *
                                 (context.WaitingForCloseRangeSuper ? 0.72f : 0.45f);
                        break;
                    case AIActionType.Search:
                    case AIActionType.Objective:
                        delta += Mathf.Max(0f, _profile.MatchupKiteShortRangeBonus) * 0.25f;
                        break;
                }
            }

            if (context.RangedKiteWindow)
            {
                float kiteBonus = Mathf.Max(0f, _profile.MatchupKiteShortRangeBonus);
                switch (actionType)
                {
                    case AIActionType.Reposition:
                        delta += kiteBonus;
                        break;
                    case AIActionType.HoldRange:
                        delta += kiteBonus * 0.75f;
                        break;
                    case AIActionType.Approach:
                        delta -= kiteBonus * 0.55f;
                        break;
                }
            }

            if (context.RangedPunishWindow)
            {
                float punishBonus = Mathf.Max(0f, _profile.MatchupPunishShortRangeBonus);
                switch (actionType)
                {
                    case AIActionType.HoldRange:
                        delta += punishBonus;
                        break;
                    case AIActionType.UseSuper:
                        delta += punishBonus * 0.45f;
                        break;
                    case AIActionType.Reposition:
                        delta += punishBonus * 0.35f;
                        break;
                }
            }

            if (context.ObjectiveOverride && context.OwnShortRange)
            {
                float overrideBonus = Mathf.Max(0f, _profile.MatchupObjectiveOverrideBonus);
                switch (actionType)
                {
                    case AIActionType.Approach:
                        delta += overrideBonus;
                        break;
                    case AIActionType.UseSuper:
                        delta += overrideBonus * 0.55f;
                        break;
                    case AIActionType.Reposition:
                        delta += overrideBonus * 0.25f;
                        break;
                }
            }

            return delta;
        }

        private static bool CanCreateRoleMatchupScore(
            AIActionType actionType,
            AIRoleMatchupContext context)
        {
            switch (actionType)
            {
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                    return context.RangedKiteWindow ||
                           context.RangedPunishWindow ||
                           context.ShortRangeBadChase;

                case AIActionType.Search:
                case AIActionType.Objective:
                    return context.ShortRangeBadChase;

                default:
                    return false;
            }
        }

        private void ApplyCoverPeekCombatPlanner(
            AITargetInfo targetInfo,
            List<AIActionScore> results)
        {
            _lastCoverPeekDebug = "CoverPeek=None";

            if (_profile == null ||
                results == null ||
                results.Count == 0 ||
                targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null ||
                _self == null ||
                SimulationClock.Pathfinder == null)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.CoverPeekPlannerWeight);
            if (weight <= 0.01f)
            {
                _lastCoverPeekDebug = "CoverPeek=Off";
                return;
            }

            AICoverPeekContext context = BuildCoverPeekContext(targetBrawler);
            if (!context.HasCoverSignal)
                return;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateCoverPeekDelta(
                    actionScore.ActionType,
                    context);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateCoverPeekScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastCoverPeekDebug =
                $"CoverPeek=selfCover:{context.SelfNearCover} targetCover:{context.TargetNearCover} " +
                $"between:{context.HasCoverBetween} direct:{context.DirectFire} " +
                $"peek:{context.CoverPeekWindow} blocked:{context.BlockedFireLane} " +
                $"exposed:{context.ExposedDuel} thrower:{context.ThrowerCoverWindow} " +
                $"dist:{context.TargetDistance:0.0} w:{weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastCoverPeekDebug += $" Delta={deltaDebug}";
        }

        private AICoverPeekContext BuildCoverPeekContext(BrawlerController target)
        {
            AStarSolver pathfinder = SimulationClock.Pathfinder;
            Vector2Int selfCoords = pathfinder.GetGridCoords(_self.Position);
            Vector2Int targetCoords = pathfinder.GetGridCoords(target.Position);
            bool selfNearCover = pathfinder.IsNearObstacle(selfCoords);
            bool targetNearCover = pathfinder.IsNearObstacle(targetCoords);
            bool hasCoverBetween = AIMapNavigationUtility.HasCoverBetween(
                pathfinder,
                selfCoords,
                targetCoords);
            float ownRange = Mathf.Max(1f, GetAbilityMaxRange());
            float targetRange = Mathf.Max(1f, GetTargetAbilityMaxRange(target));
            float distance = Vector3.Distance(_self.Position, target.Position);
            bool inOwnRange = distance <= ownRange + _profile.AttackRangeBuffer;
            bool directFire = IsCurrentMainAttackDirectFire();
            bool throwerPressure = IsArtillery || !directFire;
            bool coverPeekWindow =
                directFire &&
                selfNearCover &&
                !hasCoverBetween &&
                inOwnRange;
            bool blockedFireLane =
                directFire &&
                hasCoverBetween &&
                distance <= ownRange + _profile.AttackRangeBuffer * 2f;
            bool exposedDuel =
                !selfNearCover &&
                !hasCoverBetween &&
                distance <= targetRange + _profile.AttackRangeBuffer &&
                targetRange >= ownRange * 0.85f;
            bool throwerCoverWindow =
                throwerPressure &&
                selfNearCover &&
                hasCoverBetween &&
                inOwnRange;

            return new AICoverPeekContext
            {
                HasCoverSignal =
                    coverPeekWindow ||
                    blockedFireLane ||
                    exposedDuel ||
                    throwerCoverWindow,
                SelfNearCover = selfNearCover,
                TargetNearCover = targetNearCover,
                HasCoverBetween = hasCoverBetween,
                DirectFire = directFire,
                CoverPeekWindow = coverPeekWindow,
                BlockedFireLane = blockedFireLane,
                ExposedDuel = exposedDuel,
                ThrowerCoverWindow = throwerCoverWindow,
                TargetDistance = distance
            };
        }

        private float CalculateCoverPeekDelta(
            AIActionType actionType,
            AICoverPeekContext context)
        {
            float delta = 0f;

            if (context.CoverPeekWindow)
            {
                switch (actionType)
                {
                    case AIActionType.HoldRange:
                        delta += _profile.CoverPeekHoldBonus;
                        break;
                    case AIActionType.Reposition:
                        delta += _profile.CoverPeekHoldBonus * 0.35f;
                        break;
                    case AIActionType.Approach:
                        delta -= _profile.CoverPeekApproachPenalty;
                        break;
                }
            }

            if (context.BlockedFireLane)
            {
                switch (actionType)
                {
                    case AIActionType.Reposition:
                        delta += _profile.BlockedFireLaneRepositionBonus;
                        break;
                    case AIActionType.HoldRange:
                        delta -= _profile.CoverPeekApproachPenalty * 0.45f;
                        break;
                    case AIActionType.Approach:
                        delta -= _profile.CoverPeekApproachPenalty;
                        break;
                    case AIActionType.UseSuper:
                        delta -= _profile.CoverPeekApproachPenalty * 0.35f;
                        break;
                }
            }

            if (context.ExposedDuel)
            {
                switch (actionType)
                {
                    case AIActionType.Reposition:
                        delta += _profile.ExposedDuelRepositionBonus;
                        break;
                    case AIActionType.HoldRange:
                        delta += _profile.ExposedDuelRepositionBonus * 0.45f;
                        break;
                    case AIActionType.Approach:
                        if (IsBacklineRole())
                            delta -= _profile.CoverPeekApproachPenalty * 0.65f;
                        break;
                }
            }

            if (context.ThrowerCoverWindow)
            {
                switch (actionType)
                {
                    case AIActionType.HoldRange:
                        delta += _profile.CoverPeekHoldBonus * 0.85f;
                        break;
                    case AIActionType.Reposition:
                        delta += _profile.CoverPeekHoldBonus * 0.45f;
                        break;
                    case AIActionType.Approach:
                        delta -= _profile.CoverPeekApproachPenalty * 0.35f;
                        break;
                }
            }

            return delta;
        }

        private static bool CanCreateCoverPeekScore(
            AIActionType actionType,
            AICoverPeekContext context)
        {
            switch (actionType)
            {
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                    return context.HasCoverSignal;

                default:
                    return false;
            }
        }

        private void ApplyEngagementRiskAwareness(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState,
            uint currentTick,
            List<AIActionScore> results)
        {
            _lastEngagementRiskDebug = "EngageRisk=None";

            if (_profile == null ||
                results == null ||
                results.Count == 0 ||
                targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null ||
                _self == null ||
                _self.State == null)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.EngagementRiskAwarenessWeight);
            if (weight <= 0.01f)
            {
                _lastEngagementRiskDebug = "EngageRisk=Off";
                return;
            }

            AIEngagementRiskContext context = BuildEngagementRiskContext(
                targetBrawler,
                macroState,
                currentTick);

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateEngagementRiskDelta(
                    actionScore.ActionType,
                    context);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateEngagementRiskScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastEngagementRiskDebug =
                $"EngageRisk=ally:{context.AllyPressure:0.0} enemy:{context.EnemyPressure:0.0} " +
                $"risk:{context.RiskPressure:0.00} support:{context.SupportedFight} " +
                $"pick:{context.PickWindow} carrier:{context.SelfCarrier} " +
                $"w:{weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastEngagementRiskDebug += $" Delta={deltaDebug}";
        }

        private AIEngagementRiskContext BuildEngagementRiskContext(
            BrawlerController target,
            AIGameModeMacroState macroState,
            uint currentTick)
        {
            float radius = Mathf.Max(2.5f, _profile.EngagementRiskRadius);
            AIEngagementRiskContext context = new AIEngagementRiskContext
            {
                HasTarget = true,
                TargetHealthRatio = target.State != null
                    ? Mathf.Clamp01(target.State.CurrentHealth /
                                    Mathf.Max(1f, target.State.MaxHealth.Value))
                    : 1f,
                TargetCarriedGems = target.State != null ? target.State.CarriedGemCount : 0,
                SelfCarriedGems = _self.State != null ? _self.State.CarriedGemCount : 0,
                TargetDistance = Vector3.Distance(_self.Position, target.Position)
            };

            _nearbyAllyBuffer.Clear();
            if (SimulationClock.Grid != null)
            {
                SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                    _self.Position,
                    radius,
                    _nearbyAllyBuffer);
            }

            for (int i = 0; i < _nearbyAllyBuffer.Count; i++)
            {
                ISpatialEntity entity = _nearbyAllyBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) ||
                    entity.EntityID == _self.EntityID)
                {
                    continue;
                }

                float distance = Vector3.Distance(_self.Position, entity.Position);
                float pressure = 0.35f + (1f - Mathf.Clamp01(distance / radius));

                if (entity.Team == _self.Team)
                {
                    context.AllyPressure += pressure;
                    context.AllyCount++;
                }
                else
                {
                    context.EnemyPressure += pressure;
                    context.EnemyCount++;
                }
            }

            if (context.TargetDistance <= radius)
                context.EnemyPressure += 0.50f + (1f - Mathf.Clamp01(context.TargetDistance / radius));

            if (_teamCoordinator != null)
            {
                int approachAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(
                    AIActionType.Approach,
                    currentTick);
                int holdAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(
                    AIActionType.HoldRange,
                    currentTick);
                int peelAllies = _teamCoordinator.GetActionIntentCountExcludingSelf(
                    AIActionType.Peel,
                    currentTick);

                context.AllyPressure += approachAllies * 0.45f;
                context.AllyPressure += holdAllies * 0.25f;
                context.AllyPressure += peelAllies * 0.20f;
                context.TeamActionSupport = approachAllies + holdAllies + peelAllies;
            }

            context.PickWindow =
                context.TargetHealthRatio <= Mathf.Max(0.24f, _profile.FinisherHealthThreshold) ||
                context.TargetCarriedGems >= 3 ||
                (macroState.EnemyTeamHasCountdown && context.TargetCarriedGems > 0) ||
                target.State.HasStatus(StatusEffectType.Stun) ||
                target.State.HasStatus(StatusEffectType.Slow);
            context.SelfCarrier = context.SelfCarriedGems > 0;
            context.SupportedFight =
                context.AllyPressure >= context.EnemyPressure * 0.82f ||
                context.TeamActionSupport > 0;
            context.RiskPressure =
                Mathf.Clamp01((context.EnemyPressure - context.AllyPressure) / 2.75f);
            context.BadDive =
                context.RiskPressure >= 0.22f &&
                !context.PickWindow &&
                !context.SupportedFight;

            return context;
        }

        private float CalculateEngagementRiskDelta(
            AIActionType actionType,
            AIEngagementRiskContext context)
        {
            float delta = 0f;

            if (context.BadDive)
            {
                switch (actionType)
                {
                    case AIActionType.Approach:
                        delta -= _profile.OutnumberedApproachPenalty *
                                 (0.60f + context.RiskPressure);
                        break;
                    case AIActionType.UseSuper:
                        delta -= _profile.OutnumberedApproachPenalty *
                                 context.RiskPressure * 0.35f;
                        break;
                    case AIActionType.Reposition:
                        delta += _profile.BadDiveRepositionBonus *
                                 (0.65f + context.RiskPressure);
                        break;
                    case AIActionType.HoldRange:
                        delta += _profile.BadDiveRepositionBonus *
                                 (0.35f + context.RiskPressure * 0.45f);
                        break;
                    case AIActionType.Regroup:
                        delta += _profile.EngagementRiskSafetyBonus *
                                 (0.40f + context.RiskPressure);
                        break;
                    case AIActionType.Retreat:
                        delta += _profile.EngagementRiskSafetyBonus *
                                 context.RiskPressure * 0.70f;
                        break;
                }
            }

            if (context.SupportedFight && context.PickWindow)
            {
                switch (actionType)
                {
                    case AIActionType.Approach:
                        delta += _profile.SupportedFightCommitBonus;
                        break;
                    case AIActionType.UseSuper:
                        delta += _profile.SupportedFightCommitBonus * 0.55f;
                        break;
                    case AIActionType.HoldRange:
                        delta += _profile.SupportedFightCommitBonus * 0.30f;
                        break;
                }
            }

            if (context.SelfCarrier && context.RiskPressure > 0.10f)
            {
                switch (actionType)
                {
                    case AIActionType.Approach:
                        delta -= _profile.OutnumberedApproachPenalty *
                                 Mathf.Max(0.35f, context.RiskPressure);
                        break;
                    case AIActionType.Regroup:
                    case AIActionType.Retreat:
                        delta += _profile.EngagementRiskSafetyBonus *
                                 (0.45f + context.RiskPressure);
                        break;
                    case AIActionType.HoldRange:
                    case AIActionType.Reposition:
                        delta += _profile.BadDiveRepositionBonus *
                                 context.RiskPressure * 0.55f;
                        break;
                }
            }

            return delta;
        }

        private static bool CanCreateEngagementRiskScore(
            AIActionType actionType,
            AIEngagementRiskContext context)
        {
            switch (actionType)
            {
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                    return context.HasTarget;

                case AIActionType.Regroup:
                case AIActionType.Retreat:
                    return context.BadDive || context.SelfCarrier;

                default:
                    return false;
            }
        }

        private void ApplyPressureRotationAwareness(
            AITargetInfo targetInfo,
            uint currentTick,
            List<AIActionScore> results)
        {
            _lastPressureRotationDebug = "PressureRot=None";

            if (_profile == null ||
                _teamCoordinator == null ||
                results == null ||
                results.Count == 0)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.PressureRotationAwarenessWeight);
            if (weight <= 0.01f)
            {
                _lastPressureRotationDebug = "PressureRot=Off";
                return;
            }

            AIPressureRotationContext context = BuildPressureRotationContext(
                targetInfo,
                currentTick);

            if (!context.HasEnemyHotspot && !context.HasThreatCenter)
                return;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculatePressureRotationDelta(
                    actionScore.ActionType,
                    context);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreatePressureRotationScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastPressureRotationDebug =
                $"PressureRot=hot:{context.HotspotRelevance:0.00}/{context.HotspotPressure:0.0} " +
                $"threat:{context.ThreatRelevance:0.00}/{context.ThreatPressure:0.0} " +
                $"targetHot:{context.TargetNearHotspot} targetThreat:{context.TargetNearThreatCenter} " +
                $"w:{weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastPressureRotationDebug += $" Delta={deltaDebug}";
        }

        private AIPressureRotationContext BuildPressureRotationContext(
            AITargetInfo targetInfo,
            uint currentTick)
        {
            float radius = Mathf.Max(3f, _profile.PressureRotationRadius);
            AIPressureRotationContext context = new AIPressureRotationContext
            {
                HasTarget = targetInfo != null &&
                            targetInfo.HasLiveTarget &&
                            SpatialEntityUtility.IsAlive(targetInfo.Target)
            };

            if (context.HasTarget)
            {
                context.TargetPosition = targetInfo.Target.Position;
            }

            if (_teamCoordinator.TryGetEnemyHotspot(
                    currentTick,
                    out Vector3 hotspot,
                    out float hotspotPressure))
            {
                context.HasEnemyHotspot = true;
                context.HotspotPosition = hotspot;
                context.HotspotPressure = hotspotPressure;
                float distance = Vector3.Distance(_self.Position, hotspot);
                context.HotspotRelevance = 1f - Mathf.Clamp01(distance / radius);
                context.TargetNearHotspot =
                    context.HasTarget &&
                    Vector3.Distance(context.TargetPosition, hotspot) <= radius * 0.70f;
            }

            if (_teamCoordinator.TryGetThreatCenter(
                    currentTick,
                    out Vector3 threatCenter,
                    out float threatPressure))
            {
                context.HasThreatCenter = true;
                context.ThreatCenterPosition = threatCenter;
                context.ThreatPressure = threatPressure;
                float distance = Vector3.Distance(_self.Position, threatCenter);
                context.ThreatRelevance = 1f - Mathf.Clamp01(distance / radius);
                context.TargetNearThreatCenter =
                    context.HasTarget &&
                    Vector3.Distance(context.TargetPosition, threatCenter) <= radius * 0.75f;
            }

            return context;
        }

        private float CalculatePressureRotationDelta(
            AIActionType actionType,
            AIPressureRotationContext context)
        {
            float delta = 0f;

            if (context.HasEnemyHotspot)
            {
                float hotspotStrength =
                    Mathf.Clamp01(context.HotspotPressure / 5f) *
                    Mathf.Max(0.25f, context.HotspotRelevance);

                switch (actionType)
                {
                    case AIActionType.Search:
                        delta += _profile.EnemyHotspotRotationBonus *
                                 hotspotStrength *
                                 (context.HasTarget ? 0.35f : 1f);
                        break;
                    case AIActionType.Objective:
                        delta += _profile.EnemyHotspotRotationBonus *
                                 hotspotStrength *
                                 (context.HasTarget ? 0.25f : 0.70f);
                        break;
                    case AIActionType.Reposition:
                        delta += _profile.EnemyHotspotRotationBonus *
                                 hotspotStrength *
                                 (context.HasTarget ? 0.45f : 0.60f);
                        break;
                    case AIActionType.Approach:
                        if (context.HasTarget && context.TargetNearHotspot)
                            delta += _profile.EnemyHotspotRotationBonus *
                                     hotspotStrength * 0.35f;
                        break;
                }
            }

            if (context.HasThreatCenter)
            {
                float threatStrength =
                    Mathf.Clamp01(context.ThreatPressure / 5f) *
                    Mathf.Max(0.25f, context.ThreatRelevance);

                switch (actionType)
                {
                    case AIActionType.Approach:
                        if (!context.TargetNearThreatCenter)
                            delta -= _profile.ThreatCenterDivePenalty * threatStrength;
                        break;
                    case AIActionType.Objective:
                        delta -= _profile.ThreatCenterDivePenalty *
                                 threatStrength *
                                 (context.HasTarget ? 0.25f : 0.45f);
                        break;
                    case AIActionType.Reposition:
                        delta += _profile.ThreatCenterRotationBonus *
                                 threatStrength;
                        break;
                    case AIActionType.HoldRange:
                        delta += _profile.ThreatCenterRotationBonus *
                                 threatStrength * 0.60f;
                        break;
                    case AIActionType.Retreat:
                    case AIActionType.Regroup:
                        delta += _profile.ThreatCenterRotationBonus *
                                 threatStrength * 0.35f;
                        break;
                }
            }

            return delta;
        }

        private static bool CanCreatePressureRotationScore(
            AIActionType actionType,
            AIPressureRotationContext context)
        {
            switch (actionType)
            {
                case AIActionType.Search:
                case AIActionType.Objective:
                    return !context.HasTarget || context.HasEnemyHotspot;

                case AIActionType.Reposition:
                case AIActionType.HoldRange:
                    return context.HasTarget || context.HasThreatCenter;

                case AIActionType.Retreat:
                case AIActionType.Regroup:
                    return context.HasThreatCenter;

                default:
                    return false;
            }
        }

        private void ApplyAntiClumpSpacing(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState,
            uint currentTick,
            List<AIActionScore> results)
        {
            _lastSpacingDebug = "Spacing=None";

            if (_profile == null ||
                results == null ||
                results.Count == 0 ||
                _self == null ||
                SimulationClock.Grid == null)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.SpacingAwarenessWeight);
            if (weight <= 0.01f)
            {
                _lastSpacingDebug = "Spacing=Off";
                return;
            }

            AISpacingContext context = BuildSpacingContext(
                targetInfo,
                macroState,
                currentTick);
            if (!context.HasClumpPressure)
                return;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateSpacingDelta(
                    actionScore.ActionType,
                    context);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateSpacingScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastSpacingDebug =
                $"Spacing=allies:{context.NearbyAllyCount} pressure:{context.ClumpPressure:0.00} " +
                $"collapse:{context.IntentionalCollapse} carrier:{context.SelfCarrier} " +
                $"same:{context.SameTargetAllies} w:{weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastSpacingDebug += $" Delta={deltaDebug}";
        }

        private AISpacingContext BuildSpacingContext(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState,
            uint currentTick)
        {
            float radius = Mathf.Max(1f, _profile.LocalAllyClumpRadius);
            AISpacingContext context = new AISpacingContext
            {
                SelfCarrier =
                    _self.State != null &&
                    _self.State.CarriedGemCount > 0
            };

            BrawlerController targetBrawler = null;
            if (targetInfo != null &&
                targetInfo.HasLiveTarget &&
                targetInfo.Target is BrawlerController candidateTarget &&
                candidateTarget.State != null)
            {
                targetBrawler = candidateTarget;
                context.HasTarget = true;
                context.TargetHealthRatio = Mathf.Clamp01(
                    candidateTarget.State.CurrentHealth /
                    Mathf.Max(1f, candidateTarget.State.MaxHealth.Value));
            }

            _nearbyAllyBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                _self.Position,
                radius,
                _nearbyAllyBuffer);

            for (int i = 0; i < _nearbyAllyBuffer.Count; i++)
            {
                ISpatialEntity entity = _nearbyAllyBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) ||
                    entity.EntityID == _self.EntityID ||
                    entity.Team != _self.Team)
                {
                    continue;
                }

                context.NearbyAllyCount++;
                float distance = Vector3.Distance(_self.Position, entity.Position);
                context.ClumpPressure += 0.35f + (1f - Mathf.Clamp01(distance / radius));
            }

            if (context.HasTarget && targetBrawler != null)
            {
                context.SameTargetAllies = AITeamBlackboard.GetTargetFocusCountExcluding(
                    _self.Team,
                    targetBrawler.EntityID,
                    _self.EntityID);
            }

            bool targetFinishable =
                context.HasTarget &&
                context.TargetHealthRatio <= Mathf.Max(0.24f, _profile.FinisherHealthThreshold);
            bool pushCollapse =
                macroState.Call == AIGameModeMacroCall.Push &&
                context.HasTarget &&
                context.SameTargetAllies > 0;
            bool playbookCollapse =
                _teamCoordinator != null &&
                _teamCoordinator.GetActionIntentCountExcludingSelf(
                    AIActionType.Approach,
                    currentTick) > 0 &&
                targetFinishable;

            context.IntentionalCollapse =
                targetFinishable ||
                pushCollapse ||
                playbookCollapse;
            context.ClumpPressure = Mathf.Clamp01(context.ClumpPressure / 2.25f);
            context.HasClumpPressure =
                context.NearbyAllyCount > 0 &&
                context.ClumpPressure >= 0.25f;

            return context;
        }

        private float CalculateSpacingDelta(
            AIActionType actionType,
            AISpacingContext context)
        {
            float pressure = Mathf.Clamp01(context.ClumpPressure);
            float collapseFactor = context.IntentionalCollapse ? 0.35f : 1f;
            float delta = 0f;

            switch (actionType)
            {
                case AIActionType.Reposition:
                    delta += _profile.LocalClumpRepositionBonus * pressure;
                    break;

                case AIActionType.HoldRange:
                    delta += _profile.LocalClumpRepositionBonus * pressure * 0.35f;
                    break;

                case AIActionType.Approach:
                    delta -= _profile.ClumpedApproachPenalty * pressure * collapseFactor;
                    break;

                case AIActionType.Objective:
                case AIActionType.Search:
                    delta -= _profile.ClumpedObjectivePenalty * pressure *
                             (context.SelfCarrier ? 1.15f : collapseFactor);
                    break;

                case AIActionType.UseSuper:
                    if (!context.IntentionalCollapse)
                        delta -= _profile.ClumpedApproachPenalty * pressure * 0.30f;
                    break;
            }

            return delta;
        }

        private static bool CanCreateSpacingScore(
            AIActionType actionType,
            AISpacingContext context)
        {
            return actionType == AIActionType.Reposition ||
                   actionType == AIActionType.HoldRange;
        }

        private bool CanCreateWinConditionScore(
            AIActionType actionType,
            AIWinConditionActionContext context)
        {
            switch (actionType)
            {
                case AIActionType.Search:
                case AIActionType.Objective:
                    return !context.HasLiveTarget;

                case AIActionType.Retreat:
                case AIActionType.Regroup:
                    return context.MacroState.OwnTeamHasCountdown &&
                           context.SelfCarriedGems > 0;

                default:
                    return false;
            }
        }

        private void ApplyModeClutchLogic(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState,
            List<AIActionScore> results)
        {
            _lastModeClutchDebug = "Clutch=None";

            if (_profile == null ||
                results == null ||
                results.Count == 0)
            {
                return;
            }

            float weight = Mathf.Max(0f, _profile.ModeClutchAwarenessWeight);
            if (weight <= 0.01f)
            {
                _lastModeClutchDebug = "Clutch=Off";
                return;
            }

            AIModeClutchContext context = BuildModeClutchContext(
                targetInfo,
                macroState);

            if (!context.HasClutchSignal)
                return;

            string deltaDebug = string.Empty;
            for (int i = 0; i < results.Count; i++)
            {
                AIActionScore actionScore = results[i];
                float delta = CalculateModeClutchDelta(
                    actionScore.ActionType,
                    context);
                delta *= weight;

                if (Mathf.Abs(delta) <= 0.01f)
                    continue;

                if (actionScore.Score <= 0f &&
                    delta > 0f &&
                    !CanCreateModeClutchScore(actionScore.ActionType, context))
                {
                    continue;
                }

                float adjustedScore = ClampActionScore(
                    actionScore.ActionType,
                    actionScore.Score + delta);
                float actualDelta = adjustedScore - actionScore.Score;
                if (Mathf.Abs(actualDelta) <= 0.01f)
                    continue;

                results[i] = new AIActionScore(
                    actionScore.ActionType,
                    adjustedScore);
                deltaDebug = AppendRoleDebug(
                    deltaDebug,
                    $"{actionScore.ActionType}{actualDelta:+0.0;-0.0}");
            }

            _lastModeClutchDebug =
                $"Clutch=mode:{context.Mode} lead:{context.ProtectLead} " +
                $"comeback:{context.ComebackPressure} swing:{context.ObjectiveSwing} " +
                $"final:{context.FinalPressure} selfValue:{context.SelfObjectiveValue} " +
                $"targetValue:{context.TargetObjectiveValue} w:{weight:0.00}";

            if (!string.IsNullOrEmpty(deltaDebug))
                _lastModeClutchDebug += $" Delta={deltaDebug}";
        }

        private AIModeClutchContext BuildModeClutchContext(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState)
        {
            int selfValue = _self != null && _self.State != null
                ? _self.State.CarriedGemCount
                : 0;
            int targetValue = 0;
            float targetHealthRatio = 1f;
            BrawlerController targetBrawler = null;
            bool hasTarget = false;

            if (targetInfo != null &&
                targetInfo.HasLiveTarget &&
                targetInfo.Target is BrawlerController candidateTarget &&
                candidateTarget.State != null)
            {
                targetBrawler = candidateTarget;
                hasTarget = true;
            }

            if (hasTarget)
            {
                targetValue = targetBrawler.State.CarriedGemCount;
                targetHealthRatio = Mathf.Clamp01(
                    targetBrawler.State.CurrentHealth /
                    Mathf.Max(1f, targetBrawler.State.MaxHealth.Value));
            }

            bool finalPressure =
                macroState.Phase == AIGameModeObjectivePhase.FinalPressure ||
                (macroState.MatchTimeRemainingSeconds > 0f &&
                 macroState.MatchTimeRemainingSeconds <= _profile.ClutchFinalPressureSeconds);
            bool protectLead =
                macroState.OwnTeamHasCountdown ||
                (macroState.IsLeading &&
                 (finalPressure || IsOneScoreFromWin(macroState.OwnScore, macroState.ScoreToWin)));
            bool comebackPressure =
                macroState.EnemyTeamHasCountdown ||
                (macroState.IsBehind &&
                 (finalPressure || IsOneScoreFromWin(macroState.EnemyScore, macroState.ScoreToWin)));
            bool objectiveSwing =
                macroState.Call == AIGameModeMacroCall.Push ||
                macroState.Phase == AIGameModeObjectivePhase.Countdown ||
                (hasTarget &&
                 (targetValue >= 3 ||
                  targetHealthRatio <= Mathf.Max(0.25f, _profile.FinisherHealthThreshold)));

            switch (macroState.Mode)
            {
                case GameModeId.Knockout:
                    protectLead |= macroState.IsLeading && macroState.Call == AIGameModeMacroCall.Hold;
                    comebackPressure |= macroState.IsBehind || macroState.Call == AIGameModeMacroCall.Push;
                    objectiveSwing |= hasTarget && targetHealthRatio <= 0.45f;
                    break;

                case GameModeId.BrawlBall:
                    protectLead |= IsOneScoreFromWin(macroState.EnemyScore, macroState.ScoreToWin) &&
                                   macroState.Call == AIGameModeMacroCall.Reset;
                    comebackPressure |= IsOneScoreFromWin(macroState.OwnScore, macroState.ScoreToWin) ||
                                        macroState.Call == AIGameModeMacroCall.Push;
                    objectiveSwing |= comebackPressure;
                    break;

                case GameModeId.SoloShowdown:
                    protectLead |= finalPressure ||
                                   (_self != null &&
                                    _self.State != null &&
                                    _self.State.CurrentHealth <= _self.State.MaxHealth.Value * 0.45f);
                    comebackPressure |= hasTarget &&
                                        targetHealthRatio <= Mathf.Max(0.30f, _profile.FinisherHealthThreshold);
                    objectiveSwing |= comebackPressure;
                    break;
            }

            return new AIModeClutchContext
            {
                HasClutchSignal =
                    protectLead ||
                    comebackPressure ||
                    objectiveSwing ||
                    finalPressure,
                Mode = macroState.Mode,
                ProtectLead = protectLead,
                ComebackPressure = comebackPressure,
                ObjectiveSwing = objectiveSwing,
                FinalPressure = finalPressure,
                HasTarget = hasTarget,
                SelfObjectiveValue = selfValue,
                TargetObjectiveValue = targetValue
            };
        }

        private float CalculateModeClutchDelta(
            AIActionType actionType,
            AIModeClutchContext context)
        {
            float delta = 0f;

            if (context.ProtectLead)
            {
                float safety = Mathf.Max(0f, _profile.ClutchLeadSafetyBonus);
                switch (actionType)
                {
                    case AIActionType.HoldRange:
                    case AIActionType.Reposition:
                        delta += safety;
                        break;
                    case AIActionType.Regroup:
                    case AIActionType.Retreat:
                    case AIActionType.Peel:
                        delta += safety * (context.SelfObjectiveValue > 0 ? 0.95f : 0.55f);
                        break;
                    case AIActionType.Approach:
                        delta -= safety * (context.SelfObjectiveValue > 0 ? 0.90f : 0.45f);
                        break;
                    case AIActionType.Search:
                    case AIActionType.Objective:
                        if (context.SelfObjectiveValue > 0)
                            delta -= safety * 0.45f;
                        break;
                }
            }

            if (context.ComebackPressure)
            {
                float comeback = Mathf.Max(0f, _profile.ClutchComebackPressureBonus);
                switch (actionType)
                {
                    case AIActionType.Approach:
                    case AIActionType.UseSuper:
                        delta += comeback;
                        break;
                    case AIActionType.Objective:
                    case AIActionType.Search:
                        delta += comeback * 0.75f;
                        break;
                    case AIActionType.Retreat:
                    case AIActionType.Regroup:
                        delta -= comeback * 0.45f;
                        break;
                }
            }

            if (context.ObjectiveSwing)
            {
                float swing = Mathf.Max(0f, _profile.ClutchObjectiveSwingBonus);
                switch (actionType)
                {
                    case AIActionType.Objective:
                    case AIActionType.Search:
                        delta += swing;
                        break;
                    case AIActionType.Approach:
                        delta += context.HasTarget ? swing * 0.70f : swing * 0.25f;
                        break;
                    case AIActionType.UseSuper:
                        delta += context.HasTarget ? swing * 0.55f : 0f;
                        break;
                    case AIActionType.HoldRange:
                        delta += swing * 0.35f;
                        break;
                }
            }

            if (context.FinalPressure && !context.ProtectLead && !context.ComebackPressure)
            {
                float finalBonus = Mathf.Max(0f, _profile.ClutchObjectiveSwingBonus) * 0.45f;
                if (actionType == AIActionType.Objective ||
                    actionType == AIActionType.Search ||
                    actionType == AIActionType.HoldRange)
                {
                    delta += finalBonus;
                }
            }

            return delta;
        }

        private static bool CanCreateModeClutchScore(
            AIActionType actionType,
            AIModeClutchContext context)
        {
            switch (actionType)
            {
                case AIActionType.Objective:
                case AIActionType.Search:
                    return context.ObjectiveSwing ||
                           context.ComebackPressure ||
                           context.FinalPressure;

                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                    return context.ProtectLead || context.FinalPressure;

                case AIActionType.Regroup:
                case AIActionType.Retreat:
                case AIActionType.Peel:
                    return context.ProtectLead;

                default:
                    return false;
            }
        }

        private static bool IsOneScoreFromWin(int score, int scoreToWin)
        {
            return scoreToWin > 0 && score >= Mathf.Max(0, scoreToWin - 1);
        }

        private float CalculatePlaybookDelta(
            AIActionType actionType,
            AITeamPlaybookState playbookState)
        {
            bool selfCarrier =
                _self != null &&
                _self.State != null &&
                _self.State.CarriedGemCount > 0 &&
                playbookState.CarrierEntityId == _self.EntityID;

            switch (playbookState.Call)
            {
                case AITeamPlaybookCall.Push:
                    return GetPushPlaybookDelta(actionType);

                case AITeamPlaybookCall.Hold:
                    return GetHoldPlaybookDelta(actionType, selfCarrier);

                case AITeamPlaybookCall.Reset:
                    return GetResetPlaybookDelta(actionType, selfCarrier);

                case AITeamPlaybookCall.Engage:
                    return GetEngagePlaybookDelta(actionType);

                case AITeamPlaybookCall.Disengage:
                    return GetDisengagePlaybookDelta(actionType);

                case AITeamPlaybookCall.EscortCarrier:
                    return GetEscortCarrierPlaybookDelta(
                        actionType,
                        playbookState.Lane,
                        playbookState.EscortRole,
                        selfCarrier);

                case AITeamPlaybookCall.PinchPressure:
                    return GetPinchPressurePlaybookDelta(actionType, playbookState.Lane);

                case AITeamPlaybookCall.BaitAndCollapse:
                    return GetBaitAndCollapsePlaybookDelta(actionType, playbookState.Lane);

                default:
                    return 0f;
            }
        }

        private float GetEngagePlaybookDelta(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return 14f;
                case AIActionType.Reposition:
                    return 9f;
                case AIActionType.UseSuper:
                    return 8f;
                case AIActionType.HoldRange:
                    return 7f;
                case AIActionType.Search:
                    return 6f;
                case AIActionType.Objective:
                    return 4f;
                case AIActionType.Peel:
                    return 3f;
                case AIActionType.Retreat:
                    return -10f;
                case AIActionType.Regroup:
                    return -8f;
                default:
                    return 0f;
            }
        }

        private float GetDisengagePlaybookDelta(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Retreat:
                    return 16f;
                case AIActionType.Regroup:
                    return 14f;
                case AIActionType.Reposition:
                    return 12f;
                case AIActionType.Peel:
                    return 8f;
                case AIActionType.HoldRange:
                    return 6f;
                case AIActionType.Approach:
                    return -18f;
                case AIActionType.Search:
                    return -10f;
                case AIActionType.Objective:
                    return -8f;
                case AIActionType.UseSuper:
                    return -6f;
                default:
                    return 0f;
            }
        }

        private float GetPushPlaybookDelta(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return 10f;
                case AIActionType.Reposition:
                    return 6f;
                case AIActionType.Objective:
                    return 8f;
                case AIActionType.Search:
                    return 6f;
                case AIActionType.Retreat:
                    return -6f;
                case AIActionType.Regroup:
                    return -4f;
                default:
                    return 0f;
            }
        }

        private float GetHoldPlaybookDelta(AIActionType actionType, bool selfCarrier)
        {
            switch (actionType)
            {
                case AIActionType.HoldRange:
                    return 12f;
                case AIActionType.Reposition:
                    return 4f;
                case AIActionType.Peel:
                    return 8f;
                case AIActionType.Regroup:
                    return selfCarrier ? 10f : 5f;
                case AIActionType.Retreat:
                    return selfCarrier ? 8f : 0f;
                case AIActionType.Approach:
                    return selfCarrier ? -16f : -6f;
                case AIActionType.Search:
                    return -6f;
                default:
                    return 0f;
            }
        }

        private float GetResetPlaybookDelta(AIActionType actionType, bool selfCarrier)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return selfCarrier ? -8f : 12f;
                case AIActionType.Reposition:
                    return 8f;
                case AIActionType.UseSuper:
                    return 6f;
                case AIActionType.Search:
                    return 8f;
                case AIActionType.Objective:
                    return 8f;
                case AIActionType.Regroup:
                    return selfCarrier ? 8f : -6f;
                default:
                    return 0f;
            }
        }

        private float GetEscortCarrierPlaybookDelta(
            AIActionType actionType,
            AITeamLaneAssignment lane,
            AITeamEscortFormationRole escortRole,
            bool selfCarrier)
        {
            if (selfCarrier || lane == AITeamLaneAssignment.Anchor)
            {
                switch (actionType)
                {
                    case AIActionType.Retreat:
                        return 14f;
                    case AIActionType.HoldRange:
                        return 12f;
                    case AIActionType.Regroup:
                        return 10f;
                    case AIActionType.Reposition:
                        return 6f;
                    case AIActionType.Approach:
                        return -18f;
                    case AIActionType.Objective:
                    case AIActionType.Search:
                        return -10f;
                    default:
                        return 0f;
                }
            }

            if (escortRole == AITeamEscortFormationRole.Screen)
            {
                switch (actionType)
                {
                    case AIActionType.Peel:
                        return 20f;
                    case AIActionType.HoldRange:
                        return 14f;
                    case AIActionType.Reposition:
                        return 12f;
                    case AIActionType.Approach:
                        return 4f;
                    case AIActionType.Regroup:
                        return 4f;
                    case AIActionType.Search:
                        return -10f;
                    default:
                        return 0f;
                }
            }

            if (escortRole == AITeamEscortFormationRole.PressureFlank)
            {
                switch (actionType)
                {
                    case AIActionType.Reposition:
                        return 16f;
                    case AIActionType.HoldRange:
                        return 12f;
                    case AIActionType.Peel:
                        return 10f;
                    case AIActionType.Approach:
                        return 6f;
                    case AIActionType.UseSuper:
                        return 4f;
                    case AIActionType.Search:
                        return -8f;
                    default:
                        return 0f;
                }
            }

            switch (actionType)
            {
                case AIActionType.Peel:
                    return 16f;
                case AIActionType.HoldRange:
                    return 10f;
                case AIActionType.Reposition:
                    return 8f;
                case AIActionType.Regroup:
                    return 6f;
                case AIActionType.Approach:
                    return -4f;
                case AIActionType.Search:
                    return -8f;
                default:
                    return 0f;
            }
        }

        private float GetPinchPressurePlaybookDelta(
            AIActionType actionType,
            AITeamLaneAssignment lane)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return lane == AITeamLaneAssignment.Flank ? 10f : 5f;
                case AIActionType.Reposition:
                    return lane == AITeamLaneAssignment.Flank ? 16f : 8f;
                case AIActionType.HoldRange:
                    return lane == AITeamLaneAssignment.Anchor ? 12f : 5f;
                case AIActionType.UseSuper:
                    return 4f;
                case AIActionType.Search:
                    return 8f;
                case AIActionType.Retreat:
                    return -6f;
                default:
                    return 0f;
            }
        }

        private float GetBaitAndCollapsePlaybookDelta(
            AIActionType actionType,
            AITeamLaneAssignment lane)
        {
            if (lane == AITeamLaneAssignment.Bait)
            {
                switch (actionType)
                {
                    case AIActionType.Retreat:
                        return 12f;
                    case AIActionType.HoldRange:
                        return 10f;
                    case AIActionType.Reposition:
                        return 8f;
                    case AIActionType.Approach:
                        return -12f;
                    default:
                        return 0f;
                }
            }

            switch (actionType)
            {
                case AIActionType.Approach:
                    return 12f;
                case AIActionType.Peel:
                    return 14f;
                case AIActionType.Reposition:
                    return 10f;
                case AIActionType.UseSuper:
                    return 6f;
                case AIActionType.Retreat:
                    return -6f;
                default:
                    return 0f;
            }
        }

        private bool CanCreatePlaybookScore(
            AIActionType actionType,
            AITargetInfo targetInfo,
            AITeamPlaybookState playbookState)
        {
            switch (actionType)
            {
                case AIActionType.Search:
                    return playbookState.HasPressurePoint &&
                           (targetInfo == null || !targetInfo.HasLiveTarget);

                case AIActionType.Peel:
                    return playbookState.Call == AITeamPlaybookCall.BaitAndCollapse ||
                           playbookState.Call == AITeamPlaybookCall.Disengage ||
                           (playbookState.Call == AITeamPlaybookCall.EscortCarrier &&
                            playbookState.HasEscortTargetPoint);

                case AIActionType.Regroup:
                case AIActionType.Retreat:
                    return playbookState.Call == AITeamPlaybookCall.Disengage ||
                           playbookState.Call == AITeamPlaybookCall.Reset ||
                           playbookState.Call == AITeamPlaybookCall.EscortCarrier;

                default:
                    return targetInfo != null &&
                           targetInfo.HasLiveTarget &&
                           (actionType == AIActionType.Approach ||
                            actionType == AIActionType.HoldRange ||
                            actionType == AIActionType.Reposition);
            }
        }

        private float GetPlaybookWeight()
        {
            if (_profile == null || !_profile.UseTeamRoleCoordination)
                return 0f;

            float teamplay = _self != null && _self.Definition != null
                ? _self.Definition.TeamplayWeight
                : 1f;

            return Mathf.Clamp(
                teamplay * Mathf.Max(0.35f, _profile.TeamRoleCoordinationWeight),
                0.25f,
                1.50f);
        }

        private float CalculateTeamRoleDelta(
            AIActionScore actionScore,
            AITargetInfo targetInfo,
            int approachAllies,
            int peelAllies,
            int regroupAllies,
            int objectiveAllies,
            int searchAllies)
        {
            if (actionScore.Score <= 0f)
                return 0f;

            float weight = Mathf.Max(0f, _profile.TeamRoleCoordinationWeight);
            if (weight <= 0f)
                return 0f;

            float delta = 0f;
            int capacity = GetTeamActionCapacity(actionScore.ActionType);
            int alliedSameAction = GetKnownAlliedActionCount(
                actionScore.ActionType,
                approachAllies,
                peelAllies,
                regroupAllies,
                objectiveAllies,
                searchAllies);

            if (capacity >= 0)
            {
                int excess = (alliedSameAction + 1) - capacity;
                if (excess > 0)
                    delta -= excess * Mathf.Max(0f, _profile.TeamActionCrowdingPenalty);
            }

            if (targetInfo.HasLiveTarget)
            {
                if (actionScore.ActionType == AIActionType.Approach &&
                    CanServeFrontline() &&
                    approachAllies == 0)
                {
                    delta += _profile.TeamFrontlineNeedBonus;
                }

                if ((actionScore.ActionType == AIActionType.HoldRange ||
                     actionScore.ActionType == AIActionType.Reposition) &&
                    IsBacklineRole() &&
                    approachAllies > 0)
                {
                    delta += _profile.TeamBacklineAnchorBonus;
                }
            }

            return delta * weight;
        }

        private int GetTeamActionCapacity(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return Mathf.Max(0, _profile.MaxTeamApproachers);

                case AIActionType.Peel:
                    return Mathf.Max(0, _profile.MaxTeamPeelResponders);

                case AIActionType.Regroup:
                    return Mathf.Max(0, _profile.MaxTeamRegroupResponders);

                case AIActionType.Objective:
                    return Mathf.Max(0, _profile.MaxTeamObjectiveMovers);

                case AIActionType.Search:
                    return 1;

                default:
                    return -1;
            }
        }

        private int GetKnownAlliedActionCount(
            AIActionType actionType,
            int approachAllies,
            int peelAllies,
            int regroupAllies,
            int objectiveAllies,
            int searchAllies)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                    return approachAllies;

                case AIActionType.Peel:
                    return peelAllies;

                case AIActionType.Regroup:
                    return regroupAllies;

                case AIActionType.Objective:
                    return objectiveAllies;

                case AIActionType.Search:
                    return searchAllies;

                default:
                    return 0;
            }
        }

        private float ClampActionScore(AIActionType actionType, float score)
        {
            float maxScore = IsEmergencyAction(actionType)
                ? MaxEmergencyActionScore
                : MaxNormalActionScore;

            return Mathf.Clamp(score, MinActionScore, maxScore);
        }

        private bool IsEmergencyAction(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Retreat:
                case AIActionType.Evade:
                case AIActionType.UseSuper:
                case AIActionType.Peel:
                    return true;

                default:
                    return false;
            }
        }

        private bool CanServeFrontline()
        {
            if (!(IsTank || IsFighter || IsAssassin))
                return false;

            if (_self.State == null)
                return true;

            float healthRatio = _self.State.CurrentHealth /
                                Mathf.Max(1f, _self.State.MaxHealth.Value);

            return healthRatio >= Mathf.Max(0.35f, _profile.LowHealthRetreatRatio + 0.10f);
        }

        private bool IsBacklineRole()
        {
            return IsSniper || IsSupport || IsController || IsArtillery;
        }

        private string AppendRoleDebug(string current, string value)
        {
            return string.IsNullOrEmpty(current)
                ? value
                : $"{current},{value}";
        }

        private float AddArchetypeBias(
            float score,
            float sniper = 0f,
            float tank = 0f,
            float assassin = 0f,
            float support = 0f,
            float fighter = 0f,
            float controller = 0f,
            float artillery = 0f)
        {
            if (IsSniper) score += sniper;
            if (IsTank) score += tank;
            if (IsAssassin) score += assassin;
            if (IsSupport) score += support;
            if (IsFighter) score += fighter;
            if (IsController) score += controller;
            if (IsArtillery) score += artillery;

            return score;
        }

        private float GetMacroActionDelta(
            AIActionType actionType,
            AIGameModeMacroState macroState,
            int selfCarriedGems,
            int targetCarriedGems,
            int allyCarriedGems)
        {
            return GetMacroActionDelta(
                actionType,
                macroState,
                selfCarriedGems,
                targetCarriedGems,
                allyCarriedGems,
                out _);
        }

        private float GetMacroActionDelta(
            AIActionType actionType,
            AIGameModeMacroState macroState,
            int selfCarriedGems,
            int targetCarriedGems,
            int allyCarriedGems,
            out string reason)
        {
            reason = "macro_none";

            if (_profile == null || _profile.MacroActionBiasWeight <= 0f)
                return 0f;

            var context = new AIMacroActionContext(
                macroState,
                selfCarriedGems,
                targetCarriedGems,
                allyCarriedGems);

            AIMacroActionPolicyResult result =
                AIMacroActionPolicy.Evaluate(actionType, context);

            reason = result.Reason;
            if (!result.HasDelta)
                return 0f;

            return result.Delta * Mathf.Max(0f, _profile.MacroActionBiasWeight);
        }

        private AIActionScore ScoreRetreat(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (_self.State == null)
                return new AIActionScore(AIActionType.Retreat, 0f);

            float score = 0f;
            bool hasRetreatReason = false;
            float healthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);

            if (healthRatio <= _profile.LowHealthRetreatRatio)
            {
                score += 70f;
                hasRetreatReason = true;
            }

            if (_self.State.HasStatus(StatusEffectType.Burn))
            {
                score += 25f;
                hasRetreatReason = true;
            }

            if (_self.State.HasStatus(StatusEffectType.Stun))
                return new AIActionScore(AIActionType.Retreat, 0f);

            if (targetInfo.HasLiveTarget)
            {
                float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);
                if (dist <= _profile.GetTooCloseDistance(GetAbilityIdealRange()))
                {
                    score += 20f;
                    hasRetreatReason = true;
                }
            }

            float reactivePressure = GetReactiveDamagePressure(currentTick);
            if (reactivePressure > 0f)
            {
                score += reactivePressure * _profile.ReactiveRetreatPressureBonus;
                hasRetreatReason = true;

                if (healthRatio <= _profile.ReactiveEmergencyHealthRatio)
                    score += reactivePressure * 30f;
            }

            int selfCarriedGems = _self.State.CarriedGemCount;
            if (selfCarriedGems > 0)
            {
                score += 6f * selfCarriedGems;
                hasRetreatReason = true;
            }

            float macroDelta = GetMacroActionDelta(
                AIActionType.Retreat,
                macroState,
                selfCarriedGems,
                0,
                0);
            if (Mathf.Abs(macroDelta) > 0.01f)
            {
                score += macroDelta;
                if (macroDelta > 0f)
                    hasRetreatReason = true;
            }

            if (!hasRetreatReason)
                return new AIActionScore(AIActionType.Retreat, 0f);

            float roleSurvival = _self.Definition != null ? _self.Definition.SurvivalInstinct : 1f;
            score *= roleSurvival;

            if (IsSniper) score += 10f;
            if (IsSupport) score += 8f;
            if (IsTank) score -= 10f;
            if (IsAssassin) score -= 6f;
            if (IsController) score += 5f;
            if (IsArtillery) score += 12f;

            return MakeScore(
    AIActionType.Retreat,
    score,
    _profile.RetreatWeight,
    allowEmergencyScore: true);
        }

        private AIActionScore ScoreEvade()
        {
            if (_dangerMemory == null ||
                !_dangerMemory.HasDanger ||
                _dangerMemory.Pressure < _profile.DangerEvadePressureThreshold)
            {
                return new AIActionScore(AIActionType.Evade, 0f);
            }

            float score = 35f + (_dangerMemory.Pressure * _profile.DangerEvadeScoreBonus);

            if (_self.State != null)
            {
                float healthRatio = _self.State.CurrentHealth /
                                    Mathf.Max(1f, _self.State.MaxHealth.Value);

                if (healthRatio <= _profile.LowHealthRetreatRatio)
                    score += 15f;
            }

            score = AddArchetypeBias(
                score,
                sniper: 10f,
                tank: -10f,
                assassin: 4f,
                support: 8f,
                fighter: 0f,
                controller: 5f,
                artillery: 12f);

            return MakeScore(
                AIActionType.Evade,
                score,
                1f,
                allowEmergencyScore: true);
        }

        private AIActionScore ScoreUseSuper(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState)
        {
            if (_self.State == null || !_self.State.SuperCharge.IsReady || !targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.UseSuper, 0f);

            float score = 50f;

            if (targetInfo.Target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                float targetHealthRatio = targetBrawler.State.CurrentHealth /
                                          Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);

                if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                    score += 40f;

                if (targetBrawler.State.HasStatus(StatusEffectType.Slow))
                    score += 20f;

                if (targetHealthRatio <= _profile.SuperLowHealthTargetThreshold)
                    score += 25f;
            }

            if (IsAssassin) score += 15f;
            if (IsTank) score += 10f;
            if (IsSupport) score += 6f;
            if (IsSniper) score += 4f;
            if (IsController) score += 14f;
            if (IsArtillery) score += 8f;

            // Gem Grab: target carrying many gems → strong incentive to
            // burst them down. Killing a 3-gem carrier scatters 3 gems back
            // to the enemy (or your team if you grab them). +5 per gem
            // means a 3-gem carrier gets +15 — same magnitude as the
            // Assassin baseline, so even non-burst archetypes will swing
            // their super at a fat carrier.
            if (targetInfo.Target is BrawlerController carrierTarget &&
                carrierTarget.State != null)
            {
                int targetCarriedGems = carrierTarget.State.CarriedGemCount;
                score += 5f * targetCarriedGems;
                score += GetMacroActionDelta(
                    AIActionType.UseSuper,
                    macroState,
                    0,
                    targetCarriedGems,
                    0);
            }

            return MakeScore(
     AIActionType.UseSuper,
     score,
     _profile.SuperWeight,
     allowEmergencyScore: true);
        }

        private AIActionScore ScoreHoldRange(AITargetInfo targetInfo)
        {
            if (!targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.HoldRange, 0f);

            float attackRange = GetAbilityMaxRange();
            float idealRange = GetAbilityIdealRange();
            float preferredRange = _profile.GetPreferredAttackRange(idealRange);

            float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);
            float score = 0f;

            if (dist <= attackRange && dist >= preferredRange * 0.60f)
                score += 55f;

            if (targetInfo.Target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                    score += 25f;
            }

            if (IsSniper) score += 20f;
            if (IsSupport) score += 10f;
            if (IsTank) score -= 12f;
            if (IsAssassin) score -= 6f;
            if (IsController) score += 12f;
            if (IsArtillery) score += 18f;

            float ammoPressure = GetAmmoPressure();
            if (ammoPressure > 0f)
            {
                float reloadHoldBonus = IsTank ? 5f : 12f;
                score += ammoPressure * reloadHoldBonus;
            }

            return MakeScore(
      AIActionType.HoldRange,
      score,
      _profile.HoldRangeWeight);
        }

        private AIActionScore ScoreReposition(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (!targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.Reposition, 0f);

            float idealRange = GetAbilityIdealRange();
            float tooClose = _profile.GetTooCloseDistance(idealRange);
            float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);

            float score = 0f;
            if (dist < tooClose)
                score += 60f;

            float reactivePressure = GetReactiveDamagePressure(currentTick);
            if (reactivePressure > 0f)
                score += reactivePressure * _profile.ReactiveRepositionPressureBonus;

            if (IsSniper) score += 15f;
            if (IsSupport) score += 10f;
            if (IsAssassin) score += 6f;
            if (IsTank) score -= 8f;
            if (IsController) score += 10f;
            if (IsArtillery) score += 14f;

            float ammoPressure = GetAmmoPressure();
            if (ammoPressure > 0f)
            {
                float reloadRepositionBonus = IsArtillery ? 18f : 14f;
                score += ammoPressure * reloadRepositionBonus;
            }

            CloseRangeMatchupEvaluation closeRange =
                EvaluateCloseRangeMatchup(
                    targetInfo,
                    dist,
                    currentTick,
                    macroState);

            if (closeRange.IsActive)
                score += closeRange.RepositionBonus;

            return MakeScore(
     AIActionType.Reposition,
     score,
     _profile.RepositionWeight);
        }

        private AIActionScore ScoreApproach(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (!targetInfo.HasLiveTarget)
            {
                _chaseDisengageMemory.Reset();
                _lastChaseDebug = "Chase=None Reason=no_target";
                return new AIActionScore(AIActionType.Approach, 0f);
            }

            float attackRange = GetAbilityMaxRange();
            float dist = Vector3.Distance(_self.Position, targetInfo.Target.Position);

            float score = 0f;
            if (dist > attackRange + _profile.AttackRangeBuffer)
                score += 50f;

            if (targetInfo.Target is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                if (targetBrawler.State.HasStatus(StatusEffectType.Stun))
                    score += 20f;

                if (targetBrawler.State.HasStatus(StatusEffectType.Slow))
                    score += 15f;
            }

            score += GetChaseDisciplineDelta(
                targetInfo,
                dist,
                currentTick,
                macroState);

            CloseRangeMatchupEvaluation closeRange =
                EvaluateCloseRangeMatchup(
                    targetInfo,
                    dist,
                    currentTick,
                    macroState);

            if (closeRange.IsActive)
            {
                score += closeRange.ApproachDelta;
                _lastChaseDebug = $"{_lastChaseDebug} CloseRole={closeRange.Reason}";
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetFocusTarget(currentTick, out var focusTarget) &&
                SpatialEntityUtility.IsAlive(focusTarget) &&
                targetInfo.Target.EntityID == focusTarget.EntityID)
            {
                float focusBonus = _profile.FocusFireWeight;
                if (closeRange.IsActive && closeRange.ApproachDelta < 0f)
                    focusBonus *= 0.35f;

                score += focusBonus;
            }

            float reactivePressure = GetReactiveDamagePressure(currentTick);
            if (reactivePressure > 0f &&
                _reactiveMemory != null &&
                _reactiveMemory.TryGetRecentAttacker(
                    currentTick,
                    _profile.ReactiveDamageMemoryTicks,
                    out ISpatialEntity recentAttacker) &&
                SpatialEntityUtility.IsSameEntity(targetInfo.Target, recentAttacker))
            {
                float reactiveBonus = _profile.ReactiveAttackerFocusBonus * reactivePressure;
                if (closeRange.IsActive && closeRange.ApproachDelta < 0f)
                    reactiveBonus *= 0.35f;

                score += reactiveBonus;
            }

            float roleAggression = _self.Definition != null ? _self.Definition.Aggression : 1f;
            score *= roleAggression;

            if (IsTank) score += 12f;
            if (IsAssassin) score += 10f;
            if (IsSniper) score -= 8f;
            if (IsSupport) score -= 6f;
            if (IsController) score -= 3f;
            if (IsArtillery) score -= 10f;

            float ammoPressure = GetAmmoPressure();
            if (ammoPressure > 0f)
            {
                float lowAmmoApproachPenalty = IsTank || IsAssassin ? 8f : 18f;
                score -= ammoPressure * lowAmmoApproachPenalty;
            }

            // Gem Grab: behind on gems → push harder. The behind-ness check
            // is null-safe so non-Gem-Grab matches and unit-test contexts
            // skip this branch entirely.
            if (GemGrabMode.Instance != null && GemGrabMode.Instance.IsTeamBehind(_self.Team))
                score += 8f;

            score += GetMacroActionDelta(
                AIActionType.Approach,
                macroState,
                _self.State != null ? _self.State.CarriedGemCount : 0,
                0,
                0);

            return MakeScore(
     AIActionType.Approach,
     score,
     _profile.ApproachWeight);
        }

        private AIActionScore ScoreSearch(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            // NEVER SEARCH DURING ACTIVE COMBAT
            if (targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.Search, 0f);

            float score = 0f;

            if (!targetInfo.HasLiveTarget &&
    targetInfo.HasRecentMemory(currentTick, _profile.MemoryDurationTicks))
            {
                score += 30f;
            }

            if (AITeamMemory.TryGetRecentHotspot(
                _self.Team,
                currentTick,
                _profile.SharedHotspotMemoryTicks,
                out _))
            {
                score += 20f;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetThreatCenter(currentTick, out _, out float threatPressure))
            {
                score += Mathf.Clamp(threatPressure * 5f, 6f, 18f);
            }
            else if (_teamCoordinator != null &&
                     _teamCoordinator.TryGetEnemyHotspot(currentTick, out _, out float hotspotPressure))
            {
                score += Mathf.Clamp(hotspotPressure * 4f, 4f, 14f);
            }

            AIGemPickupDecision gemDecision = AIGemPickupDecision.None("not_checked");
            bool hasGemPickupDecision = TryResolveGemPickupDecision(
                    macroState,
                    currentTick,
                    out gemDecision);

            if (hasGemPickupDecision)
            {
                score += gemDecision.ShouldPickup
                    ? gemDecision.Score
                    : Mathf.Max(0f, gemDecision.Score * 0.20f);
            }

            AIGemMineControlEvaluation mineControl =
                AIGemGrabObjectiveUtility.EvaluateMineControl(
                    AIActionType.Search,
                    BuildGemMineControlContext(
                        macroState,
                        hasLiveTarget: false,
                        allyPressure: 0f,
                        gemDecision));
            if (mineControl.HasDelta)
            {
                score += mineControl.Delta;
                _lastGemPickupDebug += $" Mine={mineControl.Reason}_{mineControl.Delta:+0.0;-0.0}";
            }

            float laneSearchBonus = GetLaneDisciplineScoreBonus(
                currentTick,
                _profile.LaneHoldSearchScore,
                out _);
            if (laneSearchBonus > 0.01f)
            {
                score += laneSearchBonus;
            }

            score += GetMacroActionDelta(
                AIActionType.Search,
                macroState,
                0,
                0,
                0);

            return MakeScore(
      AIActionType.Search,
      score,
      _profile.SearchWeight);
        }

        private bool TryResolveGemPickupDecision(
            AIGameModeMacroState macroState,
            uint currentTick,
            out AIGemPickupDecision decision)
        {
            if (_hasGemPickupDecisionCache &&
                _lastGemPickupDecisionTick == currentTick)
            {
                decision = _lastGemPickupDecision;
                return decision.HasPickup &&
                       (_lastGemPickupShouldPickup || decision.Score > 0f);
            }

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

            bool shouldPickup = AIGemGrabObjectiveUtility.TryFindBestPickup(
                _self,
                _profile,
                macroState,
                hasThreatCenter,
                threatCenter,
                threatPressure,
                hasEnemyHotspot,
                enemyHotspot,
                hotspotPressure,
                out decision);

            _lastGemPickupDebug = decision.GetDebugSummary();
            _hasGemPickupDecisionCache = true;
            _lastGemPickupDecisionTick = currentTick;
            _lastGemPickupDecision = decision;
            _lastGemPickupShouldPickup = shouldPickup;

            return decision.HasPickup && (shouldPickup || decision.Score > 0f);
        }

        private AIActionScore ScoreWander()
        {
            return new AIActionScore(AIActionType.Wander, 5f * _profile.WanderWeight);
        }

        private AIActionScore ScoreObjective(
            AITargetInfo targetInfo,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            _lastObjectiveAllyPressure = 0f;
            _lastObjectiveCrowdingPenalty = 0f;
            _lastObjectiveRawScore = 0f;
            _lastObjectiveFinalScore = 0f;
            _lastObjectiveScoreReason = "not_evaluated";

            if (_objectiveMemory == null ||
                !_objectiveMemory.TryGetBestObjective(
                    _self.Position,
                    _profile.PreferredObjective,
                    _self.Team,
                    out AIObjectiveCandidate objective))
            {
                _lastObjectiveScoreReason = "no_objectives";
                return new AIActionScore(AIActionType.Objective, 0f);
            }

            bool isBrawlBallObjective =
                macroState.Mode == GameModeId.BrawlBall &&
                objective.IsRuntime &&
                objective.ObjectiveType == AIObjectiveType.Ball;

            // Combat usually owns the moment, but Brawl Ball is possession-led:
            // a visible enemy should not make bots forget the loose ball,
            // friendly carrier, or enemy carrier.
            if (targetInfo.HasLiveTarget && !isBrawlBallObjective)
            {
                _lastObjectiveScoreReason = "combat_target_exists";
                return new AIActionScore(AIActionType.Objective, 0f);
            }

            Vector3 objectivePosition = objective.Position;
            float objectiveRadius = Mathf.Max(0.5f, objective.Radius);

            float dist = Vector3.Distance(
                _self.Position,
                objectivePosition);

            float score = 45f;
            _lastObjectiveScoreReason =
                $"base_45|type_{objective.ObjectiveType}|radius_{objectiveRadius:0.0}";

            if (objective.IsRuntime)
                _lastObjectiveScoreReason += "|runtime";

            if (isBrawlBallObjective)
            {
                float ballDelta = macroState.Call == AIGameModeMacroCall.Reset
                    ? 54f
                    : macroState.Call == AIGameModeMacroCall.Push
                        ? 48f
                        : 60f;
                score += ballDelta;
                _lastObjectiveScoreReason += $"|ball_focus_+{ballDelta:0.0}";
            }

            float weightDelta = Mathf.Clamp((objective.Weight - 50f) * 0.15f, -10f, 18f);
            if (Mathf.Abs(weightDelta) > 0.01f)
            {
                score += weightDelta;
                _lastObjectiveScoreReason += $"|weight_{weightDelta:+0.0;-0.0}";
            }

            float controlDelta = AIObjectiveControlUtility.GetUtilityScoreDelta(
                objective.ControlState);
            if (Mathf.Abs(controlDelta) > 0.01f)
            {
                score += controlDelta;
                _lastObjectiveScoreReason +=
                    $"|control_{objective.ControlState}_{controlDelta:+0.0;-0.0}";
            }

            float presenceDelta = AIObjectiveControlUtility.GetUtilityPresenceDelta(
                objective.FriendlyPresence,
                objective.EnemyPresence);
            if (Mathf.Abs(presenceDelta) > 0.01f)
            {
                score += presenceDelta;
                _lastObjectiveScoreReason +=
                    $"|presence_{objective.FriendlyPresence}:{objective.EnemyPresence}_{presenceDelta:+0.0;-0.0}";
            }

            // Far from objective: moving toward it is useful.
            float farDistance = Mathf.Max(4f, objectiveRadius + 1.5f);
            if (dist > farDistance)
            {
                score += 20f;
                _lastObjectiveScoreReason += "|far_+20";
            }

            // Already near objective: no need to over-prioritize objective movement.
            float nearDistance = Mathf.Max(1.5f, objectiveRadius * 0.75f);
            if (dist < nearDistance)
            {
                score -= 20f;
                _lastObjectiveScoreReason += "|near_-20";
            }

            // Decision-layer anti-clumping:
            // If allies are already near the objective, reduce desire to also go there.
            float allyPressure = CalculateNearbyAllyPressure(
                objectivePosition,
                Mathf.Max(4.5f, objectiveRadius + 1.5f));

            float crowdingPenalty = allyPressure * GetObjectiveCrowdingPenalty();
            if (isBrawlBallObjective)
                crowdingPenalty *= 0.35f;

            _lastObjectiveAllyPressure = allyPressure;
            _lastObjectiveCrowdingPenalty = crowdingPenalty;

            score -= crowdingPenalty;

            if (crowdingPenalty > 0.01f)
                _lastObjectiveScoreReason += $"|crowding_-{crowdingPenalty:0.0}";

            float beforeArchetype = score;

            // Archetype shaping.
            // Tanks/controllers/artillery like objective pressure more.
            // Assassins prefer side pressure instead of sitting center.
            score = AddArchetypeBias(
                score,
                sniper: -5f,
                tank: 10f,
                assassin: -8f,
                support: 0f,
                fighter: 2f,
                controller: 8f,
                artillery: 5f);

            float archetypeDelta = score - beforeArchetype;
            if (Mathf.Abs(archetypeDelta) > 0.01f)
                _lastObjectiveScoreReason += $"|archetype_{archetypeDelta:+0.0;-0.0}";

            // Gem Grab: if behind, encourage objective/map pressure slightly.
            if (GemGrabMode.Instance != null && GemGrabMode.Instance.IsTeamBehind(_self.Team))
            {
                score += 8f;
                _lastObjectiveScoreReason += "|behind_+8";
            }

            float objectiveMacroDelta = GetMacroActionDelta(
                AIActionType.Objective,
                macroState,
                _self.State != null ? _self.State.CarriedGemCount : 0,
                0,
                0,
                out string objectiveMacroReason);
            if (Mathf.Abs(objectiveMacroDelta) > 0.01f)
            {
                score += objectiveMacroDelta;
                _lastObjectiveScoreReason += $"|{objectiveMacroReason}_{objectiveMacroDelta:+0.0;-0.0}";
            }

            TryResolveGemPickupDecision(
                macroState,
                currentTick,
                out AIGemPickupDecision objectiveGemDecision);
            AIGemMineControlEvaluation objectiveMineControl =
                AIGemGrabObjectiveUtility.EvaluateMineControl(
                    AIActionType.Objective,
                    BuildGemMineControlContext(
                        macroState,
                        hasLiveTarget: false,
                        allyPressure,
                        objectiveGemDecision));
            if (objectiveMineControl.HasDelta)
            {
                score += objectiveMineControl.Delta;
                _lastObjectiveScoreReason +=
                    $"|mine_{objectiveMineControl.Reason}_{objectiveMineControl.Delta:+0.0;-0.0}";
            }

            float opponentObjectiveNeglect = AIOpponentModel.GetMaxObjectiveNeglect(
                _self.Team,
                currentTick,
                360u);

            if (opponentObjectiveNeglect > 0.20f)
            {
                float neglectBonus = opponentObjectiveNeglect * 14f;
                score += neglectBonus;
                _lastObjectiveScoreReason += $"|opp_neglect_+{neglectBonus:0.0}";
            }

            float laneBonus = GetLaneDisciplineScoreBonus(
                currentTick,
                _profile.LaneHoldObjectiveBonus,
                out string laneReason);
            if (laneBonus > 0.01f)
            {
                score += laneBonus;
                _lastObjectiveScoreReason += $"|lane_{laneBonus:+0.0;-0.0}_{laneReason}";
            }

            _lastObjectiveRawScore = score;

            AIActionScore finalScore = MakeScore(
                AIActionType.Objective,
                score,
                _profile.ObjectiveWeight);

            _lastObjectiveFinalScore = finalScore.Score;

            return finalScore;
        }

        private AIGemMineControlContext BuildGemMineControlContext(
            AIGameModeMacroState macroState,
            bool hasLiveTarget,
            float allyPressure,
            AIGemPickupDecision gemDecision)
        {
            float healthRatio = _self != null && _self.State != null
                ? _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value)
                : 1f;

            return new AIGemMineControlContext(
                macroState,
                _self != null && _self.State != null
                    ? _self.State.CarriedGemCount
                    : 0,
                healthRatio,
                allyPressure,
                hasLiveTarget,
                gemDecision.HasPickup,
                gemDecision.ShouldPickup,
                gemDecision.Score);
        }

        private AIActionScore ScoreRegroup(
     AITargetInfo targetInfo,
     uint currentTick,
     AIGameModeMacroState macroState)
        {
            if (_teamCoordinator == null)
                return new AIActionScore(AIActionType.Regroup, 0f);

            // Never regroup during active combat.
            if (targetInfo.HasLiveTarget)
                return new AIActionScore(AIActionType.Regroup, 0f);

            if (!_teamCoordinator.TryGetRegroupPoint(currentTick, out _))
                return new AIActionScore(AIActionType.Regroup, 0f);

            float score = 45f;

            float teamplay = _self.Definition != null
                ? _self.Definition.TeamplayWeight
                : 1f;

            score *= teamplay;

            score = AddArchetypeBias(
                score,
                sniper: 8f,
                tank: -8f,
                assassin: -12f,
                support: 10f,
                fighter: 0f,
                controller: 6f,
                artillery: 8f);

            if (_self.State != null)
            {
                float healthRatio = _self.State.CurrentHealth /
                                    Mathf.Max(1f, _self.State.MaxHealth.Value);

                if (healthRatio <= _profile.RegroupHealthThreshold)
                    score += 20f;

                // Gem carriers should regroup more safely.
                score += 5f * _self.State.CarriedGemCount;

                score += GetMacroActionDelta(
                    AIActionType.Regroup,
                    macroState,
                    _self.State.CarriedGemCount,
                    0,
                    0);
            }

            return MakeScore(
                AIActionType.Regroup,
                score,
                _profile.RegroupWeight);
        }


        private AIActionScore ScorePeel(
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (_teamCoordinator == null ||
                !_teamCoordinator.TryGetAllyUnderThreat(currentTick, out var ally) ||
                ally == null)
            {
                return new AIActionScore(AIActionType.Peel, 0f);
            }

            float score = 40f;

            int allyCarriedGems = ally.State != null
                ? ally.State.CarriedGemCount
                : 0;

            if (allyCarriedGems > 0)
                score += 8f * allyCarriedGems;

            score += GetMacroActionDelta(
                AIActionType.Peel,
                macroState,
                0,
                0,
                allyCarriedGems);

            float teamplay = _self.Definition != null ? _self.Definition.TeamplayWeight : 1f;
            score *= teamplay;

            if (IsSupport) score += 20f;
            if (IsTank) score += 12f;
            if (IsAssassin) score -= 8f;
            if (IsController) score += 15f;
            if (IsArtillery) score += 10f;

            return MakeScore(
                AIActionType.Peel,
                score,
                _profile.PeelWeight,
                allowEmergencyScore: true);
        }

        private float GetAbilityIdealRange()
        {
            var attack = _self.State != null
                ? _self.State.GetCurrentMainAttackDefinition()
                : _self.Definition?.MainAttack;
            return attack != null ? attack.GetAIIdealRange() : 6f;
        }

        private float GetAbilityMaxRange()
        {
            var attack = _self.State != null
                ? _self.State.GetCurrentMainAttackDefinition()
                : _self.Definition?.MainAttack;
            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private float GetReactiveDamagePressure(uint currentTick)
        {
            return _reactiveMemory != null
                ? _reactiveMemory.GetDamagePressure(currentTick, _profile.ReactiveDamageMemoryTicks)
                : 0f;
        }

        private float GetAmmoPressure()
        {
            return AICombatMicroUtility.GetAmmoPressure(
                GetAvailableAmmo(),
                GetMaxAmmo(),
                GetCurrentAmmo());
        }

        private int GetAvailableAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.AvailableBars
                : 3;
        }

        private int GetMaxAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.MaxAmmo
                : 3;
        }

        private float GetCurrentAmmo()
        {
            return _self != null && _self.State != null && _self.State.Ammo != null
                ? _self.State.Ammo.CurrentAmmo
                : GetAvailableAmmo();
        }

        private float CalculateNearbyAllyPressure(Vector3 position, float radius)
        {
            if (SimulationClock.Grid == null)
                return 0f;

            _nearbyAllyBuffer.Clear();

            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                position,
                radius,
                _nearbyAllyBuffer);

            float pressure = 0f;

            for (int i = 0; i < _nearbyAllyBuffer.Count; i++)
            {
                ISpatialEntity entity = _nearbyAllyBuffer[i];

                if (!SpatialEntityUtility.IsAlive(entity))
                    continue;

                if (entity == _self)
                    continue;

                if (entity.Team != _self.Team)
                    continue;

                if (!(entity is BrawlerController other))
                    continue;

                if (other.State == null || other.State.IsDead)
                    continue;

                float dist = Vector3.Distance(position, other.Position);
                float closeness = 1f - Mathf.Clamp01(dist / radius);

                pressure += closeness;
            }

            return pressure;
        }

        private bool CanHoldAssignedLane(uint currentTick, out string reason)
        {
            if (_hasLaneEvaluation && _lastLaneEvaluationTick == currentTick)
            {
                reason = _lastLaneHoldReason;
                return _lastCanHoldLane;
            }

            _hasLaneEvaluation = true;
            _lastLaneEvaluationTick = currentTick;
            _lastCanHoldLane = false;
            _lastLaneHoldReason = "lane_disabled";

            if (_profile == null || !_profile.UseLaneDiscipline)
            {
                reason = _lastLaneHoldReason;
                return false;
            }

            Vector3 anchorPoint = Vector3.zero;
            bool hasAnchor = false;
            AITeamLaneAssignment lane = AILaneDisciplineUtility.ResolveAssignedLane(_self.EntityID);

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetLaneOwnership(
                    currentTick,
                    out AITeamLaneOwnershipSnapshot laneOwnership) &&
                laneOwnership.HasRecommendedLane)
            {
                lane = laneOwnership.RecommendedLane;
            }

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetPlaybookState(currentTick, out AITeamPlaybookState playbookState))
            {
                if (playbookState.Lane != AITeamLaneAssignment.None)
                    lane = playbookState.Lane;

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
                    _self.Position,
                    _profile.PreferredObjective,
                    _self.Team,
                    out AIObjectiveCandidate objective))
            {
                anchorPoint = objective.Position;
                hasAnchor = true;
            }

            if (!hasAnchor)
            {
                _lastLaneHoldReason = "lane_no_anchor";
                reason = _lastLaneHoldReason;
                return false;
            }

            _lastCanHoldLane = AILaneDisciplineUtility.TryResolveLaneHoldPoint(
                _self,
                _profile,
                lane,
                anchorPoint,
                out _,
                out _lastLaneHoldReason);

            reason = _lastLaneHoldReason;
            return _lastCanHoldLane;
        }

        private float GetLaneDisciplineScoreBonus(
            uint currentTick,
            float baseBonus,
            out string reason)
        {
            reason = "lane_none";

            if (!CanHoldAssignedLane(currentTick, out string laneReason))
            {
                reason = laneReason;
                return 0f;
            }

            float laneWeight = Mathf.Max(0f, _profile.LaneDisciplineWeight);
            if (laneWeight <= 0f || baseBonus <= 0f)
            {
                reason = laneReason;
                return 0f;
            }

            float multiplier = 1f;
            string stateReason = "hold";

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetLaneOwnership(
                    currentTick,
                    out AITeamLaneOwnershipSnapshot laneOwnership))
            {
                if (laneOwnership.AssignedLaneAbandoned)
                {
                    multiplier += 0.90f;
                    stateReason = "recover";
                }
                else if (laneOwnership.ShouldRotate)
                {
                    multiplier += 1.10f;
                    stateReason = "rotate";
                }
                else if (laneOwnership.RotationPending)
                {
                    multiplier += 0.35f;
                    stateReason = "pending";
                }
                else if (laneOwnership.CurrentLaneOverOwned)
                {
                    multiplier *= 0.70f;
                    stateReason = "overowned_hold";
                }
            }

            reason = $"{stateReason}_{laneReason}";
            return baseBonus * laneWeight * multiplier;
        }

        private struct AIRoleMacroContext
        {
            public BrawlerArchetype Archetype;
            public bool HasTarget;
            public bool TargetPickWindow;
            public bool HighValueTarget;
            public bool TargetDisabled;
            public bool SelfCarrier;
            public bool CarrierSafety;
            public bool AllyUnderThreat;
            public bool MacroPush;
            public bool MacroHold;
            public bool MacroReset;
            public bool ObjectivePressure;
            public int SelfCarriedGems;
            public int TargetCarriedGems;
            public float TargetHealthRatio;
            public float TargetDistance;
        }

        private struct AIEngagementRiskContext
        {
            public bool HasTarget;
            public bool PickWindow;
            public bool SupportedFight;
            public bool BadDive;
            public bool SelfCarrier;
            public int AllyCount;
            public int EnemyCount;
            public int TeamActionSupport;
            public int SelfCarriedGems;
            public int TargetCarriedGems;
            public float AllyPressure;
            public float EnemyPressure;
            public float RiskPressure;
            public float TargetHealthRatio;
            public float TargetDistance;
        }

        private struct AIPressureRotationContext
        {
            public bool HasTarget;
            public bool HasEnemyHotspot;
            public bool HasThreatCenter;
            public bool TargetNearHotspot;
            public bool TargetNearThreatCenter;
            public Vector3 TargetPosition;
            public Vector3 HotspotPosition;
            public Vector3 ThreatCenterPosition;
            public float HotspotPressure;
            public float ThreatPressure;
            public float HotspotRelevance;
            public float ThreatRelevance;
        }

        private struct AIRoleMatchupContext
        {
            public bool HasMatchupPressure;
            public bool OwnShortRange;
            public bool TargetShortRange;
            public bool ShortRangeBadChase;
            public bool RangedKiteWindow;
            public bool RangedPunishWindow;
            public bool ObjectiveOverride;
            public bool SuperCanCloseGap;
            public bool WaitingForCloseRangeSuper;
            public BrawlerArchetype OwnArchetype;
            public BrawlerArchetype TargetArchetype;
            public float OwnRange;
            public float TargetRange;
            public float TargetDistance;
        }

        private struct AICoverPeekContext
        {
            public bool HasCoverSignal;
            public bool SelfNearCover;
            public bool TargetNearCover;
            public bool HasCoverBetween;
            public bool DirectFire;
            public bool CoverPeekWindow;
            public bool BlockedFireLane;
            public bool ExposedDuel;
            public bool ThrowerCoverWindow;
            public float TargetDistance;
        }

        private struct AIModeClutchContext
        {
            public bool HasClutchSignal;
            public bool ProtectLead;
            public bool ComebackPressure;
            public bool ObjectiveSwing;
            public bool FinalPressure;
            public bool HasTarget;
            public GameModeId Mode;
            public int SelfObjectiveValue;
            public int TargetObjectiveValue;
        }

        private struct AISpacingContext
        {
            public bool HasClumpPressure;
            public bool HasTarget;
            public bool IntentionalCollapse;
            public bool SelfCarrier;
            public int NearbyAllyCount;
            public int SameTargetAllies;
            public float ClumpPressure;
            public float TargetHealthRatio;
        }

        private struct AIAbilityThreatContext
        {
            public bool HasThreatSignal;
            public bool HighThreat;
            public bool CanPunish;
            public bool HasCoverBetween;
            public bool MainAreaThreat;
            public bool SuperAreaThreat;
            public bool DirectFireThreat;
            public float TargetDistance;
            public float TargetRange;
            public float OwnRange;
            public float SuperThreatPressure;
            public float AreaThreatPressure;
            public float FiringWindowPressure;
            public float PendingDangerPressure;
            public float TotalThreatPressure;
        }

        private CloseRangeMatchupEvaluation EvaluateCloseRangeMatchup(
            AITargetInfo targetInfo,
            float distance,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (_profile == null ||
                targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null ||
                !IsCloseRangePressureRole())
            {
                return CloseRangeMatchupEvaluation.Inactive;
            }

            float ownRange = Mathf.Max(1f, GetAbilityMaxRange());
            float targetRange = Mathf.Max(1f, GetTargetAbilityMaxRange(targetBrawler));
            float rangeGap = targetRange - ownRange;
            float meaningfulGap = Mathf.Max(0.85f, ownRange * 0.30f);

            if (rangeGap <= meaningfulGap)
                return CloseRangeMatchupEvaluation.Inactive;

            float catchDistance = Mathf.Max(
                ownRange + _profile.AttackRangeBuffer,
                ownRange * Mathf.Max(1.25f, _profile.CloseRangeCatchDistanceMultiplier));

            float chaseThreshold = Mathf.Clamp(
                _profile.LowHealthChaseHealthThreshold > 0f
                    ? _profile.LowHealthChaseHealthThreshold
                    : _profile.FinisherHealthThreshold,
                0.05f,
                0.85f);
            float targetHealthRatio = targetBrawler.State.CurrentHealth /
                                      Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
            int targetCarriedGems = targetBrawler.State.CarriedGemCount;
            bool targetLow = targetHealthRatio <= chaseThreshold;
            bool objectiveTarget = targetCarriedGems > 0 ||
                                   IsModeCriticalTarget(targetBrawler, macroState);
            bool superReady = _self.State != null &&
                              _self.State.SuperCharge.IsReady;
            bool superCanCloseGap = CanUseSuperToCloseGap(distance);
            bool waitingForSuper =
                !superReady &&
                _self.State != null &&
                _self.State.SuperCharge != null &&
                _self.State.SuperCharge.ChargePercent >= 0.50f;
            bool teamCollapse = HasTeamCollapseOnTarget(targetBrawler, currentTick);
            bool catchable =
                distance <= catchDistance ||
                targetLow ||
                objectiveTarget ||
                superCanCloseGap ||
                teamCollapse;

            float overReach = Mathf.Max(0f, distance - catchDistance);

            if (catchable)
            {
                float engageBonus = Mathf.Max(0f, _profile.CloseRangeEvasivePressureBonus);

                if (targetLow)
                    engageBonus += _profile.ChaseCommitScoreBonus * 0.55f;

                if (objectiveTarget)
                    engageBonus += Mathf.Min(18f, 6f + targetCarriedGems * 4f);

                if (superCanCloseGap)
                    engageBonus += 8f;

                if (teamCollapse)
                    engageBonus += 8f;

                float repositionBonus =
                    Mathf.Max(0f, _profile.CloseRangeEvasivePressureBonus) *
                    (distance > ownRange + _profile.AttackRangeBuffer ? 0.75f : 0.35f);

                return new CloseRangeMatchupEvaluation(
                    true,
                    engageBonus,
                    repositionBonus,
                    $"engage rangeGap={rangeGap:0.0} dist={distance:0.0} catch={catchDistance:0.0} superGap={superCanCloseGap}");
            }

            float penalty =
                Mathf.Max(0f, _profile.CloseRangeOutrangedChasePenalty) +
                overReach * 5f +
                rangeGap * 2.5f;

            if (macroState.Call == AIGameModeMacroCall.Hold ||
                macroState.Call == AIGameModeMacroCall.Reset ||
                macroState.OwnTeamHasCountdown)
            {
                penalty += 10f;
            }

            if (_self.State != null)
            {
                float healthRatio = _self.State.CurrentHealth /
                                    Mathf.Max(1f, _self.State.MaxHealth.Value);
                if (healthRatio <= _profile.LowHealthRetreatRatio + 0.20f)
                    penalty += 12f;
            }

            if (waitingForSuper)
                penalty += 14f + _self.State.SuperCharge.ChargePercent * 10f;

            float coverBonus =
                Mathf.Max(0f, _profile.CloseRangeCoverRepositionBonus) +
                Mathf.Min(16f, overReach * 4f);

            if (waitingForSuper)
                coverBonus += Mathf.Max(4f, _profile.CloseRangeEvasivePressureBonus * 0.8f);

            return new CloseRangeMatchupEvaluation(
                true,
                -penalty,
                coverBonus,
                $"cover_outmatched rangeGap={rangeGap:0.0} dist={distance:0.0} catch={catchDistance:0.0} waitSuper={waitingForSuper}");
        }

        private bool CanUseSuperToCloseGap(float distance)
        {
            if (_self == null ||
                _self.State == null ||
                _self.State.SuperCharge == null ||
                !_self.State.SuperCharge.IsReady)
            {
                return false;
            }

            AbilityDefinition super = _self.State.GetCurrentSuperDefinition();
            if (super == null)
                super = _self.Definition != null ? _self.Definition.SuperAbility : null;

            if (super == null)
                return false;

            bool isGapCloser =
                super is LeapAbilityDefinition ||
                AIAbilityIntentUtility.IsEngage(super);

            if (!isGapCloser)
                return false;

            float maxRange =
                Mathf.Max(1f, super.GetAIMaxRange()) *
                Mathf.Max(1f, _profile.SuperMaxRangeMultiplier);

            return distance <= maxRange + Mathf.Max(0.1f, _profile.AttackRangeBuffer);
        }

        private bool IsCloseRangePressureRole()
        {
            if (IsTank || IsAssassin)
                return true;

            return IsFighter && GetAbilityMaxRange() <= 4.75f;
        }

        private static bool IsShortRangePressureTarget(
            BrawlerController target,
            float targetRange)
        {
            if (target == null)
                return false;

            BrawlerArchetype archetype = target.Definition != null
                ? target.Definition.Archetype
                : BrawlerArchetype.Fighter;

            return archetype == BrawlerArchetype.Tank ||
                   archetype == BrawlerArchetype.Assassin ||
                   (archetype == BrawlerArchetype.Fighter && targetRange <= 4.75f);
        }

        private bool IsCurrentMainAttackDirectFire()
        {
            AbilityDefinition attack = _self != null && _self.State != null
                ? _self.State.GetCurrentMainAttackDefinition()
                : _self?.Definition?.MainAttack;

            if (attack == null)
                return true;

            if (attack is ThrownHybridAoEAbilityDefinition ||
                attack is ThrownVolleyAoEAbilityDefinition ||
                attack is AoEAbilityDefinition ||
                attack is MeleeConeAbilityDefinition ||
                attack is LeapAbilityDefinition ||
                attack is MinefieldAbilityDefinition ||
                attack is EffectAbilityDefinition)
            {
                return false;
            }

            return attack.DeliveryType == AbilityDeliveryType.Projectile ||
                   attack is ProjectileAbilityDefinition ||
                   attack is BasicProjectileAttackDefinition ||
                   attack is BurstSequenceProjectileAbilityDefinition ||
                   attack is ChainProjectileAbilityDefinition ||
                   attack is HybridProjectileAbilityDefinition ||
                   attack is VolleyProjectileAbilityDefinition ||
                   attack is BasicSuperDefinition;
        }

        private static float GetTargetAbilityMaxRange(BrawlerController target)
        {
            if (target == null)
                return 6f;

            AbilityDefinition attack = target.State != null
                ? target.State.GetCurrentMainAttackDefinition()
                : target.Definition?.MainAttack;

            return attack != null ? attack.GetAIMaxRange() : 6f;
        }

        private bool IsModeCriticalTarget(
            BrawlerController target,
            AIGameModeMacroState macroState)
        {
            if (target == null)
                return false;

            if (macroState.Mode == GameModeId.BrawlBall)
            {
                BrawlBallMode mode = BrawlBallMode.Instance;
                return mode != null &&
                       SpatialEntityUtility.IsSameEntity(mode.BallCarrier, target);
            }

            return macroState.Mode == GameModeId.Knockout &&
                   macroState.Call == AIGameModeMacroCall.Push &&
                   macroState.Phase == AIGameModeObjectivePhase.FinalPressure;
        }

        private bool HasTeamCollapseOnTarget(BrawlerController target, uint currentTick)
        {
            if (_teamCoordinator == null || target == null)
                return false;

            return _teamCoordinator.TryGetFocusDirective(
                       currentTick,
                       out BrawlerController focusTarget,
                       out float urgency,
                       out _) &&
                   urgency >= 1.35f &&
                   SpatialEntityUtility.IsSameEntity(focusTarget, target);
        }

        private float GetChaseDisciplineDelta(
            AITargetInfo targetInfo,
            float distance,
            uint currentTick,
            AIGameModeMacroState macroState)
        {
            if (targetInfo == null ||
                !targetInfo.HasLiveTarget ||
                !(targetInfo.Target is BrawlerController targetBrawler) ||
                targetBrawler.State == null ||
                _profile == null)
            {
                _chaseDisengageMemory.Reset();
                _lastChaseDebug = "Chase=None Reason=no_target";
                return 0f;
            }

            float threshold = Mathf.Clamp(
                _profile.LowHealthChaseHealthThreshold > 0f
                    ? _profile.LowHealthChaseHealthThreshold
                    : _profile.FinisherHealthThreshold,
                0.05f,
                0.85f);
            float targetHealthRatio = targetBrawler.State.CurrentHealth /
                                      Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
            float maxChaseDistance = Mathf.Max(1f, _profile.LowHealthChaseMaxDistance);
            float laneWeight = Mathf.Max(0f, _profile.LaneDisciplineWeight);
            bool targetIsLow = targetHealthRatio <= threshold;
            int targetCarriedGems = targetBrawler.State.CarriedGemCount;
            int selfCarriedGems = _self.State != null
                ? _self.State.CarriedGemCount
                : 0;
            bool preserveLaneShape = ShouldPreserveLaneShape(
                macroState,
                targetCarriedGems,
                currentTick);
            bool badChaseMapPosition = IsBadChaseMapPosition(targetBrawler.Position);
            bool secureFinisher = AIChaseDisengageMemory.IsSecureFinisher(
                targetHealthRatio,
                threshold,
                distance,
                maxChaseDistance,
                selfCarriedGems,
                badChaseMapPosition);
            AIChaseDisengageDecision chaseDecision =
                _chaseDisengageMemory.Evaluate(
                    new AIChaseDisengageContext
                    {
                        TargetEntityId = targetBrawler.EntityID,
                        Tick = currentTick,
                        Distance = distance,
                        TargetHealthRatio = targetHealthRatio,
                        ChaseHealthThreshold = threshold,
                        MaxChaseDistance = maxChaseDistance,
                        SelfCarriedGems = selfCarriedGems,
                        TargetCarriedGems = targetCarriedGems,
                        PreserveLaneShape = preserveLaneShape,
                        TargetInBadMapPosition = badChaseMapPosition,
                        CommitTicks = _profile.LowHealthChaseCommitTicks,
                        MaxTicks = _profile.LowHealthChaseMaxTicks,
                        CooldownTicks = _profile.LowHealthChaseCooldownTicks,
                        BreakDistanceMultiplier =
                            _profile.LowHealthChaseBreakDistanceMultiplier,
                        CommitScoreBonus = _profile.ChaseCommitScoreBonus,
                        DisengageScorePenalty =
                            _profile.ChaseDisengageScorePenalty,
                        BadMapPenalty = _profile.BadMapChasePenalty
                    });
            _lastChaseDebug = chaseDecision.GetDebugSummary();

            if (targetIsLow)
            {
                float securePressure = Mathf.Clamp01((threshold - targetHealthRatio) / threshold);
                float delta = _profile.LowHealthChaseApproachBonus * (0.55f + securePressure);

                if (secureFinisher)
                    delta += _profile.ChaseCommitScoreBonus * 0.75f;

                if (targetCarriedGems > 0)
                    delta += targetCarriedGems * 4f;

                if (distance > maxChaseDistance && !secureFinisher)
                {
                    float overDistance = distance - maxChaseDistance;
                    delta -= overDistance * Mathf.Max(0f, _profile.UnsafeChasePenalty) * 0.35f;
                }

                if (preserveLaneShape &&
                    distance > maxChaseDistance * 0.70f &&
                    !secureFinisher)
                {
                    float valuableTargetDiscount = targetCarriedGems > 0 ? 0.55f : 1f;
                    delta -= _profile.UnsafeChasePenalty * valuableTargetDiscount * laneWeight;
                }

                if (badChaseMapPosition &&
                    preserveLaneShape &&
                    targetCarriedGems <= 0 &&
                    !secureFinisher)
                {
                    delta -= _profile.BadMapChasePenalty;
                }

                delta += chaseDecision.ScoreDelta;
                if (chaseDecision.ShouldDisengage)
                {
                    return Mathf.Min(
                        delta,
                        -_profile.ChaseDisengageScorePenalty * 0.55f);
                }

                return Mathf.Clamp(
                    delta,
                    -Mathf.Max(
                        _profile.UnsafeChasePenalty,
                        _profile.ChaseDisengageScorePenalty) * 1.5f,
                    _profile.LowHealthChaseApproachBonus * 1.8f +
                    _profile.ChaseCommitScoreBonus +
                    targetCarriedGems * 4f);
            }

            if (targetCarriedGems > 0)
            {
                float pickupValuePressure = targetCarriedGems * 5f;
                float distancePressure = Mathf.Clamp01(
                    1f - distance / Mathf.Max(1f, maxChaseDistance * 1.25f));
                float delta = pickupValuePressure * (0.65f + distancePressure * 0.55f);

                if (badChaseMapPosition)
                    delta -= _profile.BadMapChasePenalty * 0.35f;

                delta += chaseDecision.ScoreDelta;

                return Mathf.Clamp(
                    delta,
                    -_profile.ChaseDisengageScorePenalty,
                    _profile.LowHealthChaseApproachBonus +
                    _profile.ChaseCommitScoreBonus +
                    targetCarriedGems * 6f);
            }

            if (_profile.UseLaneDiscipline &&
                preserveLaneShape &&
                distance > maxChaseDistance)
            {
                float overDistance = distance - maxChaseDistance;
                float lanePenalty = -Mathf.Min(
                    _profile.UnsafeChasePenalty * laneWeight,
                    overDistance * 6f * laneWeight);

                return lanePenalty + Mathf.Min(0f, chaseDecision.ScoreDelta);
            }

            return Mathf.Min(0f, chaseDecision.ScoreDelta);
        }

        private bool ShouldPreserveLaneShape(
            AIGameModeMacroState macroState,
            int targetCarriedGems,
            uint currentTick)
        {
            if (_self.State == null)
                return false;

            float healthRatio = _self.State.CurrentHealth /
                                Mathf.Max(1f, _self.State.MaxHealth.Value);

            if (healthRatio <= _profile.LowHealthRetreatRatio + 0.15f)
                return true;

            if (_self.State.CarriedGemCount > 0)
                return true;

            if (_teamCoordinator != null &&
                _teamCoordinator.TryGetLaneOwnership(
                    currentTick,
                    out AITeamLaneOwnershipSnapshot laneOwnership) &&
                (laneOwnership.ShouldRotate ||
                 laneOwnership.AssignedLaneAbandoned ||
                 laneOwnership.CurrentLaneOverOwned))
            {
                return targetCarriedGems <= 0;
            }

            if (macroState.Call == AIGameModeMacroCall.Reset ||
                macroState.Call == AIGameModeMacroCall.Hold ||
                macroState.OwnTeamHasCountdown)
            {
                return targetCarriedGems <= 0;
            }

            return IsBacklineRole() && targetCarriedGems <= 0;
        }

        private bool IsBadChaseMapPosition(Vector3 targetPosition)
        {
            AStarSolver pathfinder = SimulationClock.Pathfinder;
            if (pathfinder == null)
                return false;

            Vector2Int targetCoords = pathfinder.GetGridCoords(targetPosition);
            if (!pathfinder.IsWalkable(targetCoords))
                return true;

            AIMapSemanticCell semantic = pathfinder.GetSemanticCell(targetCoords);
            if (semantic.HasTag(AIMapSemanticTag.DangerCorridor))
                return true;

            if (semantic.HasTag(AIMapSemanticTag.Choke) &&
                !IsTank &&
                !IsAssassin)
            {
                return true;
            }

            int walkableNeighbors = pathfinder.CountWalkableNeighbors(targetCoords);
            return walkableNeighbors <= 2 && !IsTank;
        }

        private readonly struct CloseRangeMatchupEvaluation
        {
            public static readonly CloseRangeMatchupEvaluation Inactive =
                new CloseRangeMatchupEvaluation(false, 0f, 0f, "inactive");

            public readonly bool IsActive;
            public readonly float ApproachDelta;
            public readonly float RepositionBonus;
            public readonly string Reason;

            public CloseRangeMatchupEvaluation(
                bool isActive,
                float approachDelta,
                float repositionBonus,
                string reason)
            {
                IsActive = isActive;
                ApproachDelta = approachDelta;
                RepositionBonus = repositionBonus;
                Reason = reason;
            }
        }

        private float GetObjectiveCrowdingPenalty()
        {
            if (IsTank)
                return 5f;

            if (IsFighter)
                return 7f;

            if (IsController)
                return 8f;

            if (IsSupport)
                return 10f;

            if (IsSniper)
                return 12f;

            if (IsArtillery)
                return 12f;

            if (IsAssassin)
                return 14f;

            return 10f;
        }
    }
}
