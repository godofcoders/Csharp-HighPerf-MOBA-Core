using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public static class QuestProgressionService
    {
        public static int ApplyMatchReport(QuestMatchReport report)
        {
            QuestDefinition[] quests = QuestCatalog.All;
            int changed = 0;

            for (int i = 0; i < quests.Length; i++)
            {
                QuestDefinition quest = quests[i];
                if (quest == null ||
                    !quest.MatchesMode(report.Mode) ||
                    !quest.MatchesBrawler(report.BrawlerDefinition, report.BrawlerName))
                {
                    continue;
                }

                int amount = ResolveMetricDelta(quest.Metric, report);
                if (amount > 0 && PlayerQuestProgress.AddProgress(quest, amount))
                    changed++;
            }

            return changed;
        }

        private static int ResolveMetricDelta(
            QuestMetricType metric,
            QuestMatchReport report)
        {
            MatchStats stats = report.Stats;
            switch (metric)
            {
                case QuestMetricType.MatchesPlayed:
                    return 1;

                case QuestMetricType.MatchesWon:
                    return report.WonMatch ? 1 : 0;

                case QuestMetricType.DamageDealt:
                    return Mathf.Max(0, Mathf.RoundToInt(stats.DamageDealt));

                case QuestMetricType.Kills:
                    return Mathf.Max(0, stats.Kills);

                case QuestMetricType.Assists:
                    return Mathf.Max(0, stats.Assists);

                case QuestMetricType.GemsCollected:
                    return Mathf.Max(0, stats.GemsCollected);

                case QuestMetricType.GoalsScored:
                    return Mathf.Max(0, stats.GoalsScored);

                default:
                    return 0;
            }
        }
    }
}
