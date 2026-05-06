namespace MOBA.Core.Simulation
{
    /// <summary>
    /// Per-brawler stat snapshot for a single match. Updated live by
    /// MatchStatsTracker; copied into MatchResultBoard at match-end so
    /// the Results scene can display them after the Match scene unloads.
    ///
    /// Phase 1 actively populates Deaths + GemsCollected. Kills,
    /// DamageDealt, DamageTaken, HealingDone are reserved for when the
    /// damage/heal event hookups land — fields are present so the wire
    /// shape doesn't change later.
    /// </summary>
    public struct MatchStats
    {
        public int Kills;
        public int Deaths;
        public int GemsCollected;
        public float DamageDealt;
        public float DamageTaken;
        public float HealingDone;

        /// <summary>Simple "best player" score used to pick the MVP. Phase 1
        /// weights gems heavily (objective focus); kills count too once
        /// they're tracked. Easy to retune.</summary>
        public float ComputeMvpScore()
        {
            return GemsCollected * 100f
                 + Kills * 50f
                 + DamageDealt * 0.05f;
        }
    }
}
