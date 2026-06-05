namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIMatchTelemetryTrendSnapshot
    {
        public readonly AIValidationHealthStatus Status;
        public readonly string PrimarySignal;
        public readonly int SampleCount;
        public readonly int HealthySampleCount;
        public readonly int WatchSampleCount;
        public readonly int FailSampleCount;
        public readonly int ConsecutiveNonHealthySamples;
        public readonly int ConsecutiveFailSamples;
        public readonly AIValidationHealthStatus LastStatus;
        public readonly string LastSignal;
        public readonly string DominantSignal;
        public readonly float NonHealthyRatio;
        public readonly float FailRatio;

        public AIMatchTelemetryTrendSnapshot(
            AIValidationHealthStatus status,
            string primarySignal,
            int sampleCount,
            int healthySampleCount,
            int watchSampleCount,
            int failSampleCount,
            int consecutiveNonHealthySamples,
            int consecutiveFailSamples,
            AIValidationHealthStatus lastStatus,
            string lastSignal,
            string dominantSignal,
            float nonHealthyRatio,
            float failRatio)
        {
            Status = status;
            PrimarySignal = string.IsNullOrEmpty(primarySignal)
                ? "Stable"
                : primarySignal;
            SampleCount = sampleCount;
            HealthySampleCount = healthySampleCount;
            WatchSampleCount = watchSampleCount;
            FailSampleCount = failSampleCount;
            ConsecutiveNonHealthySamples = consecutiveNonHealthySamples;
            ConsecutiveFailSamples = consecutiveFailSamples;
            LastStatus = lastStatus;
            LastSignal = string.IsNullOrEmpty(lastSignal)
                ? "NoData"
                : lastSignal;
            DominantSignal = string.IsNullOrEmpty(dominantSignal)
                ? "Stable"
                : dominantSignal;
            NonHealthyRatio = nonHealthyRatio;
            FailRatio = failRatio;
        }

        public string GetDebugSummary()
        {
            if (Status == AIValidationHealthStatus.NoData)
                return "MatchTrend=NO_DATA samples=0";

            return
                $"MatchTrend={GetStatusLabel(Status)} " +
                $"signal={PrimarySignal} " +
                $"samples={SampleCount} " +
                $"ok={HealthySampleCount} " +
                $"watch={WatchSampleCount} " +
                $"fail={FailSampleCount} " +
                $"nonHealthy={NonHealthyRatio:0%} " +
                $"failRatio={FailRatio:0%} " +
                $"consec={ConsecutiveNonHealthySamples}/{ConsecutiveFailSamples} " +
                $"last={GetStatusLabel(LastStatus)}:{LastSignal} " +
                $"top={DominantSignal}";
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

    public static class AIMatchTelemetryTrendTracker
    {
        public const int WindowCapacity = 90;
        private const int MinimumRatioSampleCount = 10;
        private const int ConsecutiveWatchThreshold = 5;
        private const int ConsecutiveFailThreshold = 3;
        private const int MinimumFailSampleCount = 4;
        private const float MaxNonHealthyRatio = 0.35f;
        private const float MaxFailRatio = 0.20f;

        private static readonly AIValidationHealthStatus[] _statuses =
            new AIValidationHealthStatus[WindowCapacity];
        private static readonly string[] _signals = new string[WindowCapacity];

        private static int _nextIndex;
        private static int _sampleCount;
        private static bool _hasLastRecordedTick;
        private static uint _lastRecordedTick;
        private static AIMatchTelemetryTrendSnapshot _lastSnapshot = NoData();

        public static AIMatchTelemetryTrendSnapshot LastSnapshot => _lastSnapshot;

        public static AIMatchTelemetryTrendSnapshot Record(
            AIMatchTelemetryReviewSnapshot review)
        {
            if (review.Status == AIValidationHealthStatus.NoData)
                return _lastSnapshot;

            if (_hasLastRecordedTick && review.Tick < _lastRecordedTick)
                Clear();

            if (_hasLastRecordedTick && review.Tick == _lastRecordedTick)
                return _lastSnapshot;

            Store(review);
            _hasLastRecordedTick = true;
            _lastRecordedTick = review.Tick;
            _lastSnapshot = Evaluate();
            return _lastSnapshot;
        }

        public static string GetDebugSummary()
        {
            return _lastSnapshot.GetDebugSummary();
        }

        public static void ResetForTests()
        {
            Clear();
        }

        public static void Clear()
        {
            for (int i = 0; i < WindowCapacity; i++)
            {
                _statuses[i] = AIValidationHealthStatus.NoData;
                _signals[i] = null;
            }

            _nextIndex = 0;
            _sampleCount = 0;
            _hasLastRecordedTick = false;
            _lastRecordedTick = 0u;
            _lastSnapshot = NoData();
        }

        private static void Store(AIMatchTelemetryReviewSnapshot review)
        {
            _statuses[_nextIndex] = review.Status;
            _signals[_nextIndex] = review.PrimarySignal;

            _nextIndex++;
            if (_nextIndex >= WindowCapacity)
                _nextIndex = 0;

            if (_sampleCount < WindowCapacity)
                _sampleCount++;
        }

        private static AIMatchTelemetryTrendSnapshot Evaluate()
        {
            if (_sampleCount <= 0)
                return NoData();

            int healthy = 0;
            int watch = 0;
            int fail = 0;

            for (int i = 0; i < _sampleCount; i++)
            {
                switch (_statuses[i])
                {
                    case AIValidationHealthStatus.Healthy:
                        healthy++;
                        break;
                    case AIValidationHealthStatus.Watch:
                        watch++;
                        break;
                    case AIValidationHealthStatus.Fail:
                        fail++;
                        break;
                }
            }

            int newestIndex = GetNewestIndex();
            AIValidationHealthStatus lastStatus = _statuses[newestIndex];
            string lastSignal = _signals[newestIndex];
            int consecutiveNonHealthy = CountConsecutive(nonHealthy: true);
            int consecutiveFail = CountConsecutive(nonHealthy: false);
            float nonHealthyRatio = GetRatio(watch + fail, _sampleCount);
            float failRatio = GetRatio(fail, _sampleCount);
            string dominantSignal = ResolveDominantSignal();
            AIValidationHealthStatus trendStatus = ResolveTrendStatus(
                _sampleCount,
                consecutiveNonHealthy,
                consecutiveFail,
                fail,
                nonHealthyRatio,
                failRatio);
            string signal = trendStatus == AIValidationHealthStatus.Healthy
                ? "Stable"
                : dominantSignal;

            return new AIMatchTelemetryTrendSnapshot(
                trendStatus,
                signal,
                _sampleCount,
                healthy,
                watch,
                fail,
                consecutiveNonHealthy,
                consecutiveFail,
                lastStatus,
                lastSignal,
                dominantSignal,
                nonHealthyRatio,
                failRatio);
        }

        private static AIValidationHealthStatus ResolveTrendStatus(
            int sampleCount,
            int consecutiveNonHealthy,
            int consecutiveFail,
            int failCount,
            float nonHealthyRatio,
            float failRatio)
        {
            if (consecutiveFail >= ConsecutiveFailThreshold ||
                (sampleCount >= MinimumRatioSampleCount &&
                 (failCount >= MinimumFailSampleCount || failRatio > MaxFailRatio)))
            {
                return AIValidationHealthStatus.Fail;
            }

            if (consecutiveNonHealthy >= ConsecutiveWatchThreshold ||
                (sampleCount >= MinimumRatioSampleCount &&
                 nonHealthyRatio > MaxNonHealthyRatio))
            {
                return AIValidationHealthStatus.Watch;
            }

            return AIValidationHealthStatus.Healthy;
        }

        private static int CountConsecutive(bool nonHealthy)
        {
            int count = 0;

            for (int age = 0; age < _sampleCount; age++)
            {
                AIValidationHealthStatus status =
                    _statuses[GetIndexFromNewest(age)];
                bool matches = nonHealthy
                    ? status == AIValidationHealthStatus.Watch ||
                      status == AIValidationHealthStatus.Fail
                    : status == AIValidationHealthStatus.Fail;

                if (!matches)
                    break;

                count++;
            }

            return count;
        }

        private static string ResolveDominantSignal()
        {
            string bestSignal = "Stable";
            int bestCount = 0;

            for (int i = 0; i < _sampleCount; i++)
            {
                if (_statuses[i] == AIValidationHealthStatus.Healthy)
                    continue;

                string signal = string.IsNullOrEmpty(_signals[i])
                    ? "Unknown"
                    : _signals[i];
                int count = CountSignal(signal);
                if (count > bestCount)
                {
                    bestSignal = signal;
                    bestCount = count;
                }
            }

            return bestSignal;
        }

        private static int CountSignal(string signal)
        {
            int count = 0;

            for (int i = 0; i < _sampleCount; i++)
            {
                if (_statuses[i] == AIValidationHealthStatus.Healthy)
                    continue;

                string current = string.IsNullOrEmpty(_signals[i])
                    ? "Unknown"
                    : _signals[i];
                if (current == signal)
                    count++;
            }

            return count;
        }

        private static int GetNewestIndex()
        {
            return GetIndexFromNewest(0);
        }

        private static int GetIndexFromNewest(int age)
        {
            int index = _nextIndex - 1 - age;
            while (index < 0)
                index += WindowCapacity;

            return index;
        }

        private static float GetRatio(int count, int total)
        {
            return total > 0 ? (float)count / total : 0f;
        }

        private static AIMatchTelemetryTrendSnapshot NoData()
        {
            return new AIMatchTelemetryTrendSnapshot(
                AIValidationHealthStatus.NoData,
                "NoData",
                0,
                0,
                0,
                0,
                0,
                0,
                AIValidationHealthStatus.NoData,
                "NoData",
                "NoData",
                0f,
                0f);
        }
    }
}
