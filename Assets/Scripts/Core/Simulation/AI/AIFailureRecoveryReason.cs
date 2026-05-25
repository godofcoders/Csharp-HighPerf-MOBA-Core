namespace MOBA.Core.Simulation.AI
{
    public enum AIFailureRecoveryReason
    {
        None = 0,
        NavigationStall = 1,
        BlockedRoute = 2,
        StaleDestination = 3,
        FailedCast = 4
    }
}
