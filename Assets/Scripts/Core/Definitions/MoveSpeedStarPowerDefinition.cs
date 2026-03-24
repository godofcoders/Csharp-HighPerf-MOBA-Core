using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "MoveSpeedStarPower", menuName = "MOBA/Star Power/Move Speed Star Power")]
    public class MoveSpeedStarPowerDefinition : StarPowerDefinition
    {
        public float SpeedMultiplier = 0.12f;

        private void OnValidate()
        {
            Category = PassiveCategory.StarPower;
            AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.StarPower };
        }

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