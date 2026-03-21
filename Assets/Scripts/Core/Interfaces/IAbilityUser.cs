using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public interface IAbilityUser
    {
        BrawlerState State { get; }

        void FireProjectile(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float range,
            float damage,
            AbilityDefinition sourceAbility,
            AbilitySlotType slotType,
            bool isSuper,
            bool isGadget
        );
    }
}