namespace MOBA.Core.Simulation
{
    public static class BrawlerHealthRegenUtility
    {
        public const float DefaultDelaySeconds = 3f;
        public const float DefaultMaxHealthPerSecond = 0.13f;

        public static bool CanRegenerate(
            uint currentTick,
            uint lastDamageTick,
            uint lastAttackTick,
            uint delayTicks,
            float currentHealth,
            float maxHealth,
            bool isDead)
        {
            if (isDead || maxHealth <= 0f || currentHealth >= maxHealth)
                return false;

            uint latestCombatTick = lastDamageTick > lastAttackTick
                ? lastDamageTick
                : lastAttackTick;

            return currentTick >= latestCombatTick &&
                   currentTick - latestCombatTick >= delayTicks;
        }

        public static float CalculateHealAmount(
            float maxHealth,
            float deltaTime,
            float maxHealthPerSecond)
        {
            if (maxHealth <= 0f || deltaTime <= 0f || maxHealthPerSecond <= 0f)
                return 0f;

            return maxHealth * maxHealthPerSecond * deltaTime;
        }
    }
}
