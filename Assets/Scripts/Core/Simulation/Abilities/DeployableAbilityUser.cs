using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class DeployableAbilityUser : IAbilityUser
    {
        private readonly DeployableController _owner;

        public DeployableAbilityUser(DeployableController owner)
        {
            _owner = owner;
        }

        public TeamType Team => _owner != null ? _owner.Team : TeamType.Blue;
        public Vector3 Position => _owner != null ? _owner.Position : Vector3.zero;

        public BrawlerController ResolveTarget(
            AbilityTargetTeamRule teamRule,
            AbilityTargetSelectionRule selectionRule,
            float range,
            bool includeSelf = false,
            bool requireAlive = true)
        {
            if (_owner == null)
                return null;

            AbilityTargetRequest request = new AbilityTargetRequest
            {
                Source = _owner.Owner,
                Origin = _owner.Position,
                Direction = _owner.transform.forward,
                Range = range,
                TeamRule = teamRule,
                SelectionRule = selectionRule,
                CountRule = AbilityTargetCountRule.Single,
                IncludeSelf = includeSelf,
                RequireAlive = requireAlive
            };

            return AbilityTargetResolver.ResolveSingleTarget(request);
        }

        public void ResolveTargets(
            AbilityTargetTeamRule teamRule,
            AbilityTargetSelectionRule selectionRule,
            float range,
            List<BrawlerController> results,
            bool includeSelf = false,
            bool requireAlive = true)
        {
            if (_owner == null || results == null)
                return;

            AbilityTargetRequest request = new AbilityTargetRequest
            {
                Source = _owner.Owner,
                Origin = _owner.Position,
                Direction = _owner.transform.forward,
                Range = range,
                TeamRule = teamRule,
                SelectionRule = selectionRule,
                CountRule = AbilityTargetCountRule.Multiple,
                IncludeSelf = includeSelf,
                RequireAlive = requireAlive
            };

            AbilityTargetResolver.ResolveTargets(request, results);
        }

        public void FireProjectile(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float range,
            float damage,
            AbilityDefinition sourceAbility,
            AbilitySlotType slotType,
            bool isSuper,
            bool isGadget,
            ProjectilePresentationProfile presentationProfile = null)
        {
            if (_owner == null)
                return;

            var projectileService = ServiceProvider.Get<IProjectileService>();

            var spawnContext = new ProjectileSpawnContext
            {
                Owner = _owner.Owner,
                SourceAbility = sourceAbility,
                SlotType = slotType,
                Origin = origin,
                Direction = direction,
                Speed = speed,
                Range = range,
                Damage = damage,
                Team = Team,
                IsSuper = isSuper,
                IsGadget = isGadget,

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

                PresentationProfile = presentationProfile,
                IsChainProjectile = false,
                RemainingBounces = 0,
                BounceRadius = 0f,
            };

            projectileService.FireProjectile(spawnContext);
        }
    }
}