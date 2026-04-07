using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class HybridProjectileLogic : IAbilityLogic
    {
        private readonly HybridProjectileAbilityDefinition _definition;

        public HybridProjectileLogic(HybridProjectileAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null || user == null)
                return AbilityExecutionResult.Failed(_definition, context.SlotType);

            if (!(user is BrawlerController brawler))
                return AbilityExecutionResult.Failed(_definition, context.SlotType);

            Vector3 direction = context.Direction.sqrMagnitude > 0.001f
                ? context.Direction.normalized
                : brawler.transform.forward;

            Vector3 origin = brawler.GetCastPosition();

            var projectileService = ServiceProvider.Get<IProjectileService>();

            var spawnContext = new ProjectileSpawnContext
            {
                Owner = brawler,
                SourceAbility = _definition,
                SlotType = context.SlotType,
                Origin = origin,
                Direction = direction,
                Speed = _definition.Speed,
                Range = _definition.Range,
                Damage = 0f,
                Team = brawler.Team,
                SuperChargeOnHit = 0.20f,
                IsSuper = context.IsSuper,
                IsGadget = context.IsGadget,

                IsHybrid = true,
                AllyHealAmount = _definition.AllyHeal,
                EnemyDamageAmount = _definition.EnemyDamage,
                HitTeamRule = ProjectileHitTeamRule.AlliesAndEnemies,
                PresentationProfile = _definition.PresentationProfile,
                IsChainProjectile = false,
                RemainingBounces = 0,
                BounceRadius = 0f,
            };

            projectileService.FireProjectile(spawnContext);

            return AbilityExecutionResult.Succeeded(_definition, context.SlotType);
        }

        public void Tick(uint currentTick)
        {
        }
    }
}