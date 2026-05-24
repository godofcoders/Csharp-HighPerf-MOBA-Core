using System.Collections.Generic;

namespace MOBA.Core.Simulation.AI
{
    public static class AIValidationTelemetry
    {
        private const int ActionSlotCount = (int)AIActionType.Objective + 1;
        private const uint BotRecordMemoryTicks = 120;
        private const float LowConfidenceMargin = 5f;
        private const float MeaningfulScore = 0.01f;

        private struct BotDecisionRecord
        {
            public AIActionType ActionType;
            public uint Tick;
        }

        private static readonly int[] _actionCounts = new int[ActionSlotCount];
        private static readonly Dictionary<int, BotDecisionRecord> _botRecords =
            new Dictionary<int, BotDecisionRecord>(32);
        private static readonly List<int> _staleBotBuffer = new List<int>(32);

        private static uint _tick;
        private static bool _hasTick;
        private static uint _lastPurgeTick;
        private static bool _hasPurgeTick;

        private static int _activeBotCount;
        private static int _targetedBotCount;
        private static int _targetlessBotCount;
        private static int _actionSwitchCount;
        private static int _invalidDecisionCount;
        private static int _lowConfidenceDecisionCount;
        private static int _zeroScoreDecisionCount;
        private static int _emergencyActionCount;
        private static int _teamRoleAdjustedDecisionCount;

        private static float _topScoreSum;
        private static float _scoreMarginSum;

        public static int ActiveBotCount => _activeBotCount;
        public static int TargetedBotCount => _targetedBotCount;
        public static int TargetlessBotCount => _targetlessBotCount;
        public static int ActionSwitchCount => _actionSwitchCount;
        public static int InvalidDecisionCount => _invalidDecisionCount;
        public static int LowConfidenceDecisionCount => _lowConfidenceDecisionCount;
        public static int ZeroScoreDecisionCount => _zeroScoreDecisionCount;
        public static int EmergencyActionCount => _emergencyActionCount;
        public static int TeamRoleAdjustedDecisionCount => _teamRoleAdjustedDecisionCount;

        public static float AverageTopScore =>
            _activeBotCount > 0 ? _topScoreSum / _activeBotCount : 0f;

        public static float AverageScoreMargin =>
            _activeBotCount > 0 ? _scoreMarginSum / _activeBotCount : 0f;

        public static void RecordDecision(
            int botEntityId,
            uint currentTick,
            AIActionScore chosenAction,
            bool hasLiveTarget,
            IReadOnlyList<AIActionScore> actionScores,
            string teamRoleDebug)
        {
            if (botEntityId == 0)
                return;

            EnsureTick(currentTick);
            PurgeStaleBotRecords(currentTick);

            _activeBotCount++;

            if (hasLiveTarget)
                _targetedBotCount++;
            else
                _targetlessBotCount++;

            if (IsTrackedAction(chosenAction.ActionType))
                _actionCounts[(int)chosenAction.ActionType]++;

            if (chosenAction.Score <= MeaningfulScore)
                _zeroScoreDecisionCount++;

            if (IsEmergencyAction(chosenAction.ActionType))
                _emergencyActionCount++;

            if (HasInvalidContext(chosenAction.ActionType, chosenAction.Score, hasLiveTarget))
                _invalidDecisionCount++;

            if (HasTeamRoleAdjustment(teamRoleDebug))
                _teamRoleAdjustedDecisionCount++;

            CalculateScoreConfidence(
                actionScores,
                out float topScore,
                out float scoreMargin);

            _topScoreSum += topScore;
            _scoreMarginSum += scoreMargin;

            if (topScore > MeaningfulScore && scoreMargin < LowConfidenceMargin)
                _lowConfidenceDecisionCount++;

            if (_botRecords.TryGetValue(botEntityId, out BotDecisionRecord previous) &&
                currentTick - previous.Tick <= BotRecordMemoryTicks &&
                previous.ActionType != chosenAction.ActionType)
            {
                _actionSwitchCount++;
            }

            _botRecords[botEntityId] = new BotDecisionRecord
            {
                ActionType = chosenAction.ActionType,
                Tick = currentTick
            };
        }

        public static int GetActionCount(AIActionType actionType)
        {
            return IsTrackedAction(actionType)
                ? _actionCounts[(int)actionType]
                : 0;
        }

        public static string GetDebugSummary(uint currentTick)
        {
            EnsureTick(currentTick);

            return
                $"Valid=active={_activeBotCount} " +
                $"target={_targetedBotCount}/{_targetlessBotCount} " +
                $"switch={_actionSwitchCount} " +
                $"lowMargin={_lowConfidenceDecisionCount} " +
                $"zero={_zeroScoreDecisionCount} " +
                $"invalid={_invalidDecisionCount} " +
                $"emergency={_emergencyActionCount} " +
                $"roleAdj={_teamRoleAdjustedDecisionCount} " +
                $"avgTop={AverageTopScore:0.0} " +
                $"avgMargin={AverageScoreMargin:0.0} " +
                $"A/H/R={GetActionCount(AIActionType.Approach)}/" +
                $"{GetActionCount(AIActionType.HoldRange)}/" +
                $"{GetActionCount(AIActionType.Reposition)} " +
                $"Rt/E/P={GetActionCount(AIActionType.Retreat)}/" +
                $"{GetActionCount(AIActionType.Evade)}/" +
                $"{GetActionCount(AIActionType.Peel)} " +
                $"U/G={GetActionCount(AIActionType.UseSuper)}/" +
                $"{GetActionCount(AIActionType.Regroup)} " +
                $"S/O/W={GetActionCount(AIActionType.Search)}/" +
                $"{GetActionCount(AIActionType.Objective)}/" +
                $"{GetActionCount(AIActionType.Wander)}";
        }

        public static void ResetForTests()
        {
            AIValidationHealthTracker.ResetForTests();
            _botRecords.Clear();
            _staleBotBuffer.Clear();
            _hasPurgeTick = false;
            _hasTick = false;
            ResetFrame(0u);
        }

        private static void EnsureTick(uint currentTick)
        {
            if (_hasTick && _tick == currentTick)
                return;

            if (_hasTick && currentTick < _tick)
            {
                ClearLongLivedState();
                ResetFrame(currentTick);
                return;
            }

            FlushCurrentFrameToHealth();
            ResetFrame(currentTick);
        }

        private static void ClearLongLivedState()
        {
            AIValidationHealthTracker.Clear();
            _botRecords.Clear();
            _staleBotBuffer.Clear();
            _hasPurgeTick = false;
        }

        private static void FlushCurrentFrameToHealth()
        {
            if (!_hasTick || _activeBotCount <= 0)
                return;

            AIValidationHealthTracker.RecordFrame(
                new AIValidationFrame
                {
                    Tick = _tick,
                    ActiveBotCount = _activeBotCount,
                    TargetedBotCount = _targetedBotCount,
                    TargetlessBotCount = _targetlessBotCount,
                    ActionSwitchCount = _actionSwitchCount,
                    InvalidDecisionCount = _invalidDecisionCount,
                    LowConfidenceDecisionCount = _lowConfidenceDecisionCount,
                    ZeroScoreDecisionCount = _zeroScoreDecisionCount,
                    EmergencyActionCount = _emergencyActionCount,
                    TeamRoleAdjustedDecisionCount = _teamRoleAdjustedDecisionCount,
                    AverageTopScore = AverageTopScore,
                    AverageScoreMargin = AverageScoreMargin
                },
                _actionCounts);
        }

        private static void ResetFrame(uint currentTick)
        {
            _tick = currentTick;
            _hasTick = true;

            _activeBotCount = 0;
            _targetedBotCount = 0;
            _targetlessBotCount = 0;
            _actionSwitchCount = 0;
            _invalidDecisionCount = 0;
            _lowConfidenceDecisionCount = 0;
            _zeroScoreDecisionCount = 0;
            _emergencyActionCount = 0;
            _teamRoleAdjustedDecisionCount = 0;
            _topScoreSum = 0f;
            _scoreMarginSum = 0f;

            for (int i = 0; i < _actionCounts.Length; i++)
            {
                _actionCounts[i] = 0;
            }
        }

        private static void PurgeStaleBotRecords(uint currentTick)
        {
            if (_hasPurgeTick && _lastPurgeTick == currentTick)
                return;

            _hasPurgeTick = true;
            _lastPurgeTick = currentTick;

            _staleBotBuffer.Clear();

            foreach (var pair in _botRecords)
            {
                if (currentTick - pair.Value.Tick > BotRecordMemoryTicks)
                    _staleBotBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _staleBotBuffer.Count; i++)
            {
                _botRecords.Remove(_staleBotBuffer[i]);
            }
        }

        private static void CalculateScoreConfidence(
            IReadOnlyList<AIActionScore> actionScores,
            out float topScore,
            out float scoreMargin)
        {
            topScore = 0f;
            float secondScore = 0f;

            if (actionScores == null)
            {
                scoreMargin = 0f;
                return;
            }

            for (int i = 0; i < actionScores.Count; i++)
            {
                float score = actionScores[i].Score;
                if (score > topScore)
                {
                    secondScore = topScore;
                    topScore = score;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                }
            }

            scoreMargin = topScore - secondScore;
        }

        private static bool HasInvalidContext(
            AIActionType actionType,
            float score,
            bool hasLiveTarget)
        {
            if (score <= MeaningfulScore)
                return false;

            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                case AIActionType.Retreat:
                case AIActionType.UseSuper:
                    return !hasLiveTarget;

                case AIActionType.Search:
                case AIActionType.Regroup:
                case AIActionType.Objective:
                    return hasLiveTarget;

                default:
                    return false;
            }
        }

        private static bool HasTeamRoleAdjustment(string teamRoleDebug)
        {
            return !string.IsNullOrEmpty(teamRoleDebug) &&
                   teamRoleDebug.Contains("Delta=");
        }

        private static bool IsEmergencyAction(AIActionType actionType)
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

        private static bool IsTrackedAction(AIActionType actionType)
        {
            return actionType > AIActionType.None &&
                   (int)actionType < ActionSlotCount;
        }
    }
}
