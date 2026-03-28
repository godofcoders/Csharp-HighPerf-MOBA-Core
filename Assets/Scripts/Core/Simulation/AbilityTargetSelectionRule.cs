namespace MOBA.Core.Simulation
{
    public enum AbilityTargetSelectionRule
    {
        None = 0,
        ExplicitDirection = 1,
        Nearest = 2,
        LowestHealth = 3,
        HighestHealth = 4
    }
}