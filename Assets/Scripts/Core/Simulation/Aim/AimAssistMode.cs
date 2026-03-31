namespace MOBA.Core.Simulation
{
    public enum AimAssistMode
    {
        None = 0,

        NearestEnemy = 1,
        LowestHealthAlly = 2,
        NearestAlly = 3,

        ForwardOnly = 4,
        SelfCentered = 5,

        SmartOffense = 6,
        SmartSupport = 7
    }
}