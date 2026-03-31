using System.Collections.Generic;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public interface IStatusTarget
    {
        int EntityID { get; }
        TeamType Team { get; }
        bool IsDead { get; }
        bool CanReceiveStatusEffects();

        List<IStatusEffectInstance> ActiveStatusEffects { get; }

        void AddIncomingMovementModifier(MovementModifier modifier);
        void RemoveIncomingMovementModifiersFromSource(object source);

        void AddIncomingDamageModifier(DamageModifier modifier);
        void RemoveIncomingDamageModifiersFromSource(object source);

        bool HasStatus(StatusEffectType type);
    }
}