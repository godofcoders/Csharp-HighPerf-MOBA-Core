using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "HealthGear", menuName = "MOBA/Gears/Health Gear")]
    public class HealthGearDefinition : GearDefinition
    {
        [Header("Health Gear")]
        public float BonusHealth = 300f;

        public override void Install(PassiveInstallContext context)
        {
            if (context.State == null)
                return;

            context.State.MaxHealth.AddModifier(
                new StatModifier(BonusHealth, ModifierType.Additive, context.SourceToken));
        }

        public override void Uninstall(PassiveInstallContext context)
        {
            base.Uninstall(context);
        }
    }
}