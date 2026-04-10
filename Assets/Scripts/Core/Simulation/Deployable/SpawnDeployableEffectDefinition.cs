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
            if (source == null || Deployable == null)
                return false;

            IDeployableService deployableService = ServiceProvider.Get<IDeployableService>();
            if (deployableService == null)
                return false;

            BrawlerController owner = source as BrawlerController;
            if (owner == null)
                return false;

            Vector3 spawnPosition;

            if (context.HasTargetPoint)
            {
                spawnPosition = context.TargetPoint;
            }
            else
            {
                Vector3 fallbackDirection = context.Direction.sqrMagnitude > 0.001f
                    ? context.Direction.normalized
                    : owner.transform.forward;

                spawnPosition = context.Origin + fallbackDirection * 2f;
            }

            DeployableSpawnRequest request = new DeployableSpawnRequest
            {
                Owner = owner,
                Definition = Deployable,
                Position = spawnPosition,
                Team = owner.Team
            };

            deployableService.Spawn(request);
            return true;
        }
    }
}