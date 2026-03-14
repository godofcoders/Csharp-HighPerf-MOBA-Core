using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewStatStarPower", menuName = "MOBA/Star Power/Stat Boost")]
    public class StatBoostStarPower : StarPowerDefinition
    {
        public float SpeedMultiplier = 0.1f; // +10%
        public float DamageMultiplier = 0.05f; // +5%

        public override void Apply(BrawlerState state)
        {
            if (SpeedMultiplier != 0)
                state.MoveSpeed.AddModifier(new StatModifier(SpeedMultiplier, ModifierType.Multiplicative, this));

            if (DamageMultiplier != 0)
                state.Damage.AddModifier(new StatModifier(DamageMultiplier, ModifierType.Multiplicative, this));

            //Debug.Log($"[SIM] Star Power Applied: {AbilityName}");
        }
    }
}