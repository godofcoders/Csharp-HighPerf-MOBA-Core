using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewHypercharge", menuName = "MOBA/Hypercharge Definition")]
    public class HyperchargeDefinition : BrawlerBuildOptionDefinition
    {
        [Header("Identity")]
        public string Name = "Hypercharge";

        [Header("Duration")]
        public float DurationSeconds = 5.0f;

        [Header("Stat Multipliers")]
        [Range(0, 1)] public float SpeedBuff = 0.25f;
        [Range(0, 1)] public float DamageBuff = 0.15f;
        [Range(0, 1)] public float ShieldBuff = 0.10f;

        [Header("Enhanced Super")]
        public AbilityDefinition EnhancedSuper;

        private void OnValidate()
        {
            AllowedBuildSlotTypes = new[] { BrawlerBuildSlotType.Hypercharge };
        }
    }
}