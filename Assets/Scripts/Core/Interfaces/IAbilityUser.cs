using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public interface IAbilityUser
    {
        TeamType Team { get; }
        Vector3 Position { get; }

        BrawlerController ResolveTarget(
            AbilityTargetTeamRule teamRule,
            AbilityTargetSelectionRule selectionRule,
            float range,
            bool includeSelf = false,
            bool requireAlive = true
        );

        void ResolveTargets(
            AbilityTargetTeamRule teamRule,
            AbilityTargetSelectionRule selectionRule,
            float range,
            System.Collections.Generic.List<BrawlerController> results,
            bool includeSelf = false,
            bool requireAlive = true
        );

        void FireProjectile(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float range,
            float damage,
            AbilityDefinition sourceAbility,
            AbilitySlotType slotType,
            bool isSuper,
            bool isGadget,
            ProjectilePresentationProfile presentationProfile = null
        );
    }
}