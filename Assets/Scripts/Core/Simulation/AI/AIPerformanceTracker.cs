namespace MOBA.Core.Simulation.AI
{
    public static class AIPerformanceTracker
    {
        private static uint _tick;
        private static bool _hasTick;

        private static int _mapResolveCount;
        private static int _mapResolveCacheHits;
        private static int _mapCandidateCount;
        private static int _mapPathValidationCount;

        private static int _pathQueryCount;
        private static int _pathQuerySuccessCount;
        private static int _pathQueryFailureCount;
        private static int _pathTouchedNodeCount;
        private static int _pathMaxTouchedNodes;

        public static int MapResolveCount => _mapResolveCount;
        public static int MapResolveCacheHits => _mapResolveCacheHits;
        public static int MapCandidateCount => _mapCandidateCount;
        public static int MapPathValidationCount => _mapPathValidationCount;
        public static int PathQueryCount => _pathQueryCount;
        public static int PathQuerySuccessCount => _pathQuerySuccessCount;
        public static int PathQueryFailureCount => _pathQueryFailureCount;
        public static int PathTouchedNodeCount => _pathTouchedNodeCount;
        public static int PathMaxTouchedNodes => _pathMaxTouchedNodes;
        public static bool IsOverBudget(
            int maxMapResolves,
            int maxPathQueries,
            int maxTouchedNodes)
        {
            return _mapResolveCount > ClampBudget(maxMapResolves) ||
                   _pathQueryCount > ClampBudget(maxPathQueries) ||
                   _pathTouchedNodeCount > ClampBudget(maxTouchedNodes);
        }

        public static void RecordMapResolve(
            uint currentTick,
            AIMapRouteIntent intent,
            bool cacheHit,
            int candidateCount,
            int pathValidationCount)
        {
            EnsureTick(currentTick);

            _mapResolveCount++;
            if (cacheHit)
                _mapResolveCacheHits++;

            _mapCandidateCount += ClampMetric(candidateCount);
            _mapPathValidationCount += ClampMetric(pathValidationCount);
        }

        public static void RecordPathQuery(
            uint currentTick,
            bool success,
            int touchedNodeCount)
        {
            EnsureTick(currentTick);

            _pathQueryCount++;
            if (success)
                _pathQuerySuccessCount++;
            else
                _pathQueryFailureCount++;

            int safeTouchedNodeCount = ClampMetric(touchedNodeCount);
            _pathTouchedNodeCount += safeTouchedNodeCount;
            if (safeTouchedNodeCount > _pathMaxTouchedNodes)
                _pathMaxTouchedNodes = safeTouchedNodeCount;
        }

        public static string GetDebugSummary(uint currentTick)
        {
            EnsureTick(currentTick);

            return
                $"Perf=Map resolves={_mapResolveCount} " +
                $"cache={_mapResolveCacheHits} " +
                $"candidates={_mapCandidateCount} " +
                $"validations={_mapPathValidationCount} " +
                $"paths={_pathQueryCount} " +
                $"ok={_pathQuerySuccessCount} " +
                $"fail={_pathQueryFailureCount} " +
                $"nodes={_pathTouchedNodeCount} " +
                $"maxNodes={_pathMaxTouchedNodes}";
        }

        public static string GetBudgetSummary(
            uint currentTick,
            int maxMapResolves,
            int maxPathQueries,
            int maxTouchedNodes)
        {
            EnsureTick(currentTick);

            bool overBudget = IsOverBudget(
                maxMapResolves,
                maxPathQueries,
                maxTouchedNodes);

            return
                $"Budget={(overBudget ? "OVER" : "OK")} " +
                $"map={_mapResolveCount}/{ClampBudget(maxMapResolves)} " +
                $"paths={_pathQueryCount}/{ClampBudget(maxPathQueries)} " +
                $"nodes={_pathTouchedNodeCount}/{ClampBudget(maxTouchedNodes)} " +
                $"maxNodes={_pathMaxTouchedNodes}";
        }

        public static void ResetForTests()
        {
            _hasTick = false;
            Reset(0u);
        }

        private static void EnsureTick(uint currentTick)
        {
            if (_hasTick && _tick == currentTick)
                return;

            Reset(currentTick);
        }

        private static void Reset(uint currentTick)
        {
            _tick = currentTick;
            _hasTick = true;

            _mapResolveCount = 0;
            _mapResolveCacheHits = 0;
            _mapCandidateCount = 0;
            _mapPathValidationCount = 0;
            _pathQueryCount = 0;
            _pathQuerySuccessCount = 0;
            _pathQueryFailureCount = 0;
            _pathTouchedNodeCount = 0;
            _pathMaxTouchedNodes = 0;
        }

        private static int ClampBudget(int value)
        {
            return value <= 0 ? 1 : value;
        }

        private static int ClampMetric(int value)
        {
            return value < 0 ? 0 : value;
        }
    }
}
