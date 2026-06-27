namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIReadinessReviewSnapshot
    {
        public readonly uint Tick;
        public readonly AIValidationHealthStatus Status;
        public readonly string PrimarySignal;
        public readonly AIValidationHealthStatus MatchStatus;
        public readonly string MatchSignal;
        public readonly AIValidationHealthStatus TrendStatus;
        public readonly string TrendSignal;
        public readonly AIValidationGauntletStatus GauntletStatus;
        public readonly string GauntletReason;
        public readonly int TeamDecisionCount;
        public readonly int UniqueActionCount;
        public readonly float ObjectiveDecisionRatio;
        public readonly float FailureRecoveryRatio;

        public AIReadinessReviewSnapshot(
            uint tick,
            AIValidationHealthStatus status,
            string primarySignal,
            AIValidationHealthStatus matchStatus,
            string matchSignal,
            AIValidationHealthStatus trendStatus,
            string trendSignal,
            AIValidationGauntletStatus gauntletStatus,
            string gauntletReason,
            int teamDecisionCount,
            int uniqueActionCount,
            float objectiveDecisionRatio,
            float failureRecoveryRatio)
        {
            Tick = tick;
            Status = status;
            PrimarySignal = string.IsNullOrEmpty(primarySignal)
                ? "Stable"
                : primarySignal;
            MatchStatus = matchStatus;
            MatchSignal = string.IsNullOrEmpty(matchSignal)
                ? "Stable"
                : matchSignal;
            TrendStatus = trendStatus;
            TrendSignal = string.IsNullOrEmpty(trendSignal)
                ? "Stable"
                : trendSignal;
            GauntletStatus = gauntletStatus;
            GauntletReason = string.IsNullOrEmpty(gauntletReason)
                ? "not_started"
                : gauntletReason;
            TeamDecisionCount = teamDecisionCount;
            UniqueActionCount = uniqueActionCount;
            ObjectiveDecisionRatio = objectiveDecisionRatio;
            FailureRecoveryRatio = failureRecoveryRatio;
        }

        public string GetDebugSummary()
        {
            if (Status == AIValidationHealthStatus.NoData)
                return "AIReady=NO_DATA signal=NoData";

            return
                $"AIReady={GetStatusLabel(Status)} " +
                $"signal={PrimarySignal} " +
                $"match={GetStatusLabel(MatchStatus)}:{MatchSignal} " +
                $"trend={GetStatusLabel(TrendStatus)}:{TrendSignal} " +
                $"gauntlet={GauntletStatus}:{GauntletReason} " +
                $"dec={TeamDecisionCount} " +
                $"unique={UniqueActionCount} " +
                $"obj={ObjectiveDecisionRatio:0%} " +
                $"recovery={FailureRecoveryRatio:0%}";
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

    public static class AIReadinessReview
    {
        private const int MinimumDiversityDecisionCount = 48;
        private const int MinimumHealthyUniqueActions = 3;

        public static AIReadinessReviewSnapshot Evaluate(
            uint currentTick,
            AIMatchTelemetryReviewSnapshot matchReview,
            AIMatchTelemetryTrendSnapshot trendReview,
            AIValidationGauntletResult gauntletResult)
        {
            bool hasMatchData = matchReview.Status != AIValidationHealthStatus.NoData;
            bool hasTrendData = trendReview.Status != AIValidationHealthStatus.NoData;
            bool hasGauntletData =
                gauntletResult.Status != AIValidationGauntletStatus.NotStarted &&
                gauntletResult.ScenarioType != AIValidationGauntletScenarioType.None;

            if (!hasMatchData && !hasTrendData && !hasGauntletData)
            {
                return new AIReadinessReviewSnapshot(
                    currentTick,
                    AIValidationHealthStatus.NoData,
                    "NoData",
                    matchReview.Status,
                    matchReview.PrimarySignal,
                    trendReview.Status,
                    trendReview.PrimarySignal,
                    gauntletResult.Status,
                    gauntletResult.Reason,
                    0,
                    0,
                    0f,
                    0f);
            }

            AIValidationHealthStatus status = AIValidationHealthStatus.Healthy;
            string signal = "Stable";

            RaiseForHealth(
                ref status,
                ref signal,
                matchReview.Status,
                $"Match:{matchReview.PrimarySignal}");

            RaiseForHealth(
                ref status,
                ref signal,
                trendReview.Status,
                $"Trend:{trendReview.PrimarySignal}");

            ApplyGauntletStatus(
                ref status,
                ref signal,
                gauntletResult);

            if (matchReview.TeamDecisionCount >= MinimumDiversityDecisionCount &&
                matchReview.UniqueActionCount < MinimumHealthyUniqueActions)
            {
                Raise(
                    ref status,
                    ref signal,
                    AIValidationHealthStatus.Watch,
                    "LowActionDiversity");
            }

            return new AIReadinessReviewSnapshot(
                currentTick,
                status,
                signal,
                matchReview.Status,
                matchReview.PrimarySignal,
                trendReview.Status,
                trendReview.PrimarySignal,
                gauntletResult.Status,
                gauntletResult.Reason,
                matchReview.TeamDecisionCount,
                matchReview.UniqueActionCount,
                matchReview.ObjectiveDecisionRatio,
                matchReview.FailureRecoveryRatio);
        }

        private static void ApplyGauntletStatus(
            ref AIValidationHealthStatus status,
            ref string signal,
            AIValidationGauntletResult gauntletResult)
        {
            switch (gauntletResult.Status)
            {
                case AIValidationGauntletStatus.Failed:
                    Raise(
                        ref status,
                        ref signal,
                        AIValidationHealthStatus.Fail,
                        $"Gauntlet:{gauntletResult.Reason}");
                    break;

                case AIValidationGauntletStatus.Watch:
                    Raise(
                        ref status,
                        ref signal,
                        AIValidationHealthStatus.Watch,
                        $"Gauntlet:{gauntletResult.Reason}");
                    break;

                case AIValidationGauntletStatus.Running:
                    Raise(
                        ref status,
                        ref signal,
                        AIValidationHealthStatus.Watch,
                        "Gauntlet:running");
                    break;
            }
        }

        private static void RaiseForHealth(
            ref AIValidationHealthStatus status,
            ref string signal,
            AIValidationHealthStatus candidateStatus,
            string candidateSignal)
        {
            if (candidateStatus == AIValidationHealthStatus.NoData ||
                candidateStatus == AIValidationHealthStatus.Healthy)
            {
                return;
            }

            Raise(ref status, ref signal, candidateStatus, candidateSignal);
        }

        private static void Raise(
            ref AIValidationHealthStatus status,
            ref string signal,
            AIValidationHealthStatus candidateStatus,
            string candidateSignal)
        {
            if (candidateStatus > status)
            {
                status = candidateStatus;
                signal = candidateSignal;
                return;
            }

            if (candidateStatus == status &&
                (string.IsNullOrEmpty(signal) || signal == "Stable"))
            {
                signal = candidateSignal;
            }
        }
    }
}
