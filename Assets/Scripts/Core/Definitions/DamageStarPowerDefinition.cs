using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "DamageStarPower", menuName = "MOBA/Star Power/Damage Star Power")]
    public class DamageStarPowerDefinition : StarPowerDefinition
    {
        [Header("Damage Star Power")]
        [Tooltip("0.15 = +15% damage")]
        public float DamageMultiplier = 0.15f;

        private void OnValidate()
        {
            Category = PassiveCategory.StarPower;
            AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.StarPower };
        }

        public override void Install(PassiveInstallContext context)
        {
            if (context.State == null)
                return;

            context.State.Damage.AddModifier(
                new StatModifier(DamageMultiplier, ModifierType.Multiplicative, context.SourceToken));
        }

        public override void Uninstall(PassiveInstallContext context)
        {
            base.Uninstall(context);
        }
    }
}