namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Per-brawler stat snapshot for a single match. Updated live by
    /// MatchStatsTracker; copied into MatchResultBoard at match-end so
    /// the Results scene can display them after the Match scene unloads.
    ///
    /// Populated by MatchStatsTracker from damage, death, assist, and
    /// objective events. The Results screen snapshots this data before
    /// leaving the Match scene.
    /// </summary>
    public struct MatchStats
    {
        public int Kills;
        public int Assists;
        public int Deaths;
        public int GemsCollected;
        public int GoalsScored;
        public float DamageDealt;
        public float DamageTaken;
        public float HealingDone;

        /// <summary>Legacy scorer kept for existing call sites. MatchEndRouter
        /// applies the newer mode-aware star-player formula for Results.</summary>
        public float ComputeMvpScore()
        {
            return GemsCollected * 100f
                 + GoalsScored * 160f
                 + Kills * 50f
                 + Assists * 28f
                 + DamageDealt * 0.05f;
        }
    }
}
