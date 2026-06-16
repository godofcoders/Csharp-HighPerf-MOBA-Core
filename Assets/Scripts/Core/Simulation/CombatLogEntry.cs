using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public struct CombatLogEntry
    {
        public CombatLogEventType EventType;
        public uint Tick;

        public int SourceEntityId;
        public int TargetEntityId;

        public float Value;
        public DamageType DamageType;
        public StatusEffectType StatusEffectType;

        public bool IsFatal;
        public bool IsSuper;

        public static CombatLogEntry CreateDamage(uint tick, DamageResultContext result)
        {
            int sourceEntityId = result.Damage.Attacker != null ? result.Damage.Attacker.EntityID : 0;
            SpatialEntityUtility.TryGetEntityIdEvenIfDestroyed(result.Damage.Target, out int targetEntityId);

            return new CombatLogEntry
            {
                EventType = CombatLogEventType.Damage,
                Tick = tick,
                SourceEntityId = sourceEntityId,
                TargetEntityId = targetEntityId,
                Value = result.FinalDamageApplied,
                DamageType = result.Damage.Type,
                StatusEffectType = default,
                IsFatal = result.WasFatal,
                IsSuper = result.Damage.IsSuper
            };
        }

        public static CombatLogEntry CreateStatusApplied(uint tick, StatusEffectResult result)
        {
            int sourceEntityId = result.Context.Source != null ? result.Context.Source.EntityID : 0;
            int targetEntityId = result.Context.Target != null ? result.Context.Target.EntityID : 0;

            return new CombatLogEntry
            {
                EventType = CombatLogEventType.StatusApplied,
                Tick = tick,
                SourceEntityId = sourceEntityId,
                TargetEntityId = targetEntityId,
                Value = result.Context.Magnitude,
                DamageType = default,
                StatusEffectType = result.Context.Type,
                IsFatal = false,
                IsSuper = false
            };
        }

        public static CombatLogEntry CreateStatusRemoved(uint tick, StatusEffectResult result)
        {
            int sourceEntityId = result.Context.Source != null ? result.Context.Source.EntityID : 0;
            int targetEntityId = result.Context.Target != null ? result.Context.Target.EntityID : 0;

            return new CombatLogEntry
            {
                EventType = CombatLogEventType.StatusRemoved,
                Tick = tick,
                SourceEntityId = sourceEntityId,
                TargetEntityId = targetEntityId,
                Value = result.Context.Magnitude,
                DamageType = default,
                StatusEffectType = result.Context.Type,
                IsFatal = false,
                IsSuper = false
            };
        }

        public static CombatLogEntry CreateHeal(uint tick, int healerId, int targetId, float value)
        {
            return new CombatLogEntry
            {
                EventType = CombatLogEventType.Heal,
                Tick = tick,
                SourceEntityId = healerId,
                TargetEntityId = targetId,
                Value = value,
                DamageType = default,
                StatusEffectType = default,
                IsFatal = false,
                IsSuper = false
            };
        }

        public static CombatLogEntry CreateKill(uint tick, int killerId, int victimId)
        {
            return new CombatLogEntry
            {
                EventType = CombatLogEventType.Kill,
                Tick = tick,
                SourceEntityId = killerId,
                TargetEntityId = victimId,
                Value = 0f,
                DamageType = default,
                StatusEffectType = default,
                IsFatal = true,
                IsSuper = false
            };
        }

        public static CombatLogEntry CreateAssist(uint tick, int assisterId, int victimId)
        {
            return new CombatLogEntry
            {
                EventType = CombatLogEventType.Assist,
                Tick = tick,
                SourceEntityId = assisterId,
                TargetEntityId = victimId,
                Value = 0f,
                DamageType = default,
                StatusEffectType = default,
                IsFatal = false,
                IsSuper = false
            };
        }

        public static CombatLogEntry CreateBreakableDestroyed(uint tick, DamageResultContext result)
        {
            int sourceEntityId = result.Damage.Attacker != null ? result.Damage.Attacker.EntityID : 0;
            SpatialEntityUtility.TryGetEntityIdEvenIfDestroyed(result.Damage.Target, out int targetEntityId);

            return new CombatLogEntry
            {
                EventType = CombatLogEventType.BreakableDestroyed,
                Tick = tick,
                SourceEntityId = sourceEntityId,
                TargetEntityId = targetEntityId,
                Value = result.FinalDamageApplied,
                DamageType = result.Damage.Type,
                StatusEffectType = default,
                IsFatal = true,
                IsSuper = result.Damage.IsSuper
            };
        }
    }
}
