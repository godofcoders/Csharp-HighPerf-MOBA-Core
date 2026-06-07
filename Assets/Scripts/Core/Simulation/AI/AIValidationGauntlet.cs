using System;

namespace MOBA.Core.Simulation.AI
{
    public enum AIValidationGauntletScenarioType
    {
        None,
        RetreatSafety,
        PeelResponse,
        ObjectivePlay,
        AbilityUsage,
        StuckRecovery,
        TeamCoordination,
        MovementLiveness,
        LaneDiscipline,
        GemPickup,
        CombatDiscipline,
        PlaytestRegression
    }

    public enum AIValidationGauntletStatus
    {
        NotStarted,
        Running,
        Passed,
        Watch,
        Failed
    }

    public enum AIValidationGauntletSignal
    {
        AbilityCast,
        MainAttackCast,
        GadgetCast,
        SuperCast,
        FailureRecovery,
        MovementStall,
        RouteBlocked,
        GemPickupIntent,
        ObjectiveIntent,
        ObjectiveNeglect,
        TacticalStop
    }

    [Flags]
    public enum AIValidationGauntletActionSet
    {
        None = 0,
        Wander = 1 << 0,
        Search = 1 << 1,
        Approach = 1 << 2,
        HoldRange = 1 << 3,
        Reposition = 1 << 4,
        Retreat = 1 << 5,
        Evade = 1 << 6,
        UseSuper = 1 << 7,
        Regroup = 1 << 8,
        Peel = 1 << 9,
        Objective = 1 << 10
    }

    public struct AIValidationGauntletSpec
    {
        public AIValidationGauntletScenarioType ScenarioType;
        public int MinimumFrameCount;
        public int MinimumBotDecisionCount;
        public float MinimumTargetedRatio;
        public float MaximumTargetedRatio;
        public AIValidationGauntletActionSet ExpectedActions;
        public float MinimumExpectedActionRatio;
        public float MaximumInvalidDecisionRatio;
        public float MaximumZeroScoreRatio;
        public float MaximumSwitchRatio;
        public float MaximumLowConfidenceRatio;
        public float MinimumEmergencyActionRatio;
        public float MinimumTeamRoleAdjustedRatio;
        public int MinimumAbilitySignalCount;
        public int MinimumFailureRecoverySignalCount;
        public int MinimumMovementSignalCount;
        public int MinimumGemIntentSignalCount;
        public int MinimumObjectiveIntentSignalCount;
        public int MaximumTacticalStopSignalCount;
        public int MaximumObjectiveNeglectSignalCount;
        public int MinimumUniqueActionCount;
    }

    public struct AIValidationGauntletResult
    {
        public AIValidationGauntletScenarioType ScenarioType;
        public AIValidationGauntletStatus Status;
        public string Reason;
        public int FrameCount;
        public int BotDecisionCount;
        public int ExpectedActionCount;
        public int AbilitySignalCount;
        public int FailureRecoverySignalCount;
        public int MovementSignalCount;
        public int GemIntentSignalCount;
        public int ObjectiveIntentSignalCount;
        public int TacticalStopSignalCount;
        public int ObjectiveNeglectSignalCount;
        public int UniqueActionCount;
        public float TargetedRatio;
        public float ExpectedActionRatio;
        public float InvalidDecisionRatio;
        public float ZeroScoreRatio;
        public float SwitchRatio;
        public float LowConfidenceRatio;
        public float EmergencyActionRatio;
        public float TeamRoleAdjustedRatio;
    }

    public static class AIValidationGauntlet
    {
        private const int ActionSlotCount = (int)AIActionType.Objective + 1;
        private const int SignalSlotCount = (int)AIValidationGauntletSignal.TacticalStop + 1;

        private static readonly int[] _actionTotals = new int[ActionSlotCount];
        private static readonly int[] _signalTotals = new int[SignalSlotCount];

        private static AIValidationGauntletSpec _activeSpec;
        private static AIValidationGauntletResult _lastResult;
        private static bool _isRunning;

        private static int _frameCount;
        private static int _botDecisionCount;
        private static int _targetedBotCount;
        private static int _invalidDecisionCount;
        private static int _zeroScoreDecisionCount;
        private static int _actionSwitchCount;
        private static int _lowConfidenceDecisionCount;
        private static int _emergencyActionCount;
        private static int _teamRoleAdjustedDecisionCount;

        public static bool IsRunning => _isRunning;
        public static AIValidationGauntletScenarioType ActiveScenario =>
            _isRunning ? _activeSpec.ScenarioType : AIValidationGauntletScenarioType.None;
        public static AIValidationGauntletResult LastResult => _lastResult;

        public static void BeginScenario(
            AIValidationGauntletScenarioType scenarioType,
            uint currentTick)
        {
            BeginScenario(CreateDefaultSpec(scenarioType), currentTick);
        }

        public static void BeginScenario(
            AIValidationGauntletSpec spec,
            uint currentTick)
        {
            ClearRunningState();

            if (spec.ScenarioType == AIValidationGauntletScenarioType.None)
            {
                _lastResult = BuildResult(
                    spec,
                    AIValidationGauntletStatus.Failed,
                    "none_scenario");
                return;
            }

            _activeSpec = NormalizeSpec(spec);
            _isRunning = true;
            _lastResult = BuildResult(
                _activeSpec,
                AIValidationGauntletStatus.Running,
                "running");
        }

        public static AIValidationGauntletResult EndScenario(uint currentTick)
        {
            if (!_isRunning)
                return _lastResult;

            AIValidationGauntletResult result = EvaluateActiveScenario();
            _lastResult = result;
            _isRunning = false;
            return result;
        }

        public static void RecordFrame(AIValidationFrame frame, int[] actionCounts)
        {
            if (!_isRunning || frame.ActiveBotCount <= 0)
                return;

            _frameCount++;
            _botDecisionCount += frame.ActiveBotCount;
            _targetedBotCount += frame.TargetedBotCount;
            _invalidDecisionCount += frame.InvalidDecisionCount;
            _zeroScoreDecisionCount += frame.ZeroScoreDecisionCount;
            _actionSwitchCount += frame.ActionSwitchCount;
            _lowConfidenceDecisionCount += frame.LowConfidenceDecisionCount;
            _emergencyActionCount += frame.EmergencyActionCount;
            _teamRoleAdjustedDecisionCount += frame.TeamRoleAdjustedDecisionCount;

            for (int action = 1; action < ActionSlotCount; action++)
            {
                int count = actionCounts != null && action < actionCounts.Length
                    ? actionCounts[action]
                    : 0;

                if (count > 0)
                    _actionTotals[action] += count;
            }
        }

        public static void RecordSignal(
            AIValidationGauntletSignal signal,
            uint currentTick,
            int amount = 1)
        {
            if (!_isRunning || !IsTrackedSignal(signal))
                return;

            int safeAmount = amount < 0 ? 0 : amount;
            if (safeAmount == 0)
                return;

            _signalTotals[(int)signal] += safeAmount;

            if (signal == AIValidationGauntletSignal.MainAttackCast ||
                signal == AIValidationGauntletSignal.GadgetCast ||
                signal == AIValidationGauntletSignal.SuperCast)
            {
                _signalTotals[(int)AIValidationGauntletSignal.AbilityCast] += safeAmount;
            }
        }

        public static int GetActionCount(AIActionType actionType)
        {
            return IsTrackedAction(actionType)
                ? _actionTotals[(int)actionType]
                : 0;
        }

        public static int GetSignalCount(AIValidationGauntletSignal signal)
        {
            return IsTrackedSignal(signal)
                ? _signalTotals[(int)signal]
                : 0;
        }

        public static string GetDebugSummary()
        {
            AIValidationGauntletResult result = _isRunning
                ? BuildResult(_activeSpec, AIValidationGauntletStatus.Running, "running")
                : _lastResult;

            if (result.Status == AIValidationGauntletStatus.NotStarted)
                return "Gauntlet=NO_DATA status=NOT_STARTED";

            return
                $"Gauntlet={result.ScenarioType} " +
                $"status={result.Status} " +
                $"frames={result.FrameCount} " +
                $"bots={result.BotDecisionCount} " +
                $"expected={result.ExpectedActionRatio:0%} " +
                $"ability={result.AbilitySignalCount} " +
                $"recovery={result.FailureRecoverySignalCount} " +
                $"gem={result.GemIntentSignalCount} " +
                $"obj={result.ObjectiveIntentSignalCount} " +
                $"stop={result.TacticalStopSignalCount} " +
                $"reason={result.Reason}";
        }

        public static void ResetForTests()
        {
            ClearRunningState();
            _lastResult = new AIValidationGauntletResult
            {
                ScenarioType = AIValidationGauntletScenarioType.None,
                Status = AIValidationGauntletStatus.NotStarted,
                Reason = "not_started"
            };
        }

        public static AIValidationGauntletSpec CreateDefaultSpec(
            AIValidationGauntletScenarioType scenarioType)
        {
            AIValidationGauntletSpec spec = new AIValidationGauntletSpec
            {
                ScenarioType = scenarioType,
                MinimumFrameCount = 12,
                MinimumBotDecisionCount = 12,
                MinimumTargetedRatio = 0f,
                MaximumTargetedRatio = 1f,
                ExpectedActions = AIValidationGauntletActionSet.None,
                MinimumExpectedActionRatio = 0f,
                MaximumInvalidDecisionRatio = 0f,
                MaximumZeroScoreRatio = 0.10f,
                MaximumSwitchRatio = 0.75f,
                MaximumLowConfidenceRatio = 0.75f,
                MinimumEmergencyActionRatio = 0f,
                MinimumTeamRoleAdjustedRatio = 0f,
                MinimumAbilitySignalCount = 0,
                MinimumFailureRecoverySignalCount = 0,
                MinimumMovementSignalCount = 0,
                MinimumGemIntentSignalCount = 0,
                MinimumObjectiveIntentSignalCount = 0,
                MaximumTacticalStopSignalCount = int.MaxValue,
                MaximumObjectiveNeglectSignalCount = int.MaxValue,
                MinimumUniqueActionCount = 1
            };

            switch (scenarioType)
            {
                case AIValidationGauntletScenarioType.RetreatSafety:
                    spec.ExpectedActions =
                        AIValidationGauntletActionSet.Retreat |
                        AIValidationGauntletActionSet.Evade;
                    spec.MinimumExpectedActionRatio = 0.30f;
                    spec.MinimumEmergencyActionRatio = 0.20f;
                    spec.MinimumTargetedRatio = 0.50f;
                    break;

                case AIValidationGauntletScenarioType.PeelResponse:
                    spec.ExpectedActions = AIValidationGauntletActionSet.Peel;
                    spec.MinimumExpectedActionRatio = 0.15f;
                    spec.MinimumTargetedRatio = 0.35f;
                    break;

                case AIValidationGauntletScenarioType.ObjectivePlay:
                    spec.ExpectedActions =
                        AIValidationGauntletActionSet.Objective |
                        AIValidationGauntletActionSet.Search;
                    spec.MinimumExpectedActionRatio = 0.40f;
                    spec.MaximumTargetedRatio = 0.55f;
                    break;

                case AIValidationGauntletScenarioType.AbilityUsage:
                    spec.ExpectedActions = AIValidationGauntletActionSet.UseSuper;
                    spec.MinimumExpectedActionRatio = 0.05f;
                    spec.MinimumTargetedRatio = 0.50f;
                    spec.MinimumAbilitySignalCount = 1;
                    break;

                case AIValidationGauntletScenarioType.StuckRecovery:
                    spec.MinimumFailureRecoverySignalCount = 1;
                    spec.MaximumSwitchRatio = 0.90f;
                    break;

                case AIValidationGauntletScenarioType.TeamCoordination:
                    spec.ExpectedActions =
                        AIValidationGauntletActionSet.Approach |
                        AIValidationGauntletActionSet.HoldRange |
                        AIValidationGauntletActionSet.Reposition |
                        AIValidationGauntletActionSet.Peel |
                        AIValidationGauntletActionSet.Regroup |
                        AIValidationGauntletActionSet.Objective;
                    spec.MinimumExpectedActionRatio = 0.50f;
                    spec.MinimumTeamRoleAdjustedRatio = 0.05f;
                    spec.MinimumUniqueActionCount = 3;
                    break;

                case AIValidationGauntletScenarioType.MovementLiveness:
                    spec.ExpectedActions =
                        AIValidationGauntletActionSet.Wander |
                        AIValidationGauntletActionSet.Search |
                        AIValidationGauntletActionSet.Regroup |
                        AIValidationGauntletActionSet.Objective;
                    spec.MinimumExpectedActionRatio = 0.30f;
                    spec.MaximumTacticalStopSignalCount = 0;
                    spec.MinimumUniqueActionCount = 2;
                    break;

                case AIValidationGauntletScenarioType.LaneDiscipline:
                    spec.ExpectedActions =
                        AIValidationGauntletActionSet.Search |
                        AIValidationGauntletActionSet.Regroup |
                        AIValidationGauntletActionSet.Objective;
                    spec.MinimumExpectedActionRatio = 0.35f;
                    spec.MinimumObjectiveIntentSignalCount = 1;
                    spec.MinimumTeamRoleAdjustedRatio = 0.03f;
                    spec.MinimumUniqueActionCount = 2;
                    break;

                case AIValidationGauntletScenarioType.GemPickup:
                    spec.ExpectedActions =
                        AIValidationGauntletActionSet.Search |
                        AIValidationGauntletActionSet.Objective;
                    spec.MinimumExpectedActionRatio = 0.35f;
                    spec.MinimumGemIntentSignalCount = 1;
                    spec.MaximumTargetedRatio = 0.75f;
                    break;

                case AIValidationGauntletScenarioType.CombatDiscipline:
                    spec.ExpectedActions =
                        AIValidationGauntletActionSet.Approach |
                        AIValidationGauntletActionSet.HoldRange |
                        AIValidationGauntletActionSet.Reposition |
                        AIValidationGauntletActionSet.Retreat |
                        AIValidationGauntletActionSet.Evade;
                    spec.MinimumExpectedActionRatio = 0.55f;
                    spec.MinimumTargetedRatio = 0.50f;
                    spec.MaximumTacticalStopSignalCount = 0;
                    spec.MinimumUniqueActionCount = 2;
                    break;

                case AIValidationGauntletScenarioType.PlaytestRegression:
                    spec.ExpectedActions =
                        AIValidationGauntletActionSet.Wander |
                        AIValidationGauntletActionSet.Search |
                        AIValidationGauntletActionSet.Approach |
                        AIValidationGauntletActionSet.HoldRange |
                        AIValidationGauntletActionSet.Reposition |
                        AIValidationGauntletActionSet.Retreat |
                        AIValidationGauntletActionSet.Evade |
                        AIValidationGauntletActionSet.Regroup |
                        AIValidationGauntletActionSet.Peel |
                        AIValidationGauntletActionSet.Objective;
                    spec.MinimumExpectedActionRatio = 0.70f;
                    spec.MinimumGemIntentSignalCount = 1;
                    spec.MinimumObjectiveIntentSignalCount = 1;
                    spec.MaximumTacticalStopSignalCount = 0;
                    spec.MaximumObjectiveNeglectSignalCount = 0;
                    spec.MinimumUniqueActionCount = 5;
                    break;
            }

            return spec;
        }

        private static AIValidationGauntletResult EvaluateActiveScenario()
        {
            AIValidationGauntletResult result = BuildResult(
                _activeSpec,
                AIValidationGauntletStatus.Passed,
                "passed");

            if (_frameCount < _activeSpec.MinimumFrameCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "insufficient_frames");

            if (_botDecisionCount < _activeSpec.MinimumBotDecisionCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "insufficient_decisions");

            if (result.InvalidDecisionRatio > _activeSpec.MaximumInvalidDecisionRatio)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "invalid_decisions");

            if (result.TargetedRatio < _activeSpec.MinimumTargetedRatio)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "target_ratio_low");

            if (result.TargetedRatio > _activeSpec.MaximumTargetedRatio)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "target_ratio_high");

            if (_activeSpec.ExpectedActions != AIValidationGauntletActionSet.None &&
                result.ExpectedActionRatio < _activeSpec.MinimumExpectedActionRatio)
            {
                return WithStatus(result, AIValidationGauntletStatus.Failed, "expected_action_low");
            }

            if (result.EmergencyActionRatio < _activeSpec.MinimumEmergencyActionRatio)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "emergency_action_low");

            if (result.TeamRoleAdjustedRatio < _activeSpec.MinimumTeamRoleAdjustedRatio)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "team_coordination_low");

            if (result.AbilitySignalCount < _activeSpec.MinimumAbilitySignalCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "ability_signal_missing");

            if (result.FailureRecoverySignalCount < _activeSpec.MinimumFailureRecoverySignalCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "recovery_signal_missing");

            if (result.MovementSignalCount < _activeSpec.MinimumMovementSignalCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "movement_signal_missing");

            if (result.GemIntentSignalCount < _activeSpec.MinimumGemIntentSignalCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "gem_intent_missing");

            if (result.ObjectiveIntentSignalCount < _activeSpec.MinimumObjectiveIntentSignalCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "objective_intent_missing");

            if (result.TacticalStopSignalCount > _activeSpec.MaximumTacticalStopSignalCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "tactical_stop_high");

            if (result.ObjectiveNeglectSignalCount > _activeSpec.MaximumObjectiveNeglectSignalCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "objective_neglect_high");

            if (result.UniqueActionCount < _activeSpec.MinimumUniqueActionCount)
                return WithStatus(result, AIValidationGauntletStatus.Failed, "action_diversity_low");

            if (result.ZeroScoreRatio > _activeSpec.MaximumZeroScoreRatio)
                return WithStatus(result, AIValidationGauntletStatus.Watch, "zero_score_watch");

            if (result.SwitchRatio > _activeSpec.MaximumSwitchRatio)
                return WithStatus(result, AIValidationGauntletStatus.Watch, "switch_watch");

            if (result.LowConfidenceRatio > _activeSpec.MaximumLowConfidenceRatio)
                return WithStatus(result, AIValidationGauntletStatus.Watch, "low_confidence_watch");

            return result;
        }

        private static AIValidationGauntletResult BuildResult(
            AIValidationGauntletSpec spec,
            AIValidationGauntletStatus status,
            string reason)
        {
            int expectedActionCount = CountExpectedActions(spec.ExpectedActions);
            int abilitySignals = GetSignalCount(AIValidationGauntletSignal.AbilityCast);
            int recoverySignals = GetSignalCount(AIValidationGauntletSignal.FailureRecovery);
            int movementSignals =
                recoverySignals +
                GetSignalCount(AIValidationGauntletSignal.MovementStall) +
                GetSignalCount(AIValidationGauntletSignal.RouteBlocked);

            return new AIValidationGauntletResult
            {
                ScenarioType = spec.ScenarioType,
                Status = status,
                Reason = reason,
                FrameCount = _frameCount,
                BotDecisionCount = _botDecisionCount,
                ExpectedActionCount = expectedActionCount,
                AbilitySignalCount = abilitySignals,
                FailureRecoverySignalCount = recoverySignals,
                MovementSignalCount = movementSignals,
                GemIntentSignalCount = GetSignalCount(AIValidationGauntletSignal.GemPickupIntent),
                ObjectiveIntentSignalCount = GetSignalCount(AIValidationGauntletSignal.ObjectiveIntent),
                TacticalStopSignalCount = GetSignalCount(AIValidationGauntletSignal.TacticalStop),
                ObjectiveNeglectSignalCount = GetSignalCount(AIValidationGauntletSignal.ObjectiveNeglect),
                UniqueActionCount = CountUniqueActions(),
                TargetedRatio = GetRatio(_targetedBotCount),
                ExpectedActionRatio = GetRatio(expectedActionCount),
                InvalidDecisionRatio = GetRatio(_invalidDecisionCount),
                ZeroScoreRatio = GetRatio(_zeroScoreDecisionCount),
                SwitchRatio = GetRatio(_actionSwitchCount),
                LowConfidenceRatio = GetRatio(_lowConfidenceDecisionCount),
                EmergencyActionRatio = GetRatio(_emergencyActionCount),
                TeamRoleAdjustedRatio = GetRatio(_teamRoleAdjustedDecisionCount)
            };
        }

        private static AIValidationGauntletResult WithStatus(
            AIValidationGauntletResult result,
            AIValidationGauntletStatus status,
            string reason)
        {
            result.Status = status;
            result.Reason = reason;
            return result;
        }

        private static AIValidationGauntletSpec NormalizeSpec(AIValidationGauntletSpec spec)
        {
            if (spec.MinimumFrameCount <= 0)
                spec.MinimumFrameCount = 1;

            if (spec.MinimumBotDecisionCount <= 0)
                spec.MinimumBotDecisionCount = 1;

            spec.MinimumTargetedRatio = Clamp01(spec.MinimumTargetedRatio);
            spec.MaximumTargetedRatio = Clamp01(spec.MaximumTargetedRatio <= 0f ? 1f : spec.MaximumTargetedRatio);
            if (spec.MaximumTargetedRatio < spec.MinimumTargetedRatio)
                spec.MaximumTargetedRatio = spec.MinimumTargetedRatio;

            spec.MinimumExpectedActionRatio = Clamp01(spec.MinimumExpectedActionRatio);
            spec.MaximumInvalidDecisionRatio = Clamp01(spec.MaximumInvalidDecisionRatio);
            spec.MaximumZeroScoreRatio = Clamp01(spec.MaximumZeroScoreRatio);
            spec.MaximumSwitchRatio = Clamp01(spec.MaximumSwitchRatio <= 0f ? 1f : spec.MaximumSwitchRatio);
            spec.MaximumLowConfidenceRatio = Clamp01(spec.MaximumLowConfidenceRatio <= 0f ? 1f : spec.MaximumLowConfidenceRatio);
            spec.MinimumEmergencyActionRatio = Clamp01(spec.MinimumEmergencyActionRatio);
            spec.MinimumTeamRoleAdjustedRatio = Clamp01(spec.MinimumTeamRoleAdjustedRatio);

            if (spec.MinimumAbilitySignalCount < 0)
                spec.MinimumAbilitySignalCount = 0;

            if (spec.MinimumFailureRecoverySignalCount < 0)
                spec.MinimumFailureRecoverySignalCount = 0;

            if (spec.MinimumMovementSignalCount < 0)
                spec.MinimumMovementSignalCount = 0;

            if (spec.MinimumGemIntentSignalCount < 0)
                spec.MinimumGemIntentSignalCount = 0;

            if (spec.MinimumObjectiveIntentSignalCount < 0)
                spec.MinimumObjectiveIntentSignalCount = 0;

            if (spec.MaximumTacticalStopSignalCount < 0)
                spec.MaximumTacticalStopSignalCount = 0;

            if (spec.MaximumObjectiveNeglectSignalCount < 0)
                spec.MaximumObjectiveNeglectSignalCount = 0;

            if (spec.MinimumUniqueActionCount < 0)
                spec.MinimumUniqueActionCount = 0;

            return spec;
        }

        private static int CountExpectedActions(AIValidationGauntletActionSet expectedActions)
        {
            int count = 0;
            for (int action = 1; action < ActionSlotCount; action++)
            {
                AIActionType actionType = (AIActionType)action;
                if ((expectedActions & ToActionSet(actionType)) != 0)
                    count += _actionTotals[action];
            }

            return count;
        }

        private static int CountUniqueActions()
        {
            int count = 0;
            for (int action = 1; action < ActionSlotCount; action++)
            {
                if (_actionTotals[action] > 0)
                    count++;
            }

            return count;
        }

        private static AIValidationGauntletActionSet ToActionSet(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Wander:
                    return AIValidationGauntletActionSet.Wander;
                case AIActionType.Search:
                    return AIValidationGauntletActionSet.Search;
                case AIActionType.Approach:
                    return AIValidationGauntletActionSet.Approach;
                case AIActionType.HoldRange:
                    return AIValidationGauntletActionSet.HoldRange;
                case AIActionType.Reposition:
                    return AIValidationGauntletActionSet.Reposition;
                case AIActionType.Retreat:
                    return AIValidationGauntletActionSet.Retreat;
                case AIActionType.Evade:
                    return AIValidationGauntletActionSet.Evade;
                case AIActionType.UseSuper:
                    return AIValidationGauntletActionSet.UseSuper;
                case AIActionType.Regroup:
                    return AIValidationGauntletActionSet.Regroup;
                case AIActionType.Peel:
                    return AIValidationGauntletActionSet.Peel;
                case AIActionType.Objective:
                    return AIValidationGauntletActionSet.Objective;
                default:
                    return AIValidationGauntletActionSet.None;
            }
        }

        private static float GetRatio(int value)
        {
            return _botDecisionCount > 0
                ? (float)value / _botDecisionCount
                : 0f;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
                return 0f;

            return value > 1f ? 1f : value;
        }

        private static bool IsTrackedAction(AIActionType actionType)
        {
            return actionType > AIActionType.None &&
                   (int)actionType < ActionSlotCount;
        }

        private static bool IsTrackedSignal(AIValidationGauntletSignal signal)
        {
            return signal >= AIValidationGauntletSignal.AbilityCast &&
                   (int)signal < SignalSlotCount;
        }

        private static void ClearRunningState()
        {
            _isRunning = false;
            _frameCount = 0;
            _botDecisionCount = 0;
            _targetedBotCount = 0;
            _invalidDecisionCount = 0;
            _zeroScoreDecisionCount = 0;
            _actionSwitchCount = 0;
            _lowConfidenceDecisionCount = 0;
            _emergencyActionCount = 0;
            _teamRoleAdjustedDecisionCount = 0;

            for (int i = 0; i < _actionTotals.Length; i++)
                _actionTotals[i] = 0;

            for (int i = 0; i < _signalTotals.Length; i++)
                _signalTotals[i] = 0;
        }
    }
}
