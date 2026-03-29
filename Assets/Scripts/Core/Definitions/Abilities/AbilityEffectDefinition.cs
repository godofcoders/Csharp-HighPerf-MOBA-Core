using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    public abstract class AbilityEffectDefinition : ScriptableObject
    {
        [Header("Effect Identity")]
        public string EffectName;

        public abstract bool Apply(IAbilityUser source, BrawlerController target, AbilityExecutionContext context);
    }
}