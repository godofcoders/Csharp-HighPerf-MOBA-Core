using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public interface IStatusEffectService
    {
        void ApplyStatus(in StatusEffectContext context);
    }

    public sealed class StatusEffectService : IStatusEffectService
    {
        public void ApplyStatus(in StatusEffectContext context)
        {
            if (context.Target == null || context.Target.State == null)
                return;

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            var state = context.Target.State;
            var effects = state.ActiveStatusEffects;

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].CanMerge(context))
                {
                    effects[i].Merge(context, currentTick);

                    var refreshResult = new StatusEffectResult
                    {
                        Context = context,
                        Applied = true,
                        Refreshed = true
                    };

                    StatusEffectEventBus.RaiseApplied(refreshResult);

                    var combatLog = ServiceProvider.Get<ICombatLogService>();
                    combatLog.AddEntry(CombatLogEntry.CreateStatusApplied(currentTick, refreshResult));
                    return;
                }
            }

            IStatusEffectInstance instance = CreateInstance(context, currentTick);
            if (instance == null)
                return;

            instance.Apply(state, currentTick);
            effects.Add(instance);

            var applyResult = new StatusEffectResult
            {
                Context = context,
                Applied = true,
                Refreshed = false
            };

            StatusEffectEventBus.RaiseApplied(applyResult);

            var logService = ServiceProvider.Get<ICombatLogService>();
            logService.AddEntry(CombatLogEntry.CreateStatusApplied(currentTick, applyResult));
        }

        private IStatusEffectInstance CreateInstance(StatusEffectContext context, uint currentTick)
        {
            uint durationTicks = (uint)(context.Duration * 30f);
            uint endTick = currentTick + durationTicks;

            switch (context.Type)
            {
                case StatusEffectType.Slow:
                    return new SlowEffect(context.Magnitude, context.Duration, context.SourceToken, currentTick);

                case StatusEffectType.Stun:
                    return new StunEffect(context.Duration, currentTick);

                case StatusEffectType.Burn:
                    return new BurnEffect(context.Source, context.Magnitude, context.Duration, currentTick);

                case StatusEffectType.Reveal:
                    return new RevealEffect(context.Duration, currentTick);

                case StatusEffectType.Silence:
                    return new SilenceStatusEffect(currentTick, endTick, context.SourceToken);

                case StatusEffectType.AttackLock:
                    return new AttackLockStatusEffect(currentTick, endTick, context.SourceToken);

                case StatusEffectType.GadgetLock:
                    return new GadgetLockStatusEffect(currentTick, endTick, context.SourceToken);

                case StatusEffectType.SuperLock:
                    return new SuperLockStatusEffect(currentTick, endTick, context.SourceToken);

                case StatusEffectType.MovementLock:
                    return new MovementLockStatusEffect(currentTick, endTick, context.SourceToken);
            }

            return null;
        }
    }
}