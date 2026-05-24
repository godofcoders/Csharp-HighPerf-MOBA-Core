namespace MOBA.Core.Simulation.AI
{
    public enum AIValidationScenarioType
    {
        None,
        Combat,
        Objective,
        Mixed
    }

    public static class AIValidationScenarioTracker
    {
        private const int ActionSlotCount = (int)AIActionType.Objective + 1;
        private const int ScenarioSlotCount = (int)AIValidationScenarioType.Mixed + 1;
        private const float CombatTargetRatio = 0.60f;
        private const float ObjectiveTargetlessRatio = 0.60f;
        private const float FrameSwitchWatchRatio = 0.65f;
        private const float FrameLowConfidenceWatchRatio = 0.70f;

        private struct ScenarioStats
        {
            public int FrameCount;
            public int BotDecisionCount;
            public int FailFrameCount;
            public int WatchFrameCount;
            public int InvalidDecisionCount;
            public int ZeroScoreDecisionCount;
            public int ActionSwitchCount;
            public int LowConfidenceDecisionCount;
            public AIValidationHealthStatus WorstStatus;
            public string LastSignal;
        }

        private static readonly ScenarioStats[] _stats = new ScenarioStats[ScenarioSlotCount];
        private static readonly int[,] _actionTotals = new int[ScenarioSlotCount, ActionSlotCount];

        private static int _totalFrameCount;
        private static int _totalBotDecisionCount;
        private static int _totalFailFrameCount;
        private static int _totalWatchFrameCount;
        private static AIValidationScenarioType _currentScenario = AIValidationScenarioType.None;
        private static AIValidationHealthStatus _currentHealthStatus = AIValidationHealthStatus.NoData;
        private static string _currentSignal = "NoData";

        public static int TotalFrameCount => _totalFrameCount;
        public static int TotalBotDecisionCount => _totalBotDecisionCount;
        public static int TotalFailFrameCount => _totalFailFrameCount;
        public static int TotalWatchFrameCount => _totalWatchFrameCount;
        public static AIValidationScenarioType CurrentScenario => _currentScenario;
        public static AIValidationHealthStatus CurrentHealthStatus => _currentHealthStatus;
        public static string CurrentSignal => _currentSignal;

        public static void RecordFrame(
            AIValidationFrame frame,
            int[] actionCounts,
            AIValidationHealthStatus healthStatus,
            string healthSignal)
        {
            if (frame.ActiveBotCount <= 0)
                return;

            AIValidationScenarioType scenario = ClassifyFrame(frame, actionCounts);
            int scenarioIndex = (int)scenario;

            _currentScenario = scenario;
            _currentHealthStatus = healthStatus;
            _currentSignal = string.IsNullOrEmpty(healthSignal) ? "Stable" : healthSignal;
            AIValidationHealthStatus frameStatus = GetFrameLocalStatus(frame);

            _totalFrameCount++;
            _totalBotDecisionCount += frame.ActiveBotCount;

            ScenarioStats stats = _stats[scenarioIndex];
            stats.FrameCount++;
            stats.BotDecisionCount += frame.ActiveBotCount;
            stats.InvalidDecisionCount += frame.InvalidDecisionCount;
            stats.ZeroScoreDecisionCount += frame.ZeroScoreDecisionCount;
            stats.ActionSwitchCount += frame.ActionSwitchCount;
            stats.LowConfidenceDecisionCount += frame.LowConfidenceDecisionCount;
            stats.LastSignal = _currentSignal;

            if (frameStatus > stats.WorstStatus)
                stats.WorstStatus = frameStatus;

            if (frameStatus == AIValidationHealthStatus.Fail)
            {
                stats.FailFrameCount++;
                _totalFailFrameCount++;
            }
            else if (frameStatus == AIValidationHealthStatus.Watch)
            {
                stats.WatchFrameCount++;
                _totalWatchFrameCount++;
            }

            _stats[scenarioIndex] = stats;

            for (int action = 1; action < ActionSlotCount; action++)
            {
                int count = actionCounts != null && action < actionCounts.Length
                    ? actionCounts[action]
                    : 0;

                if (count > 0)
                    _actionTotals[scenarioIndex, action] += count;
            }
        }

        public static AIValidationScenarioType ClassifyFrame(
            AIValidationFrame frame,
            int[] actionCounts)
        {
            if (frame.ActiveBotCount <= 0)
                return AIValidationScenarioType.None;

            float activeBotCount = frame.ActiveBotCount;
            float targetedRatio = frame.TargetedBotCount / activeBotCount;
            float targetlessRatio = frame.TargetlessBotCount / activeBotCount;

            int combatActions =
                GetActionCount(actionCounts, AIActionType.Approach) +
                GetActionCount(actionCounts, AIActionType.HoldRange) +
                GetActionCount(actionCounts, AIActionType.Reposition) +
                GetActionCount(actionCounts, AIActionType.Retreat) +
                GetActionCount(actionCounts, AIActionType.Evade) +
                GetActionCount(actionCounts, AIActionType.UseSuper) +
                GetActionCount(actionCounts, AIActionType.Peel);

            int mapActions =
                GetActionCount(actionCounts, AIActionType.Search) +
                GetActionCount(actionCounts, AIActionType.Objective) +
                GetActionCount(actionCounts, AIActionType.Wander);

            if (targetedRatio >= CombatTargetRatio && combatActions >= mapActions)
                return AIValidationScenarioType.Combat;

            if (targetlessRatio >= ObjectiveTargetlessRatio && mapActions > 0)
                return AIValidationScenarioType.Objective;

            return AIValidationScenarioType.Mixed;
        }

        public static int GetFrameCount(AIValidationScenarioType scenario)
        {
            return IsTrackedScenario(scenario)
                ? _stats[(int)scenario].FrameCount
                : 0;
        }

        public static int GetBotDecisionCount(AIValidationScenarioType scenario)
        {
            return IsTrackedScenario(scenario)
                ? _stats[(int)scenario].BotDecisionCount
                : 0;
        }

        public static int GetFailFrameCount(AIValidationScenarioType scenario)
        {
            return IsTrackedScenario(scenario)
                ? _stats[(int)scenario].FailFrameCount
                : 0;
        }

        public static int GetWatchFrameCount(AIValidationScenarioType scenario)
        {
            return IsTrackedScenario(scenario)
                ? _stats[(int)scenario].WatchFrameCount
                : 0;
        }

        public static AIValidationHealthStatus GetWorstStatus(AIValidationScenarioType scenario)
        {
            return IsTrackedScenario(scenario)
                ? _stats[(int)scenario].WorstStatus
                : AIValidationHealthStatus.NoData;
        }

        public static int GetActionCount(
            AIValidationScenarioType scenario,
            AIActionType actionType)
        {
            return IsTrackedScenario(scenario) && IsTrackedAction(actionType)
                ? _actionTotals[(int)scenario, (int)actionType]
                : 0;
        }

        public static string GetDebugSummary()
        {
            if (_totalFrameCount <= 0)
                return "Scenario=NO_DATA frames=0 signal=NoData";

            return
                $"Scenario={_currentScenario} " +
                $"health={GetStatusLabel(_currentHealthStatus)} " +
                $"frames={_totalFrameCount} " +
                $"bots={_totalBotDecisionCount} " +
                $"C/O/M={GetFrameCount(AIValidationScenarioType.Combat)}/" +
                $"{GetFrameCount(AIValidationScenarioType.Objective)}/" +
                $"{GetFrameCount(AIValidationScenarioType.Mixed)} " +
                $"fail={_totalFailFrameCount} " +
                $"watch={_totalWatchFrameCount} " +
                $"signal={_currentSignal}";
        }

        public static void ResetForTests()
        {
            Clear();
        }

        public static void Clear()
        {
            for (int scenario = 0; scenario < ScenarioSlotCount; scenario++)
            {
                _stats[scenario] = new ScenarioStats
                {
                    LastSignal = "NoData"
                };

                for (int action = 0; action < ActionSlotCount; action++)
                {
                    _actionTotals[scenario, action] = 0;
                }
            }

            _totalFrameCount = 0;
            _totalBotDecisionCount = 0;
            _totalFailFrameCount = 0;
            _totalWatchFrameCount = 0;
            _currentScenario = AIValidationScenarioType.None;
            _currentHealthStatus = AIValidationHealthStatus.NoData;
            _currentSignal = "NoData";
        }

        private static int GetActionCount(int[] actionCounts, AIActionType actionType)
        {
            return IsTrackedAction(actionType) &&
                   actionCounts != null &&
                   (int)actionType < actionCounts.Length
                ? actionCounts[(int)actionType]
                : 0;
        }

        private static AIValidationHealthStatus GetFrameLocalStatus(AIValidationFrame frame)
        {
            if (frame.InvalidDecisionCount > 0)
                return AIValidationHealthStatus.Fail;

            if (frame.ZeroScoreDecisionCount > 0)
                return AIValidationHealthStatus.Watch;

            if (frame.ActiveBotCount <= 0)
                return AIValidationHealthStatus.NoData;

            float activeBotCount = frame.ActiveBotCount;
            if (frame.ActionSwitchCount / activeBotCount > FrameSwitchWatchRatio)
                return AIValidationHealthStatus.Watch;

            if (frame.LowConfidenceDecisionCount / activeBotCount > FrameLowConfidenceWatchRatio)
                return AIValidationHealthStatus.Watch;

            return AIValidationHealthStatus.Healthy;
        }

        private static bool IsTrackedScenario(AIValidationScenarioType scenario)
        {
            return scenario > AIValidationScenarioType.None &&
                   (int)scenario < ScenarioSlotCount;
        }

        private static bool IsTrackedAction(AIActionType actionType)
        {
            return actionType > AIActionType.None &&
                   (int)actionType < ActionSlotCount;
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
