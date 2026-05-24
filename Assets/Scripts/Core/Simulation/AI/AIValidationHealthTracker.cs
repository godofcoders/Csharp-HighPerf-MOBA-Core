namespace MOBA.Core.Simulation.AI
{
    public enum AIValidationHealthStatus
    {
        NoData,
        Healthy,
        Watch,
        Fail
    }

    public struct AIValidationFrame
    {
        public uint Tick;
        public int ActiveBotCount;
        public int TargetedBotCount;
        public int TargetlessBotCount;
        public int ActionSwitchCount;
        public int InvalidDecisionCount;
        public int LowConfidenceDecisionCount;
        public int ZeroScoreDecisionCount;
        public int EmergencyActionCount;
        public int TeamRoleAdjustedDecisionCount;
        public float AverageTopScore;
        public float AverageScoreMargin;
    }

    public static class AIValidationHealthTracker
    {
        public const int WindowFrameCapacity = 180;

        private const int ActionSlotCount = (int)AIActionType.Objective + 1;
        private const int MinimumDecisionCount = 12;
        private const int DominanceMinimumDecisionCount = 24;
        private const float MaxZeroScoreRatio = 0.10f;
        private const float MaxSwitchRatio = 0.65f;
        private const float MaxLowConfidenceRatio = 0.70f;
        private const float MaxDominantActionRatio = 0.85f;

        private static readonly AIValidationFrame[] _frames =
            new AIValidationFrame[WindowFrameCapacity];
        private static readonly int[,] _actionCountsByFrame =
            new int[WindowFrameCapacity, ActionSlotCount];
        private static readonly int[] _actionTotals = new int[ActionSlotCount];

        private static int _nextFrameIndex;
        private static int _frameCount;

        private static int _activeBotTotal;
        private static int _targetedBotTotal;
        private static int _targetlessBotTotal;
        private static int _actionSwitchTotal;
        private static int _invalidDecisionTotal;
        private static int _lowConfidenceDecisionTotal;
        private static int _zeroScoreDecisionTotal;
        private static int _emergencyActionTotal;
        private static int _teamRoleAdjustedDecisionTotal;

        private static AIActionType _dominantActionType = AIActionType.None;
        private static int _dominantActionCount;
        private static int _uniqueActionCount;
        private static AIValidationHealthStatus _status = AIValidationHealthStatus.NoData;
        private static string _primarySignal = "NoData";

        public static int WindowFrameCount => _frameCount;
        public static int BotDecisionCount => _activeBotTotal;
        public static int TargetedBotTotal => _targetedBotTotal;
        public static int TargetlessBotTotal => _targetlessBotTotal;
        public static int InvalidDecisionTotal => _invalidDecisionTotal;
        public static int ZeroScoreDecisionTotal => _zeroScoreDecisionTotal;
        public static int ActionSwitchTotal => _actionSwitchTotal;
        public static int LowConfidenceDecisionTotal => _lowConfidenceDecisionTotal;
        public static int EmergencyActionTotal => _emergencyActionTotal;
        public static int TeamRoleAdjustedDecisionTotal => _teamRoleAdjustedDecisionTotal;
        public static AIActionType DominantActionType => _dominantActionType;
        public static int DominantActionCount => _dominantActionCount;
        public static int UniqueActionCount => _uniqueActionCount;
        public static AIValidationHealthStatus Status => _status;
        public static string PrimarySignal => _primarySignal;

        public static float InvalidDecisionRatio => GetRatio(_invalidDecisionTotal);
        public static float ZeroScoreRatio => GetRatio(_zeroScoreDecisionTotal);
        public static float ActionSwitchRatio => GetRatio(_actionSwitchTotal);
        public static float LowConfidenceRatio => GetRatio(_lowConfidenceDecisionTotal);
        public static float DominantActionRatio => GetRatio(_dominantActionCount);

        public static void RecordFrame(AIValidationFrame frame, int[] actionCounts)
        {
            if (frame.ActiveBotCount <= 0)
            {
                Evaluate();
                return;
            }

            if (_frameCount == WindowFrameCapacity)
            {
                RemoveFrame(_nextFrameIndex);
            }
            else
            {
                _frameCount++;
            }

            StoreFrame(_nextFrameIndex, frame, actionCounts);
            _nextFrameIndex++;
            if (_nextFrameIndex >= WindowFrameCapacity)
                _nextFrameIndex = 0;

            Evaluate();
        }

        public static string GetDebugSummary()
        {
            if (_status == AIValidationHealthStatus.NoData)
            {
                return
                    $"Health=NO_DATA win={_frameCount}/{WindowFrameCapacity} " +
                    "bots=0 signal=NoData";
            }

            return
                $"Health={GetStatusLabel(_status)} " +
                $"win={_frameCount}/{WindowFrameCapacity} " +
                $"bots={_activeBotTotal} " +
                $"inv={_invalidDecisionTotal}({InvalidDecisionRatio:0%}) " +
                $"zero={_zeroScoreDecisionTotal}({ZeroScoreRatio:0%}) " +
                $"switch={_actionSwitchTotal}({ActionSwitchRatio:0%}) " +
                $"low={_lowConfidenceDecisionTotal}({LowConfidenceRatio:0%}) " +
                $"dom={_dominantActionType}:{DominantActionRatio:0%} " +
                $"unique={_uniqueActionCount} " +
                $"signal={_primarySignal}";
        }

        public static void ResetForTests()
        {
            Clear();
        }

        public static void Clear()
        {
            for (int i = 0; i < WindowFrameCapacity; i++)
            {
                _frames[i] = new AIValidationFrame();

                for (int action = 0; action < ActionSlotCount; action++)
                {
                    _actionCountsByFrame[i, action] = 0;
                }
            }

            for (int i = 0; i < _actionTotals.Length; i++)
            {
                _actionTotals[i] = 0;
            }

            _nextFrameIndex = 0;
            _frameCount = 0;
            _activeBotTotal = 0;
            _targetedBotTotal = 0;
            _targetlessBotTotal = 0;
            _actionSwitchTotal = 0;
            _invalidDecisionTotal = 0;
            _lowConfidenceDecisionTotal = 0;
            _zeroScoreDecisionTotal = 0;
            _emergencyActionTotal = 0;
            _teamRoleAdjustedDecisionTotal = 0;
            _dominantActionType = AIActionType.None;
            _dominantActionCount = 0;
            _uniqueActionCount = 0;
            _status = AIValidationHealthStatus.NoData;
            _primarySignal = "NoData";
        }

        private static void StoreFrame(int index, AIValidationFrame frame, int[] actionCounts)
        {
            _frames[index] = frame;

            _activeBotTotal += frame.ActiveBotCount;
            _targetedBotTotal += frame.TargetedBotCount;
            _targetlessBotTotal += frame.TargetlessBotCount;
            _actionSwitchTotal += frame.ActionSwitchCount;
            _invalidDecisionTotal += frame.InvalidDecisionCount;
            _lowConfidenceDecisionTotal += frame.LowConfidenceDecisionCount;
            _zeroScoreDecisionTotal += frame.ZeroScoreDecisionCount;
            _emergencyActionTotal += frame.EmergencyActionCount;
            _teamRoleAdjustedDecisionTotal += frame.TeamRoleAdjustedDecisionCount;

            for (int action = 0; action < ActionSlotCount; action++)
            {
                int count = actionCounts != null && action < actionCounts.Length
                    ? actionCounts[action]
                    : 0;

                if (count < 0)
                    count = 0;

                _actionCountsByFrame[index, action] = count;
                _actionTotals[action] += count;
            }
        }

        private static void RemoveFrame(int index)
        {
            AIValidationFrame frame = _frames[index];

            _activeBotTotal -= frame.ActiveBotCount;
            _targetedBotTotal -= frame.TargetedBotCount;
            _targetlessBotTotal -= frame.TargetlessBotCount;
            _actionSwitchTotal -= frame.ActionSwitchCount;
            _invalidDecisionTotal -= frame.InvalidDecisionCount;
            _lowConfidenceDecisionTotal -= frame.LowConfidenceDecisionCount;
            _zeroScoreDecisionTotal -= frame.ZeroScoreDecisionCount;
            _emergencyActionTotal -= frame.EmergencyActionCount;
            _teamRoleAdjustedDecisionTotal -= frame.TeamRoleAdjustedDecisionCount;
            _frames[index] = new AIValidationFrame();

            for (int action = 0; action < ActionSlotCount; action++)
            {
                int count = _actionCountsByFrame[index, action];
                _actionTotals[action] -= count;
                _actionCountsByFrame[index, action] = 0;
            }
        }

        private static void Evaluate()
        {
            RefreshActionDistribution();

            if (_activeBotTotal <= 0)
            {
                _status = AIValidationHealthStatus.NoData;
                _primarySignal = "NoData";
                return;
            }

            if (_invalidDecisionTotal > 0)
            {
                _status = AIValidationHealthStatus.Fail;
                _primarySignal = "InvalidContext";
                return;
            }

            if (_activeBotTotal >= MinimumDecisionCount &&
                ZeroScoreRatio > MaxZeroScoreRatio)
            {
                _status = AIValidationHealthStatus.Fail;
                _primarySignal = "ZeroScores";
                return;
            }

            if (_activeBotTotal >= MinimumDecisionCount &&
                ActionSwitchRatio > MaxSwitchRatio)
            {
                _status = AIValidationHealthStatus.Watch;
                _primarySignal = "ActionFlicker";
                return;
            }

            if (_activeBotTotal >= MinimumDecisionCount &&
                LowConfidenceRatio > MaxLowConfidenceRatio)
            {
                _status = AIValidationHealthStatus.Watch;
                _primarySignal = "LowConfidence";
                return;
            }

            if (_activeBotTotal >= DominanceMinimumDecisionCount &&
                DominantActionRatio > MaxDominantActionRatio)
            {
                _status = AIValidationHealthStatus.Watch;
                _primarySignal = "ActionCollapse";
                return;
            }

            _status = AIValidationHealthStatus.Healthy;
            _primarySignal = "Stable";
        }

        private static void RefreshActionDistribution()
        {
            _dominantActionType = AIActionType.None;
            _dominantActionCount = 0;
            _uniqueActionCount = 0;

            for (int action = 1; action < ActionSlotCount; action++)
            {
                int count = _actionTotals[action];
                if (count <= 0)
                    continue;

                _uniqueActionCount++;
                if (count > _dominantActionCount)
                {
                    _dominantActionCount = count;
                    _dominantActionType = (AIActionType)action;
                }
            }
        }

        private static float GetRatio(int value)
        {
            return _activeBotTotal > 0
                ? (float)value / _activeBotTotal
                : 0f;
        }

        private static string GetStatusLabel(AIValidationHealthStatus status)
        {
            switch (status)
            {
                case AIValidationHealthStatus.Healthy:
                    return "OK";
                case AIValidationHealthStatus.Watch:
                    return "WATCH";
                case AIValidationHealthStatus.Fail:
                    return "FAIL";
                default:
                    return "NO_DATA";
            }
        }
    }
}
