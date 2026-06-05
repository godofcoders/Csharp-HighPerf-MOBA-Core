namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIPerformanceSnapshot
    {
        public readonly int MapResolveCount;
        public readonly int MapResolveCacheHits;
        public readonly int MapCandidateCount;
        public readonly int MapPathValidationCount;
        public readonly int PathQueryCount;
        public readonly int PathQuerySuccessCount;
        public readonly int PathQueryFailureCount;
        public readonly int PathTouchedNodeCount;
        public readonly int PathMaxTouchedNodes;

        public AIPerformanceSnapshot(
            int mapResolveCount,
            int mapResolveCacheHits,
            int mapCandidateCount,
            int mapPathValidationCount,
            int pathQueryCount,
            int pathQuerySuccessCount,
            int pathQueryFailureCount,
            int pathTouchedNodeCount,
            int pathMaxTouchedNodes)
        {
            MapResolveCount = mapResolveCount;
            MapResolveCacheHits = mapResolveCacheHits;
            MapCandidateCount = mapCandidateCount;
            MapPathValidationCount = mapPathValidationCount;
            PathQueryCount = pathQueryCount;
            PathQuerySuccessCount = pathQuerySuccessCount;
            PathQueryFailureCount = pathQueryFailureCount;
            PathTouchedNodeCount = pathTouchedNodeCount;
            PathMaxTouchedNodes = pathMaxTouchedNodes;
        }

        public float PathFailureRatio =>
            PathQueryCount > 0 ? (float)PathQueryFailureCount / PathQueryCount : 0f;

        public bool IsOverBudget(
            int maxMapResolves,
            int maxPathQueries,
            int maxTouchedNodes)
        {
            return MapResolveCount > ClampBudget(maxMapResolves) ||
                   PathQueryCount > ClampBudget(maxPathQueries) ||
                   PathTouchedNodeCount > ClampBudget(maxTouchedNodes);
        }

        public string GetDebugSummary()
        {
            return
                $"Perf=Map resolves={MapResolveCount} " +
                $"cache={MapResolveCacheHits} " +
                $"candidates={MapCandidateCount} " +
                $"validations={MapPathValidationCount} " +
                $"paths={PathQueryCount} " +
                $"ok={PathQuerySuccessCount} " +
                $"fail={PathQueryFailureCount} " +
                $"nodes={PathTouchedNodeCount} " +
                $"maxNodes={PathMaxTouchedNodes}";
        }

        public string GetBudgetSummary(
            int maxMapResolves,
            int maxPathQueries,
            int maxTouchedNodes)
        {
            bool overBudget = IsOverBudget(
                maxMapResolves,
                maxPathQueries,
                maxTouchedNodes);

            return
                $"Budget={(overBudget ? "OVER" : "OK")} " +
                $"map={MapResolveCount}/{ClampBudget(maxMapResolves)} " +
                $"paths={PathQueryCount}/{ClampBudget(maxPathQueries)} " +
                $"nodes={PathTouchedNodeCount}/{ClampBudget(maxTouchedNodes)} " +
                $"maxNodes={PathMaxTouchedNodes}";
        }

        private static int ClampBudget(int value)
        {
            return value <= 0 ? 1 : value;
        }
    }

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

        public static AIPerformanceSnapshot GetSnapshot(uint currentTick)
        {
            EnsureTick(currentTick);

            return new AIPerformanceSnapshot(
                _mapResolveCount,
                _mapResolveCacheHits,
                _mapCandidateCount,
                _mapPathValidationCount,
                _pathQueryCount,
                _pathQuerySuccessCount,
                _pathQueryFailureCount,
                _pathTouchedNodeCount,
                _pathMaxTouchedNodes);
        }

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
            return GetSnapshot(currentTick).GetDebugSummary();
        }

        public static string GetBudgetSummary(
            uint currentTick,
            int maxMapResolves,
            int maxPathQueries,
            int maxTouchedNodes)
        {
            return GetSnapshot(currentTick).GetBudgetSummary(
                maxMapResolves,
                maxPathQueries,
                maxTouchedNodes);
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
