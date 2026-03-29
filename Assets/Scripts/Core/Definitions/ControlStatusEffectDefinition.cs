using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "ControlStatusEffect", menuName = "MOBA/Effects/Control Status Effect")]
    public class ControlStatusEffectDefinition : AbilityEffectDefinition
    {
        [Header("Control Status")]
        public StatusEffectType StatusType = StatusEffectType.Silence;

        [Min(0f)]
        public float DurationSeconds = 2f;

        public override bool Apply(IAbilityUser source, BrawlerController target, AbilityExecutionContext context)
        {
            BrawlerController caster = source as BrawlerController;
            if (target == null || target.State == null)
                return false;

            StatusEffectContext statusContext = new StatusEffectContext
            {
                Source = caster,
                Target = target,
                Type = StatusType,
                Duration = DurationSeconds,
                Magnitude = 0f,
                Origin = caster != null ? caster.Position : context.Origin,
                SourceToken = this
            };

            IStatusEffectService statusEffectService = ServiceProvider.Get<IStatusEffectService>();
            if (statusEffectService == null)
                return false;

            statusEffectService.ApplyStatus(statusContext);
            return true;
        }
    }
}