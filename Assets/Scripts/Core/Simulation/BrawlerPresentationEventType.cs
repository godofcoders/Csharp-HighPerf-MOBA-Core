namespace MOBA.Core.Simulation
{
    public enum BrawlerPresentationEventType
    {
        None = 0,

        MainAttackStarted = 1,
        MainAttackSucceeded = 2,
        MainAttackFailed = 3,

        GadgetStarted = 10,
        GadgetSucceeded = 11,
        GadgetFailed = 12,

        SuperStarted = 20,
        SuperSucceeded = 21,
        SuperFailed = 22,

        HyperchargeStarted = 30,
        HyperchargeEnded = 31,

        DamageTaken = 40,
        Healed = 41,
        Died = 42
    }
}