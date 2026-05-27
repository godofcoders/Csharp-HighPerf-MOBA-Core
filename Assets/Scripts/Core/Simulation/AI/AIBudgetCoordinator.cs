namespace MOBA.Core.Simulation.AI
{
    public static class AIBudgetCoordinator
    {
        private static uint _tick;
        private static bool _hasTick;

        private static int _perceptionGrants;
        private static int _perceptionDeferrals;
        private static int _dangerGrants;
        private static int _dangerDeferrals;
        private static int _mapGrants;
        private static int _mapDeferrals;
        private static int _pathGrants;
        private static int _pathDeferrals;
        private static int _criticalOverspends;

        public static int PerceptionGrants => _perceptionGrants;
        public static int PerceptionDeferrals => _perceptionDeferrals;
        public static int DangerGrants => _dangerGrants;
        public static int DangerDeferrals => _dangerDeferrals;
        public static int MapGrants => _mapGrants;
        public static int MapDeferrals => _mapDeferrals;
        public static int PathGrants => _pathGrants;
        public static int PathDeferrals => _pathDeferrals;
        public static int CriticalOverspends => _criticalOverspends;

        public static bool TryAcquirePerceptionScan(
            uint currentTick,
            BrawlerAIProfile profile,
            bool highPriority)
        {
            return TryAcquire(
                currentTick,
                profile,
                profile != null ? profile.MaxPerceptionScansPerTick : 1,
                highPriority,
                ref _perceptionGrants,
                ref _perceptionDeferrals);
        }

        public static bool TryAcquireDangerRefresh(
            uint currentTick,
            BrawlerAIProfile profile,
            bool highPriority)
        {
            return TryAcquire(
                currentTick,
                profile,
                profile != null ? profile.MaxDangerRefreshesPerTick : 1,
                highPriority,
                ref _dangerGrants,
                ref _dangerDeferrals);
        }

        public static bool TryAcquireMapResolve(
            uint currentTick,
            BrawlerAIProfile profile,
            bool highPriority)
        {
            return TryAcquire(
                currentTick,
                profile,
                profile != null ? profile.MaxMapResolvesPerTick : 1,
                highPriority,
                ref _mapGrants,
                ref _mapDeferrals);
        }

        public static bool TryAcquirePathQuery(
            uint currentTick,
            BrawlerAIProfile profile,
            bool highPriority)
        {
            return TryAcquire(
                currentTick,
                profile,
                profile != null ? profile.MaxPathQueriesPerTick : 1,
                highPriority,
                ref _pathGrants,
                ref _pathDeferrals);
        }

        public static bool HasPressure(uint currentTick)
        {
            EnsureTick(currentTick);

            return _perceptionDeferrals > 0 ||
                   _dangerDeferrals > 0 ||
                   _mapDeferrals > 0 ||
                   _pathDeferrals > 0 ||
                   _criticalOverspends > 0;
        }

        public static string GetDebugSummary(uint currentTick)
        {
            EnsureTick(currentTick);

            return
                $"BudgetLOD " +
                $"sense={_perceptionGrants}/{_perceptionDeferrals} " +
                $"danger={_dangerGrants}/{_dangerDeferrals} " +
                $"map={_mapGrants}/{_mapDeferrals} " +
                $"path={_pathGrants}/{_pathDeferrals} " +
                $"critical={_criticalOverspends}";
        }

        public static void ResetForTests()
        {
            _hasTick = false;
            Reset(0u);
        }

        private static bool TryAcquire(
            uint currentTick,
            BrawlerAIProfile profile,
            int maxPerTick,
            bool highPriority,
            ref int grants,
            ref int deferrals)
        {
            EnsureTick(currentTick);

            if (profile == null || !profile.EnableAIBudgetEnforcement)
            {
                grants++;
                return true;
            }

            int budget = ClampBudget(maxPerTick);
            if (grants < budget)
            {
                grants++;
                return true;
            }

            if (highPriority && profile.AllowCriticalBudgetOverspend)
            {
                grants++;
                _criticalOverspends++;
                return true;
            }

            deferrals++;
            return false;
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

            _perceptionGrants = 0;
            _perceptionDeferrals = 0;
            _dangerGrants = 0;
            _dangerDeferrals = 0;
            _mapGrants = 0;
            _mapDeferrals = 0;
            _pathGrants = 0;
            _pathDeferrals = 0;
            _criticalOverspends = 0;
        }

        private static int ClampBudget(int value)
        {
            return value <= 0 ? 1 : value;
        }
    }
}
