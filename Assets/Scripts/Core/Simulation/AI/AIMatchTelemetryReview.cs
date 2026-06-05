namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIMatchTelemetryReviewLimits
    {
        public readonly int MaxMapResolves;
        public readonly int MaxPathQueries;
        public readonly int MaxPathTouchedNodes;

        public AIMatchTelemetryReviewLimits(
            int maxMapResolves,
            int maxPathQueries,
            int maxPathTouchedNodes)
        {
            MaxMapResolves = maxMapResolves <= 0 ? 1 : maxMapResolves;
            MaxPathQueries = maxPathQueries <= 0 ? 1 : maxPathQueries;
            MaxPathTouchedNodes = maxPathTouchedNodes <= 0 ? 1 : maxPathTouchedNodes;
        }
    }

    public struct AIMatchTelemetryReviewContext
    {
        public uint Tick;
        public AIReportCardSnapshot BlueTeam;
        public AIReportCardSnapshot RedTeam;
        public AIValidationHealthStatus HealthStatus;
        public string HealthSignal;
        public int WindowBotDecisionCount;
        public float InvalidDecisionRatio;
        public float ZeroScoreRatio;
        public float ActionSwitchRatio;
        public float LowConfidenceRatio;
        public AIActionType DominantActionType;
        public float DominantActionRatio;
        public int UniqueActionCount;
        public AIPerformanceSnapshot Performance;
        public AIMatchTelemetryReviewLimits Limits;
    }

    public readonly struct AIMatchTelemetryReviewSnapshot
    {
        public readonly uint Tick;
        public readonly AIValidationHealthStatus Status;
        public readonly string PrimarySignal;
        public readonly AIValidationHealthStatus HealthStatus;
        public readonly string HealthSignal;
        public readonly int RegisteredBotCount;
        public readonly int TeamDecisionCount;
        public readonly int WindowBotDecisionCount;
        public readonly int ObjectiveDecisionCount;
        public readonly int ObjectiveValue;
        public readonly int AbilityCastCount;
        public readonly int BadCastCount;
        public readonly int SuperCastCount;
        public readonly int WastedSuperCount;
        public readonly int FailureRecoveryCount;
        public readonly int IdleHesitationRecoveryCount;
        public readonly bool IsPerformanceOverBudget;
        public readonly float ObjectiveDecisionRatio;
        public readonly float BadCastRatio;
        public readonly float WastedSuperRatio;
        public readonly float FailureRecoveryRatio;
        public readonly float IdleHesitationRatio;
        public readonly float PathFailureRatio;
        public readonly float InvalidDecisionRatio;
        public readonly float ZeroScoreRatio;
        public readonly float ActionSwitchRatio;
        public readonly float LowConfidenceRatio;
        public readonly AIActionType DominantActionType;
        public readonly float DominantActionRatio;
        public readonly int UniqueActionCount;

        public AIMatchTelemetryReviewSnapshot(
            uint tick,
            AIValidationHealthStatus status,
            string primarySignal,
            AIValidationHealthStatus healthStatus,
            string healthSignal,
            int registeredBotCount,
            int teamDecisionCount,
            int windowBotDecisionCount,
            int objectiveDecisionCount,
            int objectiveValue,
            int abilityCastCount,
            int badCastCount,
            int superCastCount,
            int wastedSuperCount,
            int failureRecoveryCount,
            int idleHesitationRecoveryCount,
            bool isPerformanceOverBudget,
            float objectiveDecisionRatio,
            float badCastRatio,
            float wastedSuperRatio,
            float failureRecoveryRatio,
            float idleHesitationRatio,
            float pathFailureRatio,
            float invalidDecisionRatio,
            float zeroScoreRatio,
            float actionSwitchRatio,
            float lowConfidenceRatio,
            AIActionType dominantActionType,
            float dominantActionRatio,
            int uniqueActionCount)
        {
            Tick = tick;
            Status = status;
            PrimarySignal = string.IsNullOrEmpty(primarySignal)
                ? "Stable"
                : primarySignal;
            HealthStatus = healthStatus;
            HealthSignal = string.IsNullOrEmpty(healthSignal)
                ? "NoData"
                : healthSignal;
            RegisteredBotCount = registeredBotCount;
            TeamDecisionCount = teamDecisionCount;
            WindowBotDecisionCount = windowBotDecisionCount;
            ObjectiveDecisionCount = objectiveDecisionCount;
            ObjectiveValue = objectiveValue;
            AbilityCastCount = abilityCastCount;
            BadCastCount = badCastCount;
            SuperCastCount = superCastCount;
            WastedSuperCount = wastedSuperCount;
            FailureRecoveryCount = failureRecoveryCount;
            IdleHesitationRecoveryCount = idleHesitationRecoveryCount;
            IsPerformanceOverBudget = isPerformanceOverBudget;
            ObjectiveDecisionRatio = objectiveDecisionRatio;
            BadCastRatio = badCastRatio;
            WastedSuperRatio = wastedSuperRatio;
            FailureRecoveryRatio = failureRecoveryRatio;
            IdleHesitationRatio = idleHesitationRatio;
            PathFailureRatio = pathFailureRatio;
            InvalidDecisionRatio = invalidDecisionRatio;
            ZeroScoreRatio = zeroScoreRatio;
            ActionSwitchRatio = actionSwitchRatio;
            LowConfidenceRatio = lowConfidenceRatio;
            DominantActionType = dominantActionType;
            DominantActionRatio = dominantActionRatio;
            UniqueActionCount = uniqueActionCount;
        }

        public string GetDebugSummary()
        {
            return
                $"MatchReview={GetStatusLabel(Status)} " +
                $"signal={PrimarySignal} " +
                $"bots={RegisteredBotCount} " +
                $"dec={TeamDecisionCount}/{WindowBotDecisionCount} " +
                $"obj={ObjectiveDecisionCount}+{ObjectiveValue}({ObjectiveDecisionRatio:0%}) " +
                $"bad={BadCastCount}/{AbilityCastCount}({BadCastRatio:0%}) " +
                $"superWaste={WastedSuperCount}/{SuperCastCount}({WastedSuperRatio:0%}) " +
                $"rec={FailureRecoveryCount}({FailureRecoveryRatio:0%}) " +
                $"idle={IdleHesitationRecoveryCount}({IdleHesitationRatio:0%}) " +
                $"pathFail={PathFailureRatio:0%} " +
                $"perf={(IsPerformanceOverBudget ? "OVER" : "OK")} " +
                $"health={GetStatusLabel(HealthStatus)}:{HealthSignal} " +
                $"dom={DominantActionType}:{DominantActionRatio:0%} " +
                $"unique={UniqueActionCount}";
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

    public static class AIMatchTelemetryReview
    {
        private const int MinimumReviewDecisionCount = 24;
        private const int MinimumObjectiveReviewDecisionCount = 60;
        private const float MinimumObjectiveDecisionRatio = 0.03f;
        private const float MaxBadCastWatchRatio = 0.30f;
        private const float MaxBadCastFailRatio = 0.55f;
        private const float MaxWastedSuperWatchRatio = 0.35f;
        private const float MaxWastedSuperFailRatio = 0.60f;
        private const float MaxRecoveryWatchRatio = 0.18f;
        private const float MaxRecoveryFailRatio = 0.35f;
        private const float MaxIdleHesitationWatchRatio = 0.08f;
        private const float MaxIdleHesitationFailRatio = 0.18f;
        private const float MaxPathFailureWatchRatio = 0.25f;
        private const float MaxPathFailureFailRatio = 0.50f;

        public static AIMatchTelemetryReviewSnapshot Build(
            uint currentTick,
            AIMatchTelemetryReviewLimits limits)
        {
            return Evaluate(
                new AIMatchTelemetryReviewContext
                {
                    Tick = currentTick,
                    BlueTeam = AIReportCardTracker.GetTeamSnapshot(
                        TeamType.Blue,
                        currentTick),
                    RedTeam = AIReportCardTracker.GetTeamSnapshot(
                        TeamType.Red,
                        currentTick),
                    HealthStatus = AIValidationHealthTracker.Status,
                    HealthSignal = AIValidationHealthTracker.PrimarySignal,
                    WindowBotDecisionCount = AIValidationHealthTracker.BotDecisionCount,
                    InvalidDecisionRatio = AIValidationHealthTracker.InvalidDecisionRatio,
                    ZeroScoreRatio = AIValidationHealthTracker.ZeroScoreRatio,
                    ActionSwitchRatio = AIValidationHealthTracker.ActionSwitchRatio,
                    LowConfidenceRatio = AIValidationHealthTracker.LowConfidenceRatio,
                    DominantActionType = AIValidationHealthTracker.DominantActionType,
                    DominantActionRatio = AIValidationHealthTracker.DominantActionRatio,
                    UniqueActionCount = AIValidationHealthTracker.UniqueActionCount,
                    Performance = AIPerformanceTracker.GetSnapshot(currentTick),
                    Limits = limits
                });
        }

        public static AIMatchTelemetryReviewSnapshot Evaluate(
            AIMatchTelemetryReviewContext context)
        {
            int registeredBotCount =
                context.BlueTeam.RegisteredBotCount +
                context.RedTeam.RegisteredBotCount;
            int teamDecisionCount =
                context.BlueTeam.DecisionCount +
                context.RedTeam.DecisionCount;
            int objectiveDecisionCount =
                context.BlueTeam.ObjectiveDecisionCount +
                context.RedTeam.ObjectiveDecisionCount;
            int objectiveValue =
                context.BlueTeam.ObjectiveValue +
                context.RedTeam.ObjectiveValue;
            int abilityCastCount =
                context.BlueTeam.AbilityCastCount +
                context.RedTeam.AbilityCastCount;
            int badCastCount =
                context.BlueTeam.BadCastCount +
                context.RedTeam.BadCastCount;
            int superCastCount =
                context.BlueTeam.SuperCastCount +
                context.RedTeam.SuperCastCount;
            int wastedSuperCount =
                context.BlueTeam.WastedSuperCount +
                context.RedTeam.WastedSuperCount;
            int failureRecoveryCount =
                context.BlueTeam.FailureRecoveryCount +
                context.RedTeam.FailureRecoveryCount;
            int idleRecoveryCount =
                context.BlueTeam.IdleHesitationRecoveryCount +
                context.RedTeam.IdleHesitationRecoveryCount;

            float objectiveRatio = GetRatio(
                objectiveDecisionCount,
                teamDecisionCount);
            float badCastRatio = GetRatio(
                badCastCount,
                abilityCastCount);
            float wastedSuperRatio = GetRatio(
                wastedSuperCount,
                superCastCount);
            float recoveryRatio = GetRatio(
                failureRecoveryCount,
                teamDecisionCount);
            float idleRecoveryRatio = GetRatio(
                idleRecoveryCount,
                teamDecisionCount);
            float pathFailureRatio = context.Performance.PathFailureRatio;
            bool perfOverBudget = context.Performance.IsOverBudget(
                context.Limits.MaxMapResolves,
                context.Limits.MaxPathQueries,
                context.Limits.MaxPathTouchedNodes);

            AIValidationHealthStatus status =
                NormalizeInitialStatus(
                    context.HealthStatus,
                    teamDecisionCount,
                    context.WindowBotDecisionCount);
            string signal = ResolveInitialSignal(
                status,
                context.HealthSignal);

            RaiseForRatio(
                ref status,
                ref signal,
                context.WindowBotDecisionCount,
                1,
                context.InvalidDecisionRatio,
                0.0001f,
                0.0001f,
                "InvalidContext");
            RaiseForRatio(
                ref status,
                ref signal,
                context.WindowBotDecisionCount,
                MinimumReviewDecisionCount,
                context.ZeroScoreRatio,
                0.10f,
                0.18f,
                "ZeroScores");
            RaiseForRatio(
                ref status,
                ref signal,
                context.WindowBotDecisionCount,
                MinimumReviewDecisionCount,
                context.ActionSwitchRatio,
                0.65f,
                0.85f,
                "ActionFlicker");
            RaiseForRatio(
                ref status,
                ref signal,
                context.WindowBotDecisionCount,
                MinimumReviewDecisionCount,
                context.LowConfidenceRatio,
                0.70f,
                0.90f,
                "LowConfidence");

            if (perfOverBudget)
                Raise(ref status, ref signal, AIValidationHealthStatus.Fail, "PerfBudget");

            RaiseForRatio(
                ref status,
                ref signal,
                context.Performance.PathQueryCount,
                4,
                pathFailureRatio,
                MaxPathFailureWatchRatio,
                MaxPathFailureFailRatio,
                "PathFailures");
            RaiseForRatio(
                ref status,
                ref signal,
                abilityCastCount,
                6,
                badCastRatio,
                MaxBadCastWatchRatio,
                MaxBadCastFailRatio,
                "BadCasts");
            RaiseForRatio(
                ref status,
                ref signal,
                superCastCount,
                3,
                wastedSuperRatio,
                MaxWastedSuperWatchRatio,
                MaxWastedSuperFailRatio,
                "WastedSupers");
            RaiseForRatio(
                ref status,
                ref signal,
                teamDecisionCount,
                MinimumReviewDecisionCount,
                recoveryRatio,
                MaxRecoveryWatchRatio,
                MaxRecoveryFailRatio,
                "RecoveryPressure");
            RaiseForRatio(
                ref status,
                ref signal,
                teamDecisionCount,
                MinimumReviewDecisionCount,
                idleRecoveryRatio,
                MaxIdleHesitationWatchRatio,
                MaxIdleHesitationFailRatio,
                "IdleHesitation");

            if (teamDecisionCount >= MinimumObjectiveReviewDecisionCount &&
                objectiveValue <= 0 &&
                objectiveRatio < MinimumObjectiveDecisionRatio)
            {
                Raise(
                    ref status,
                    ref signal,
                    AIValidationHealthStatus.Watch,
                    "ObjectiveNeglect");
            }

            return new AIMatchTelemetryReviewSnapshot(
                context.Tick,
                status,
                signal,
                context.HealthStatus,
                context.HealthSignal,
                registeredBotCount,
                teamDecisionCount,
                context.WindowBotDecisionCount,
                objectiveDecisionCount,
                objectiveValue,
                abilityCastCount,
                badCastCount,
                superCastCount,
                wastedSuperCount,
                failureRecoveryCount,
                idleRecoveryCount,
                perfOverBudget,
                objectiveRatio,
                badCastRatio,
                wastedSuperRatio,
                recoveryRatio,
                idleRecoveryRatio,
                pathFailureRatio,
                context.InvalidDecisionRatio,
                context.ZeroScoreRatio,
                context.ActionSwitchRatio,
                context.LowConfidenceRatio,
                context.DominantActionType,
                context.DominantActionRatio,
                context.UniqueActionCount);
        }

        private static AIValidationHealthStatus NormalizeInitialStatus(
            AIValidationHealthStatus healthStatus,
            int teamDecisionCount,
            int windowBotDecisionCount)
        {
            if (teamDecisionCount <= 0 &&
                windowBotDecisionCount <= 0 &&
                healthStatus == AIValidationHealthStatus.NoData)
            {
                return AIValidationHealthStatus.NoData;
            }

            if (healthStatus == AIValidationHealthStatus.NoData)
                return AIValidationHealthStatus.Healthy;

            return healthStatus;
        }

        private static string ResolveInitialSignal(
            AIValidationHealthStatus status,
            string healthSignal)
        {
            if (status == AIValidationHealthStatus.NoData)
                return "NoData";

            if (!string.IsNullOrEmpty(healthSignal) &&
                healthSignal != "NoData")
            {
                return healthSignal;
            }

            return "Stable";
        }

        private static void RaiseForRatio(
            ref AIValidationHealthStatus status,
            ref string signal,
            int sampleCount,
            int minimumSampleCount,
            float ratio,
            float watchThreshold,
            float failThreshold,
            string reason)
        {
            if (sampleCount < minimumSampleCount)
                return;

            if (ratio > failThreshold)
            {
                Raise(ref status, ref signal, AIValidationHealthStatus.Fail, reason);
                return;
            }

            if (ratio > watchThreshold)
                Raise(ref status, ref signal, AIValidationHealthStatus.Watch, reason);
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
                (string.IsNullOrEmpty(signal) ||
                 signal == "Stable" ||
                 signal == "NoData"))
            {
                signal = candidateSignal;
            }
        }

        private static float GetRatio(int count, int total)
        {
            return total > 0 ? (float)count / total : 0f;
        }
    }
}
