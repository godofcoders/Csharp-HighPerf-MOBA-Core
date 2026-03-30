using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class DeployableState
    {
        public DeployableDefinition Definition { get; private set; }
        public BrawlerController Owner { get; private set; }
        public TeamType Team { get; private set; }

        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }

        public uint SpawnTick { get; private set; }
        public uint ExpiryTick { get; private set; }

        public bool IsDead => CurrentHealth <= 0f;

        public DeployableState(
            DeployableDefinition definition,
            BrawlerController owner,
            TeamType team,
            uint spawnTick)
        {
            Definition = definition;
            Owner = owner;
            Team = team;
            SpawnTick = spawnTick;

            MaxHealth = definition != null ? definition.MaxHealth : 0f;
            CurrentHealth = MaxHealth;

            float lifetimeSeconds = definition != null ? definition.LifetimeSeconds : 0f;
            ExpiryTick = spawnTick + (uint)(lifetimeSeconds * 30f);
        }

        public bool IsExpired(uint currentTick)
        {
            return currentTick >= ExpiryTick;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            CurrentHealth -= amount;
            if (CurrentHealth < 0f)
                CurrentHealth = 0f;
        }
    }
}