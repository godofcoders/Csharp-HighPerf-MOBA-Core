namespace MOBA.Core.Simulation
{
    public struct DamageResultContext
    {
        public DamageContext Damage;
        public bool WasFatal;
        public float FinalDamageApplied;
        public float ShieldAbsorbed;
        // Actual brawler health removed, clamped for overkill; presentation can
        // confirm health hits without changing FinalDamageApplied's existing semantics.
        public float HealthDamageApplied;
    }
}