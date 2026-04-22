namespace MOBA.Core.Definitions
{
    // Coarse playstyle classification. Used as a UI-grouping / balance-tagging
    // label; actual behavioural tuning lives on AIProfile assets and on the
    // brawler's stat values. Values below are append-only — existing integer
    // values are serialised into every brawler asset, so renumbering would
    // silently re-categorise live data. Append new categories at the end.
    //
    // Name mapping vs. Brawl Stars' canonical categories:
    //   Sniper == Marksman (same playstyle, different label)
    //   Fighter == Damage Dealer (same playstyle, different label)
    //   Controller, Artillery match Brawl Stars exactly.
    public enum BrawlerArchetype
    {
        Tank = 0,
        Assassin = 1,
        Sniper = 2,
        Support = 3,
        Fighter = 4,
        Controller = 5,
        Artillery = 6
    }
}