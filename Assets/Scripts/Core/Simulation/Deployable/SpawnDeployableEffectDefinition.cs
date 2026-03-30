using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "SpawnDeployableEffect", menuName = "MOBA/Effects/Spawn Deployable Effect")]
    public class SpawnDeployableEffectDefinition : AbilityEffectDefinition
    {
        [Header("Deployable")]
        public DeployableDefinition Deployable;

        [Header("Placement")]
        public DeployableSpawnLocationRule SpawnLocationRule = DeployableSpawnLocationRule.InFrontOfCaster;
        public float ForwardOffset = 2f;

        public override bool Apply(IAbilityUser source, BrawlerController target, AbilityExecutionContext context)
        {
            BrawlerController caster = source as BrawlerController;
            if (caster == null || Deployable == null)
                return false;

            IDeployableService deployableService = ServiceProvider.Get<IDeployableService>();
            if (deployableService == null)
                return false;

            Vector3 spawnPosition = ResolveSpawnPosition(caster, context);
            Vector3 direction = context.Direction.sqrMagnitude > 0.001f
                ? context.Direction.normalized
                : caster.transform.forward;

            DeployableSpawnRequest request = new DeployableSpawnRequest
            {
                Owner = caster,
                Team = caster.Team,
                Definition = Deployable,
                Position = spawnPosition,
                Direction = direction
            };

            DeployableController spawned = deployableService.Spawn(request);
            return spawned != null;
        }

        private Vector3 ResolveSpawnPosition(BrawlerController caster, AbilityExecutionContext context)
        {
            switch (SpawnLocationRule)
            {
                case DeployableSpawnLocationRule.AtCaster:
                    return caster.Position;

                case DeployableSpawnLocationRule.AtTargetPoint:
                    return context.Origin;

                case DeployableSpawnLocationRule.InFrontOfCaster:
                default:
                    {
                        Vector3 forward = context.Direction.sqrMagnitude > 0.001f
                            ? context.Direction.normalized
                            : caster.transform.forward;

                        return caster.Position + (forward * ForwardOffset);
                    }
            }
        }
    }
}