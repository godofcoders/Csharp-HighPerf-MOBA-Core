using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AITeamCoordinator
    {
        private readonly BrawlerController _self;
        private readonly List<ISpatialEntity> _localFightBuffer = new List<ISpatialEntity>(16);

        private const uint FocusMemoryTicks = 90;
        private const uint RegroupMemoryTicks = 30;
        private const uint PeelMemoryTicks = 60;
        private const uint EnemyHotspotMemoryTicks = 120;
        private const uint ThreatCenterMemoryTicks = 75;
        private const uint ActionIntentMemoryTicks = 12;
        private const uint CarrierMemoryTicks = 90;
        private const uint PlaybookMemoryTicks = 20;
        private const uint LaneOwnershipMemoryTicks = 45;
        private const float LocalFightRadius = 6.5f;

        private AITeamPlaybookState _lastPlaybookState;
        private AITeamLaneOwnershipSnapshot _lastLaneOwnership;
        private string _lastPlaybookDebug = "Playbook=None";
        private string _lastLaneOwnershipDebug = "LaneOwn=None";

        public string LastPlaybookDebug => _lastPlaybookDebug;
        public string LastLaneOwnershipDebug => _lastLaneOwnershipDebug;

        public AITeamCoordinator(BrawlerController self)
        {
            _self = self;
            _lastPlaybookState = AITeamPlaybookState.None(0u);
            _lastLaneOwnership = AITeamLaneOwnershipSnapshot.None(0, 0u);
        }

        public void UpdateTeamSignals(AITargetInfo targetInfo, uint currentTick)
        {
            if (_self == null || _self.State == null || _self.State.IsDead)
                return;

            if (targetInfo.HasLiveTarget && targetInfo.Target is BrawlerController targetBrawler)
            {
                AIGameModeMacroState macroState =
                    AIGameModeMacroStrategy.ResolveCurrentMode(_self.Team);
                float focusUrgency = BuildFocusUrgency(
                    targetBrawler,
                    macroState,
                    out string focusReason);

                AITeamBlackboard.ReportFocusTarget(
                    _self.Team,
                    targetBrawler,
                    currentTick,
                    focusUrgency,
                    focusReason);
                AITeamBlackboard.ReportEnemyHotspot(
                    _self.Team,
                    targetBrawler.Position,
                    currentTick,
                    Mathf.Clamp(1.10f + focusUrgency * 0.25f, 1.25f, 3.50f));
            }

            if (_self.State.ThreatTracker != null)
            {
                int highestThreatId = _self.State.ThreatTracker.GetHighestThreatTarget(currentTick, 240);
                if (highestThreatId != 0)
                {
                    float threat = _self.State.ThreatTracker.GetThreat(highestThreatId, currentTick, 240);
                    float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
                    float urgency = BuildPeelUrgency(threat, selfHealthRatio);

                    AITeamBlackboard.ReportAllyUnderThreat(_self.Team, _self, currentTick, urgency);

                    ISpatialEntity threatEntity = CombatRegistry.GetEntity(highestThreatId);
                    if (threatEntity != null && threatEntity.Team != _self.Team)
                    {
                        AITeamBlackboard.ReportThreatCenter(
                            _self.Team,
                            threatEntity.Position,
                            currentTick,
                            Mathf.Clamp(threat / 350f, 1f, 4f));
                    }
                }
            }

            float healthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
            if (healthRatio <= 0.30f)
            {
                AITeamBlackboard.ReportRegroupPoint(
                    _self.Team,
                    _self.Position,
                    currentTick,
                    BuildRegroupUrgency(healthRatio));
            }

            if (_self.State.CarriedGemCount > 0)
            {
                AITeamBlackboard.ReportCarrier(
                    _self.Team,
                    _self,
                    _self.State.CarriedGemCount,
                    currentTick);
            }
        }

        public AITeamPlaybookState UpdatePlaybook(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState,
            uint currentTick)
        {
            if (_self == null || _self.State == null || _self.State.IsDead)
            {
                _lastPlaybookState = AITeamPlaybookState.None(currentTick);
                _lastLaneOwnership = AITeamLaneOwnershipSnapshot.None(
                    _self != null ? _self.EntityID : 0,
                    currentTick);
                _lastLaneOwnershipDebug = _lastLaneOwnership.GetDebugSummary();
                _lastPlaybookDebug =
                    $"{_lastPlaybookState.GetDebugSummary()} {_lastLaneOwnershipDebug}";

                if (_self != null)
                    AITeamBlackboard.ClearLaneOwnership(_self.Team, _self.EntityID);

                return _lastPlaybookState;
            }

            AITeamPlaybookContext context = BuildPlaybookContext(
                targetInfo,
                macroState,
                currentTick);

            _lastPlaybookState = AITeamPlaybookDirector.Resolve(context);
            RecordPlaybookGauntletSignal(_lastPlaybookState.Call, currentTick);

            AITeamBlackboard.ReportPlaybookState(
                _self.Team,
                _lastPlaybookState,
                currentTick);

            AITeamBlackboard.ReportLaneOwnership(
                _self.Team,
                _self.EntityID,
                ResolveLaneReport(_lastPlaybookState, context),
                _self.Position,
                currentTick);

            AITeamBlackboard.TryGetLaneOwnership(
                _self.Team,
                _self.EntityID,
                currentTick,
                LaneOwnershipMemoryTicks,
                out _lastLaneOwnership);

            _lastLaneOwnershipDebug = _lastLaneOwnership.GetDebugSummary();
            _lastPlaybookDebug =
                $"{_lastPlaybookState.GetDebugSummary()} {_lastLaneOwnershipDebug}";

            return _lastPlaybookState;
        }

        private static void RecordPlaybookGauntletSignal(
            AITeamPlaybookCall call,
            uint currentTick)
        {
            switch (call)
            {
                case AITeamPlaybookCall.Engage:
                    AIValidationGauntlet.RecordSignal(
                        AIValidationGauntletSignal.TeamEngageCall,
                        currentTick);
                    break;

                case AITeamPlaybookCall.Disengage:
                    AIValidationGauntlet.RecordSignal(
                        AIValidationGauntletSignal.TeamDisengageCall,
                        currentTick);
                    break;
            }
        }

        public bool TryGetFocusTarget(uint currentTick, out BrawlerController target)
        {
            return AITeamBlackboard.TryGetFocusTarget(_self.Team, currentTick, FocusMemoryTicks, out target);
        }

        public bool TryGetFocusDirective(
            uint currentTick,
            out BrawlerController target,
            out float urgency,
            out string reason)
        {
            return AITeamBlackboard.TryGetFocusDirective(
                _self.Team,
                currentTick,
                FocusMemoryTicks,
                out target,
                out urgency,
                out reason);
        }

        public bool TryGetRegroupPoint(uint currentTick, out Vector3 point)
        {
            return AITeamBlackboard.TryGetRegroupPoint(_self.Team, currentTick, RegroupMemoryTicks, out point);
        }

        public bool TryGetAllyUnderThreat(uint currentTick, out BrawlerController ally)
        {
            return AITeamBlackboard.TryGetAllyUnderThreat(_self.Team, currentTick, PeelMemoryTicks, out ally);
        }

        public bool TryGetEnemyHotspot(uint currentTick, out Vector3 position, out float pressure)
        {
            return AITeamBlackboard.TryGetEnemyHotspot(
                _self.Team,
                currentTick,
                EnemyHotspotMemoryTicks,
                out position,
                out pressure);
        }

        public bool TryGetThreatCenter(uint currentTick, out Vector3 position, out float pressure)
        {
            return AITeamBlackboard.TryGetThreatCenter(
                _self.Team,
                currentTick,
                ThreatCenterMemoryTicks,
                out position,
                out pressure);
        }

        public bool TryGetCarrier(
            uint currentTick,
            out BrawlerController carrier,
            out int carriedGemCount)
        {
            return AITeamBlackboard.TryGetCarrier(
                _self.Team,
                currentTick,
                CarrierMemoryTicks,
                out carrier,
                out carriedGemCount);
        }

        public bool TryGetPlaybookState(
            uint currentTick,
            out AITeamPlaybookState state)
        {
            if (_lastPlaybookState.IsActive &&
                currentTick - _lastPlaybookState.Tick <= PlaybookMemoryTicks)
            {
                state = _lastPlaybookState;
                return true;
            }

            return AITeamBlackboard.TryGetPlaybookState(
                _self.Team,
                currentTick,
                PlaybookMemoryTicks,
                out state);
        }

        public bool TryGetLaneOwnership(
            uint currentTick,
            out AITeamLaneOwnershipSnapshot snapshot)
        {
            if (_lastLaneOwnership.HasValue &&
                currentTick - _lastLaneOwnership.Tick <= LaneOwnershipMemoryTicks)
            {
                snapshot = _lastLaneOwnership;
                return true;
            }

            return AITeamBlackboard.TryGetLaneOwnership(
                _self.Team,
                _self.EntityID,
                currentTick,
                LaneOwnershipMemoryTicks,
                out snapshot);
        }

        public void ReportTargetFocusCount(int targetEntityId)
        {
            if (_self == null)
                return;

            AITeamBlackboard.ReportTargetFocusCount(
                _self.Team,
                _self.EntityID,
                targetEntityId);
        }

        public void ClearTargetFocusCount()
        {
            if (_self == null)
                return;

            AITeamBlackboard.ClearTargetFocusCount(
                _self.Team,
                _self.EntityID);
        }

        public int GetTargetFocusCount(int targetEntityId)
        {
            if (_self == null)
                return 0;

            return AITeamBlackboard.GetTargetFocusCount(
                _self.Team,
                targetEntityId);
        }

        public int GetTargetFocusCountExcludingSelf(int targetEntityId)
        {
            if (_self == null)
                return 0;

            return AITeamBlackboard.GetTargetFocusCountExcluding(
                _self.Team,
                targetEntityId,
                _self.EntityID);
        }

        public void ReportActionIntent(AIActionType actionType, uint currentTick)
        {
            if (_self == null)
                return;

            AITeamBlackboard.ReportActionIntent(
                _self.Team,
                _self.EntityID,
                actionType,
                currentTick);
        }

        public void ClearActionIntent()
        {
            if (_self == null)
                return;

            AITeamBlackboard.ClearActionIntent(
                _self.Team,
                _self.EntityID);
        }

        public void ClearLaneOwnership()
        {
            if (_self == null)
                return;

            AITeamBlackboard.ClearLaneOwnership(
                _self.Team,
                _self.EntityID);
        }

        public int GetActionIntentCount(AIActionType actionType, uint currentTick)
        {
            if (_self == null)
                return 0;

            return AITeamBlackboard.GetActionIntentCount(
                _self.Team,
                actionType,
                currentTick,
                ActionIntentMemoryTicks);
        }

        public int GetActionIntentCountExcludingSelf(AIActionType actionType, uint currentTick)
        {
            if (_self == null)
                return 0;

            return AITeamBlackboard.GetActionIntentCountExcluding(
                _self.Team,
                actionType,
                _self.EntityID,
                currentTick,
                ActionIntentMemoryTicks);
        }

        private float BuildPeelUrgency(float threat, float selfHealthRatio)
        {
            float healthUrgency = 1f - Mathf.Clamp01(selfHealthRatio);
            float gemUrgency = _self.State != null ? _self.State.CarriedGemCount * 0.35f : 0f;

            return 1f +
                   Mathf.Clamp(threat / 500f, 0f, 3f) +
                   healthUrgency * 2f +
                   gemUrgency;
        }

        private AITeamPlaybookContext BuildPlaybookContext(
            AITargetInfo targetInfo,
            AIGameModeMacroState macroState,
            uint currentTick)
        {
            float healthRatio = _self.State.CurrentHealth /
                                Mathf.Max(1f, _self.State.MaxHealth.Value);

            AITeamPlaybookContext context = new AITeamPlaybookContext
            {
                BotEntityId = _self.EntityID,
                Tick = currentTick,
                SelfPosition = _self.Position,
                Archetype = _self.Definition != null
                    ? _self.Definition.Archetype
                    : BrawlerArchetype.Fighter,
                HealthRatio = healthRatio,
                MacroState = macroState,
                SelfIsCarrier = _self.State.CarriedGemCount > 0,
                SelfCarriedGems = _self.State.CarriedGemCount,
                ApproachAllies = GetActionIntentCountExcludingSelf(AIActionType.Approach, currentTick),
                HoldAllies = GetActionIntentCountExcludingSelf(AIActionType.HoldRange, currentTick),
                RepositionAllies = GetActionIntentCountExcludingSelf(AIActionType.Reposition, currentTick),
                PeelAllies = GetActionIntentCountExcludingSelf(AIActionType.Peel, currentTick),
                RegroupAllies = GetActionIntentCountExcludingSelf(AIActionType.Regroup, currentTick),
                ObjectiveAllies = GetActionIntentCountExcludingSelf(AIActionType.Objective, currentTick)
            };

            PopulateCarrierContext(ref context, currentTick);
            PopulateThreatenedAllyContext(ref context, currentTick);
            PopulateFocusContext(ref context, targetInfo, currentTick);
            PopulatePositionSignalContext(ref context, currentTick);
            PopulateLocalFightContext(ref context);
            PopulateLaneOwnershipContext(ref context, currentTick);

            return context;
        }

        private void PopulateLaneOwnershipContext(
            ref AITeamPlaybookContext context,
            uint currentTick)
        {
            AITeamBlackboard.TryGetLaneOwnership(
                _self.Team,
                _self.EntityID,
                currentTick,
                LaneOwnershipMemoryTicks,
                out AITeamLaneOwnershipSnapshot laneOwnership);

            context.LaneOwnership = laneOwnership;
            context.HasLaneOwnership = laneOwnership.HasValue;
        }

        private AITeamLaneAssignment ResolveLaneReport(
            AITeamPlaybookState state,
            AITeamPlaybookContext context)
        {
            if (state.Lane != AITeamLaneAssignment.None)
                return state.Lane;

            if (context.HasLaneOwnership &&
                context.LaneOwnership.HasRecommendedLane)
            {
                return context.LaneOwnership.RecommendedLane;
            }

            return AILaneDisciplineUtility.ResolveAssignedLane(_self.EntityID);
        }

        private void PopulateCarrierContext(
            ref AITeamPlaybookContext context,
            uint currentTick)
        {
            if (!TryGetCarrier(currentTick, out BrawlerController carrier, out int carriedGemCount) ||
                !SpatialEntityUtility.IsAlive(carrier))
            {
                return;
            }

            if (carrier.EntityID == _self.EntityID)
            {
                context.SelfIsCarrier = carriedGemCount > 0;
                context.SelfCarriedGems = carriedGemCount;
                return;
            }

            context.HasAllyCarrier = true;
            context.AllyCarrierEntityId = carrier.EntityID;
            context.AllyCarrierGemCount = carriedGemCount;
            context.AllyCarrierPosition = carrier.Position;
        }

        private void PopulateThreatenedAllyContext(
            ref AITeamPlaybookContext context,
            uint currentTick)
        {
            if (!TryGetAllyUnderThreat(currentTick, out BrawlerController ally) ||
                !SpatialEntityUtility.IsAlive(ally))
            {
                return;
            }

            context.HasAllyUnderThreat = true;
            context.ThreatenedAllyEntityId = ally.EntityID;
            context.ThreatenedAllyPosition = ally.Position;
            context.SelfIsThreatenedAlly = ally.EntityID == _self.EntityID;
        }

        private void PopulateFocusContext(
            ref AITeamPlaybookContext context,
            AITargetInfo targetInfo,
            uint currentTick)
        {
            if (TryGetFocusDirective(
                    currentTick,
                    out BrawlerController directiveTarget,
                    out float directiveUrgency,
                    out string directiveReason) &&
                SpatialEntityUtility.IsAlive(directiveTarget))
            {
                context.HasFocusTarget = true;
                context.FocusTargetEntityId = directiveTarget.EntityID;
                context.FocusTargetPosition = directiveTarget.Position;
                context.FocusUrgency = directiveUrgency;
                context.FocusReason = directiveReason;
                return;
            }

            if (targetInfo != null &&
                targetInfo.HasLiveTarget &&
                SpatialEntityUtility.TryGetEntityId(targetInfo.Target, out int targetId))
            {
                context.HasFocusTarget = true;
                context.FocusTargetEntityId = targetId;
                context.FocusTargetPosition = targetInfo.Target.Position;
                context.FocusUrgency = 1f;
                context.FocusReason = "local_target";
            }
        }

        private float BuildFocusUrgency(
            BrawlerController target,
            AIGameModeMacroState macroState,
            out string reason)
        {
            reason = "sighted";

            if (!SpatialEntityUtility.IsAlive(target) || target.State == null)
                return 0.5f;

            float targetHealthRatio = target.State.CurrentHealth /
                                      Mathf.Max(1f, target.State.MaxHealth.Value);
            int targetCarriedGems = target.State.CarriedGemCount;
            float distance = Vector3.Distance(_self.Position, target.Position);
            int alliedFocus = GetTargetFocusCountExcludingSelf(target.EntityID);

            AIWinConditionTargetEvaluation evaluation =
                AIWinConditionUtility.EvaluateTarget(
                    new AIWinConditionTargetContext(
                        macroState,
                        _self.State != null ? _self.State.CarriedGemCount : 0,
                        targetCarriedGems,
                        targetHealthRatio,
                        distance,
                        isCurrentTarget: true,
                        isTeamFocusTarget: false,
                        alliedFocus));

            float urgency = 1f;
            if (evaluation.HasDelta)
            {
                urgency += evaluation.ScoreDelta / 30f;
                reason = evaluation.Reason;
            }

            if (evaluation.ShouldCollapse)
            {
                urgency += 1.10f;
                reason = string.IsNullOrEmpty(reason)
                    ? "collapse"
                    : $"collapse:{reason}";
            }

            if (targetHealthRatio <= 0.25f)
            {
                urgency += 0.70f;
                if (reason == "sighted")
                    reason = "low_health";
            }

            if (targetCarriedGems > 0)
                urgency += Mathf.Min(1.25f, targetCarriedGems * 0.18f);

            if (alliedFocus > 0 && evaluation.ShouldCollapse)
                urgency += Mathf.Min(0.80f, alliedFocus * 0.25f);

            return Mathf.Clamp(urgency, 0.5f, 6f);
        }

        private void PopulatePositionSignalContext(
            ref AITeamPlaybookContext context,
            uint currentTick)
        {
            if (TryGetEnemyHotspot(currentTick, out Vector3 enemyHotspot, out float enemyPressure))
            {
                context.HasEnemyHotspot = true;
                context.EnemyHotspotPosition = enemyHotspot;
                context.EnemyHotspotPressure = enemyPressure;
            }

            if (TryGetThreatCenter(currentTick, out Vector3 threatCenter, out float threatPressure))
            {
                context.HasThreatCenter = true;
                context.ThreatCenterPosition = threatCenter;
                context.ThreatCenterPressure = threatPressure;
            }
        }

        private void PopulateLocalFightContext(ref AITeamPlaybookContext context)
        {
            if (SimulationClock.Grid == null)
                return;

            _localFightBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(
                _self.Position,
                LocalFightRadius,
                _localFightBuffer);

            for (int i = 0; i < _localFightBuffer.Count; i++)
            {
                ISpatialEntity entity = _localFightBuffer[i];
                if (!SpatialEntityUtility.IsAlive(entity) ||
                    entity.EntityID == _self.EntityID ||
                    entity is not BrawlerController brawler ||
                    brawler.State == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(_self.Position, brawler.Position);
                float distancePressure = 1f - Mathf.Clamp01(distance / LocalFightRadius);
                float healthRatio = brawler.State.CurrentHealth /
                                    Mathf.Max(1f, brawler.State.MaxHealth.Value);
                float pressure = 0.35f + distancePressure * 0.45f + healthRatio * 0.35f;

                if (brawler.Team == _self.Team)
                {
                    context.NearbyAllies++;
                    context.LocalAllyPressure += pressure;
                }
                else
                {
                    context.NearbyEnemies++;
                    context.LocalEnemyPressure += pressure;
                }
            }
        }

        private float BuildRegroupUrgency(float selfHealthRatio)
        {
            float healthUrgency = 1f - Mathf.Clamp01(selfHealthRatio);
            float gemUrgency = _self.State != null ? _self.State.CarriedGemCount * 0.25f : 0f;

            return 1f + healthUrgency * 2f + gemUrgency;
        }

    }
}
