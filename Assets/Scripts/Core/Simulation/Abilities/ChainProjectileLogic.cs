using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public class ChainProjectileLogic : IAbilityLogic
    {
        private readonly float _damage;
        private readonly float _range;
        private readonly float _speed;
        private readonly int _bounceCount;
        private readonly float _bounceRadius;
        private readonly ProjectilePresentationProfile _presentationProfile;

        public ChainProjectileLogic(
            float damage,
            float range,
            float speed,
            int bounceCount,
            float bounceRadius,
            ProjectilePresentationProfile presentationProfile)
        {
            _damage = damage;
            _range = range;
            _speed = speed;
            _bounceCount = bounceCount;
            _bounceRadius = bounceRadius;
            _presentationProfile = presentationProfile;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user == null)
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);

            BrawlerController ownerBrawler = user as BrawlerController;

            var projectileService = ServiceProvider.Get<IProjectileService>();

            var spawnContext = new ProjectileSpawnContext
            {
                Owner = ownerBrawler,
                SourceAbility = context.AbilityDefinition,
                SlotType = context.SlotType,
                Origin = context.Origin,
                Direction = context.Direction,
                Speed = _speed,
                Range = _range,
                Damage = _damage,
                Team = user.Team,
                IsSuper = context.IsSuper,
                IsGadget = context.IsGadget,

                IsHybrid = false,
                AllyHealAmount = 0f,
                EnemyDamageAmount = 0f,
                HitTeamRule = ProjectileHitTeamRule.EnemiesOnly,

                DeliveryType = ProjectileDeliveryType.DirectHit,
                TargetPoint = Vector3.zero,

                HasHybridAoEImpact = false,
                ImpactRadius = 0f,
                ImpactEnemyDamage = 0f,
                ImpactAllyHeal = 0f,

                UseArcMotion = false,
                ArcHeight = 0f,
                TravelDistance = 0f,

                PresentationProfile = _presentationProfile,

                IsChainProjectile = true,
                RemainingBounces = _bounceCount,
                BounceRadius = _bounceRadius
            };

            projectileService.FireProjectile(spawnContext);

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.SpawnedProjectile = true;
            result.ProjectileCount = 1;
            result.ConsumedResource = true;
            return result;
        }

        public void Tick(uint currentTick) { }
    }
}