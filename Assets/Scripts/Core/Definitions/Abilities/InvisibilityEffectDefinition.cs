using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "InvisibilityEffect", menuName = "MOBA/Effects/Invisibility")]
    public sealed class InvisibilityEffectDefinition : AbilityEffectDefinition
    {
        [Header("Invisibility")]
        [Min(0.1f)]
        public float DurationSeconds = 6f;

        public override bool Apply(IAbilityUser source, BrawlerController target, AbilityExecutionContext context)
        {
            BrawlerController caster = source as BrawlerController;
            BrawlerController recipient = target != null ? target : caster;
            if (recipient == null || recipient.State == null)
                return false;

            IStatusEffectService statusEffectService = ServiceProvider.Get<IStatusEffectService>();
            if (statusEffectService == null)
                return false;

            statusEffectService.ApplyStatus(new StatusEffectContext
            {
                Source = caster,
                Target = recipient.State,
                Type = StatusEffectType.Invisibility,
                Duration = DurationSeconds,
                Magnitude = 1f,
                Origin = caster != null ? caster.Position : context.Origin,
                SourceToken = this
            });

            return true;
        }
    }
}
