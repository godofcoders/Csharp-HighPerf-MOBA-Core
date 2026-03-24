using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "MoveSpeedGear", menuName = "MOBA/Gears/Move Speed Gear")]
    public class MoveSpeedGearDefinition : GearDefinition
    {
        [Header("Move Speed Gear")]
        [Tooltip("0.10 = +10% move speed")]
        public float SpeedMultiplier = 0.10f;

        public override void Install(PassiveInstallContext context)
        {
            if (context.State == null)
                return;

            context.State.MoveSpeed.AddModifier(
                new StatModifier(SpeedMultiplier, ModifierType.Multiplicative, context.SourceToken));
        }

        public override void Uninstall(PassiveInstallContext context)
        {
            base.Uninstall(context);
        }
    }
}