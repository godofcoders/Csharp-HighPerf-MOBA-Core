namespace MOBA.Core.Infrastructure
{
    public static class QuestCatalog
    {
        private static readonly QuestDefinition[] AllQuests =
        {
            new QuestDefinition(
                "barley_win_5",
                "Barley Specialist",
                "Win 5 matches as Barley.",
                "Brawler Mastery",
                QuestMetricType.MatchesWon,
                5,
                "barley"),

            new QuestDefinition(
                "piper_win_5",
                "Piper Specialist",
                "Win 5 matches as Piper.",
                "Brawler Mastery",
                QuestMetricType.MatchesWon,
                5,
                "piper"),

            new QuestDefinition(
                "colt_damage_25000",
                "Colt Pressure",
                "Deal 25,000 damage as Colt.",
                "Combat",
                QuestMetricType.DamageDealt,
                25000,
                "colt"),

            new QuestDefinition(
                "piper_damage_20000",
                "Sniper Discipline",
                "Deal 20,000 damage as Piper.",
                "Combat",
                QuestMetricType.DamageDealt,
                20000,
                "piper"),

            new QuestDefinition(
                "brawlball_score_10",
                "Striker",
                "Score 10 goals in Brawl Ball.",
                "Mode Objective",
                QuestMetricType.GoalsScored,
                10,
                "",
                true,
                GameModeId.BrawlBall),

            new QuestDefinition(
                "gemgrab_collect_30",
                "Gem Runner",
                "Collect 30 gems in Gem Grab.",
                "Mode Objective",
                QuestMetricType.GemsCollected,
                30,
                "",
                true,
                GameModeId.GemGrab),

            new QuestDefinition(
                "knockout_win_3",
                "Knockout Closer",
                "Win 3 Knockout matches.",
                "Mode Objective",
                QuestMetricType.MatchesWon,
                3,
                "",
                true,
                GameModeId.Knockout),

            new QuestDefinition(
                "eliminations_25",
                "Finisher",
                "Get 25 eliminations.",
                "Combat",
                QuestMetricType.Kills,
                25)
        };

        public static QuestDefinition[] All => AllQuests;
    }
}
