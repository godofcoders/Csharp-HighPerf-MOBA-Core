namespace MOBA.Core.Simulation
{
    public struct DamageResultContext
    {
        public DamageContext Damage;
        public bool WasFatal;
        public float FinalDamageApplied;
        public float ShieldAbsorbed;
    }
}