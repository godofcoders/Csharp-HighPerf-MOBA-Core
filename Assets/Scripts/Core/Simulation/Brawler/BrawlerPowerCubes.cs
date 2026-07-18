namespace MOBA.Core.Simulation
{
    public sealed class BrawlerPowerCubes
    {
        public const float StatBonusPerCube = 0.05f;

        private readonly object _modifierSource = new object();

        public int Count { get; private set; }
        public object ModifierSource => _modifierSource;
        public float BonusMultiplier => Count * StatBonusPerCube;

        public bool Add(int amount)
        {
            if (amount <= 0)
                return false;

            Count += amount;
            return true;
        }

        public bool SetCount(int count)
        {
            int sanitized = count < 0 ? 0 : count;
            if (Count == sanitized)
                return false;

            Count = sanitized;
            return true;
        }

        public bool Clear()
        {
            if (Count <= 0)
                return false;

            Count = 0;
            return true;
        }
    }
}
