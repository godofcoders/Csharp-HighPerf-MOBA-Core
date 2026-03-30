using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public sealed class DeployableAbilityUser : IAbilityUser
    {
        private readonly DeployableController _controller;

        public DeployableAbilityUser(DeployableController controller)
        {
            _controller = controller;
        }

        public TeamType Team => _controller.Team;

        public Vector3 Position => _controller.Position;

        public BrawlerState State => _controller.Owner != null ? _controller.Owner.State : null;

        public void FireProjectile(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float range,
            float damage,
            AbilityDefinition sourceAbility,
            AbilitySlotType slotType,
            bool isSuper,
            bool isGadget)
        {
            var projectileService = ServiceProvider.Get<IProjectileService>();
            if (projectileService == null)
                return;

            ProjectileSpawnContext spawnContext = new ProjectileSpawnContext
            {
                Owner = _controller.Owner,
                SourceAbility = sourceAbility,
                SlotType = slotType,
                Origin = origin,
                Direction = direction,
                Speed = speed,
                Range = range,
                Damage = damage,
                Team = _controller.Team,
                SuperChargeOnHit = 0f,
                IsSuper = isSuper,
                IsGadget = isGadget
            };

            projectileService.FireProjectile(spawnContext);
        }
    }
}