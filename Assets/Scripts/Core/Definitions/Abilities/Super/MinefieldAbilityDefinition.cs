using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "MinefieldAbility", menuName = "MOBA/Abilities/Minefield")]
    public class MinefieldAbilityDefinition : AbilityDefinition
    {
        [Header("Minefield")]
        public MineTrapDeployableDefinition MineDefinition;
        [Min(1)] public int MineCount = 3;
        public float Range = 7.5f;
        public float MineSpacing = 1.05f;

        public float Damage => MineDefinition != null ? MineDefinition.Damage : 0f;
        public float ExplosionRadius => MineDefinition != null ? MineDefinition.ExplosionRadius : 0f;

        private void OnValidate()
        {
            SlotType = AbilitySlotType.Super;
            TargetingType = AbilityTargetingType.Area;
            DeliveryType = AbilityDeliveryType.Area;
            PreviewMode = AimPreviewMode.Placement;
            Intent = AbilityIntentType.AreaControl;
            AimAssistMode = AimAssistMode.SmartDeployablePlacement;
        }

        public override IAbilityLogic CreateLogic()
        {
            return new MOBA.Core.Simulation.Abilities.MinefieldAbilityLogic(this);
        }

        public override float GetAIIdealRange()
        {
            return Mathf.Max(2f, Range * 0.85f);
        }

        public override float GetAIMaxRange()
        {
            return Mathf.Max(2f, Range);
        }
    }
}
