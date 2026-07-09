using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Abilities;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "MeleeConeAbility", menuName = "MOBA/Abilities/Melee Cone")]
    public class MeleeConeAbilityDefinition : AbilityDefinition
    {
        [Header("Melee Cone")]
        public float Damage = 1500f;
        public float Range = 2.35f;
        [Range(10f, 180f)]
        public float ArcDegrees = 95f;
        [Tooltip("Extra distance added when checking target collision radius near the cone edge.")]
        public float HitRadiusPadding = 0.2f;

        private void OnValidate()
        {
            SlotType = AbilitySlotType.MainAttack;
            TargetingType = AbilityTargetingType.Directional;
            DeliveryType = AbilityDeliveryType.Instant;

            if (PreviewMode == AimPreviewMode.None)
                PreviewMode = AimPreviewMode.Directional;
        }

        public override IAbilityLogic CreateLogic()
        {
            return new MeleeConeAbilityLogic(this);
        }

        public override float GetAIIdealRange()
        {
            return Mathf.Max(0.75f, Range * 0.82f);
        }

        public override float GetAIMaxRange()
        {
            return Mathf.Max(1f, Range + HitRadiusPadding);
        }
    }
}
