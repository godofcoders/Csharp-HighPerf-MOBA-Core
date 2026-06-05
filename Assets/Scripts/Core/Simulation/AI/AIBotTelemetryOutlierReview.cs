using System.Collections.Generic;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIBotTelemetryOutlierSnapshot
    {
        public readonly AIValidationHealthStatus Status;
        public readonly int CandidateCount;
        public readonly int EntityId;
        public readonly string Name;
        public readonly TeamType Team;
        public readonly float Score;
        public readonly string Reason;
        public readonly int DecisionCount;
        public readonly float BadCastRatio;
        public readonly float WastedSuperRatio;
        public readonly float RecoveryRatio;
        public readonly float IdleHesitationRatio;
        public readonly float ZeroScoreRatio;
        public readonly float LowConfidenceRatio;
        public readonly float ActionSwitchRatio;
        public readonly float CombatUsefulness;

        public AIBotTelemetryOutlierSnapshot(
            AIValidationHealthStatus status,
            int candidateCount,
            int entityId,
            string name,
            TeamType team,
            float score,
            string reason,
            int decisionCount,
            float badCastRatio,
            float wastedSuperRatio,
            float recoveryRatio,
            float idleHesitationRatio,
            float zeroScoreRatio,
            float lowConfidenceRatio,
            float actionSwitchRatio,
            float combatUsefulness)
        {
            Status = status;
            CandidateCount = candidateCount;
            EntityId = entityId;
            Name = string.IsNullOrEmpty(name) ? $"Bot {entityId}" : name;
            Team = team;
            Score = score;
            Reason = string.IsNullOrEmpty(reason) ? "Stable" : reason;
            DecisionCount = decisionCount;
            BadCastRatio = badCastRatio;
            WastedSuperRatio = wastedSuperRatio;
            RecoveryRatio = recoveryRatio;
            IdleHesitationRatio = idleHesitationRatio;
            ZeroScoreRatio = zeroScoreRatio;
            LowConfidenceRatio = lowConfidenceRatio;
            ActionSwitchRatio = actionSwitchRatio;
            CombatUsefulness = combatUsefulness;
        }

        public string GetDebugSummary()
        {
            if (Status == AIValidationHealthStatus.NoData)
                return "BotOutlier=NO_DATA bots=0";

            if (Status == AIValidationHealthStatus.Healthy)
                return $"BotOutlier=None bots={CandidateCount}";

            return
                $"BotOutlier={GetStatusLabel(Status)} " +
                $"bot={EntityId} " +
                $"team={Team} " +
                $"name={Name} " +
                $"score={Score:0.0} " +
                $"reason={Reason} " +
                $"dec={DecisionCount} " +
                $"bad={BadCastRatio:0%} " +
                $"waste={WastedSuperRatio:0%} " +
                $"rec={RecoveryRatio:0%} " +
                $"idle={IdleHesitationRatio:0%} " +
                $"zero={ZeroScoreRatio:0%} " +
                $"low={LowConfidenceRatio:0%} " +
                $"switch={ActionSwitchRatio:0%} " +
                $"useful={CombatUsefulness:0}";
        }

        private static string GetStatusLabel(AIValidationHealthStatus status)
        {
            switch (status)
            {
                case AIValidationHealthStatus.Fail:
                    return "FAIL";
                case AIValidationHealthStatus.Watch:
                    return "WATCH";
                case AIValidationHealthStatus.Healthy:
                    return "OK";
                default:
                    return "NO_DATA";
            }
        }
    }

    public static class AIBotTelemetryOutlierReview
    {
        private const int MinimumDecisionCount = 12;
        private const int MinimumObjectiveDecisionCount = 50;
        private const int MinimumAbilityCastCount = 6;
        private const int MinimumSuperCastCount = 3;
        private const float WatchScore = 35f;
        private const float FailScore = 80f;
        private static readonly List<AIReportCardSnapshot> _snapshotBuffer =
            new List<AIReportCardSnapshot>(16);

        public static AIBotTelemetryOutlierSnapshot Build(uint currentTick)
        {
            AIReportCardTracker.GetBotSnapshots(_snapshotBuffer, currentTick);
            return Evaluate(_snapshotBuffer);
        }

        public static AIBotTelemetryOutlierSnapshot Evaluate(
            IReadOnlyList<AIReportCardSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
                return NoData();

            int candidateCount = 0;
            AIReportCardSnapshot bestSnapshot = default;
            BotScore bestScore = default;

            for (int i = 0; i < snapshots.Count; i++)
            {
                AIReportCardSnapshot snapshot = snapshots[i];
                if (snapshot.IsTeamSnapshot)
                    continue;

                candidateCount++;
                BotScore score = Score(snapshot);
                if (score.Value > bestScore.Value)
                {
                    bestScore = score;
                    bestSnapshot = snapshot;
                }
            }

            if (candidateCount == 0)
                return NoData();

            if (bestScore.Value < WatchScore)
            {
                return new AIBotTelemetryOutlierSnapshot(
                    AIValidationHealthStatus.Healthy,
                    candidateCount,
                    0,
                    "None",
                    TeamType.Neutral,
                    0f,
                    "Stable",
                    0,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f);
            }

            AIValidationHealthStatus status = bestScore.Value >= FailScore
                ? AIValidationHealthStatus.Fail
                : AIValidationHealthStatus.Watch;

            return new AIBotTelemetryOutlierSnapshot(
                status,
                candidateCount,
                bestSnapshot.EntityId,
                bestSnapshot.Name,
                bestSnapshot.Team,
                bestScore.Value,
                bestScore.Reason,
                bestSnapshot.DecisionCount,
                bestSnapshot.BadCastRatio,
                bestSnapshot.WastedSuperRatio,
                GetRatio(bestSnapshot.FailureRecoveryCount, bestSnapshot.DecisionCount),
                GetRatio(bestSnapshot.IdleHesitationRecoveryCount, bestSnapshot.DecisionCount),
                GetRatio(bestSnapshot.ZeroScoreDecisionCount, bestSnapshot.DecisionCount),
                GetRatio(bestSnapshot.LowConfidenceDecisionCount, bestSnapshot.DecisionCount),
                GetRatio(bestSnapshot.ActionSwitchCount, bestSnapshot.DecisionCount),
                bestSnapshot.CombatUsefulness);
        }

        private static AIBotTelemetryOutlierSnapshot NoData()
        {
            return new AIBotTelemetryOutlierSnapshot(
                AIValidationHealthStatus.NoData,
                0,
                0,
                "None",
                TeamType.Neutral,
                0f,
                "NoData",
                0,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f);
        }

        private static BotScore Score(AIReportCardSnapshot snapshot)
        {
            BotScore score = new BotScore(0f, "Stable");
            int decisions = snapshot.DecisionCount;

            if (snapshot.InvalidDecisionCount > 0)
                score.Raise(95f + snapshot.InvalidDecisionCount * 2f, "InvalidContext");

            if (decisions >= MinimumDecisionCount)
            {
                ScoreRatio(
                    ref score,
                    snapshot.ZeroScoreDecisionCount,
                    decisions,
                    0.10f,
                    0.18f,
                    55f,
                    85f,
                    "ZeroScores");
                ScoreRatio(
                    ref score,
                    snapshot.ActionSwitchCount,
                    decisions,
                    0.65f,
                    0.85f,
                    50f,
                    80f,
                    "ActionFlicker");
                ScoreRatio(
                    ref score,
                    snapshot.LowConfidenceDecisionCount,
                    decisions,
                    0.70f,
                    0.90f,
                    45f,
                    70f,
                    "LowConfidence");
                ScoreRatio(
                    ref score,
                    snapshot.FailureRecoveryCount,
                    decisions,
                    0.18f,
                    0.35f,
                    60f,
                    85f,
                    "RecoveryPressure");
                ScoreRatio(
                    ref score,
                    snapshot.IdleHesitationRecoveryCount,
                    decisions,
                    0.08f,
                    0.18f,
                    65f,
                    85f,
                    "IdleHesitation");
            }

            if (snapshot.AbilityCastCount >= MinimumAbilityCastCount)
            {
                ScoreRatio(
                    ref score,
                    snapshot.BadCastCount,
                    snapshot.AbilityCastCount,
                    0.30f,
                    0.55f,
                    60f,
                    90f,
                    "BadCasts");
            }

            if (snapshot.SuperCastCount >= MinimumSuperCastCount)
            {
                ScoreRatio(
                    ref score,
                    snapshot.WastedSuperCount,
                    snapshot.SuperCastCount,
                    0.35f,
                    0.60f,
                    55f,
                    80f,
                    "WastedSupers");
            }

            if (decisions >= MinimumObjectiveDecisionCount &&
                snapshot.ObjectiveDecisionCount == 0 &&
                snapshot.ObjectiveValue <= 0)
            {
                score.Raise(38f, "ObjectiveNeglect");
            }

            if (decisions >= MinimumDecisionCount * 2)
            {
                if (snapshot.CombatUsefulness < -2500f)
                    score.Raise(70f, "TakingPressure");
                else if (snapshot.CombatUsefulness < -1000f)
                    score.Raise(45f, "TakingPressure");
            }

            return score.Clamp();
        }

        private static void ScoreRatio(
            ref BotScore score,
            int count,
            int total,
            float watchRatio,
            float failRatio,
            float watchScore,
            float failScore,
            string reason)
        {
            float ratio = GetRatio(count, total);
            if (ratio > failRatio)
            {
                score.Raise(failScore, reason);
                return;
            }

            if (ratio > watchRatio)
                score.Raise(watchScore, reason);
        }

        private static float GetRatio(int count, int total)
        {
            return total > 0 ? (float)count / total : 0f;
        }

        private struct BotScore
        {
            public float Value;
            public string Reason;

            public BotScore(float value, string reason)
            {
                Value = value;
                Reason = reason;
            }

            public void Raise(float value, string reason)
            {
                if (value <= Value)
                    return;

                Value = value;
                Reason = reason;
            }

            public BotScore Clamp()
            {
                if (Value > 100f)
                    return new BotScore(100f, Reason);

                return this;
            }
        }
    }
}
