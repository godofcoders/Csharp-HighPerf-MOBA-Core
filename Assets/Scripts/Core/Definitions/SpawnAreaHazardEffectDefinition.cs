using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "SpawnAreaHazardEffect", menuName = "MOBA/Abilities/Effects/Spawn Area Hazard")]
    public class SpawnAreaHazardEffectDefinition : AbilityEffectDefinition
    {
        [SerializeField] private AreaHazardDefinition _hazardDefinition;

        public override bool Apply(IAbilityUser source, BrawlerController target, AbilityExecutionContext context)
        {
            if (_hazardDefinition == null || source is not BrawlerController owner)
                return false;

            var service = ServiceProvider.Get<IAreaHazardService>();
            if (service == null)
                return false;

            Vector3 spawnPosition = context.HasTargetPoint
                ? context.TargetPoint
                : (target != null ? target.Position : context.Origin);

            service.SpawnHazard(new AreaHazardSpawnRequest
            {
                Owner = owner,
                Team = owner.Team,
                Definition = _hazardDefinition,
                Position = spawnPosition,
                SourceAbility = context.AbilityDefinition,
                SlotType = context.SlotType,
                IsSuper = context.IsSuper
            });

            return true;
        }
    }
}