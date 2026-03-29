namespace MOBA.Core.Simulation
{
    public enum BrawlerActionBlockReason
    {
        None = 0,
        MissingDefinition = 1,
        Dead = 2,
        ActionLocked = 3,
        AbilityCooldown = 4,
        NoAmmo = 5,
        NoGadgetCharges = 6,
        SuperNotReady = 7,
        HyperchargeNotReady = 8,
        Silenced = 9,
        AttackLocked = 10,
        GadgetLocked = 11,
        SuperLocked = 12,
        MovementLocked = 13
    }
}