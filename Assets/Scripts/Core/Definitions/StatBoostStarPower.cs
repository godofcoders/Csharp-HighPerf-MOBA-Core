using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewStatStarPower", menuName = "MOBA/Star Power/Stat Boost")]
    public class StatBoostStarPower : StarPowerDefinition
    {
        public float BonusHealth;
        public float SpeedMultiplier = 0.1f;
        public float DamageMultiplier = 0.05f;

        private void OnValidate()
        {
            Category = PassiveCategory.StarPower;
            AllowedSlotTypes = new[] { PassiveSlotType.StarPower };
        }

        public override void Install(PassiveInstallContext context)
        {
            BrawlerState state = context.State;
            object source = context.SourceToken;

            if (state == null)
                return;

            if (BonusHealth != 0f)
                state.MaxHealth.AddModifier(new StatModifier(BonusHealth, ModifierType.Additive, source));

            if (SpeedMultiplier != 0f)
                state.MoveSpeed.AddModifier(new StatModifier(SpeedMultiplier, ModifierType.Multiplicative, source));

            if (DamageMultiplier != 0f)
                state.Damage.AddModifier(new StatModifier(DamageMultiplier, ModifierType.Multiplicative, source));
        }

        public override void Uninstall(PassiveInstallContext context)
        {
            base.Uninstall(context);
        }
    }
}