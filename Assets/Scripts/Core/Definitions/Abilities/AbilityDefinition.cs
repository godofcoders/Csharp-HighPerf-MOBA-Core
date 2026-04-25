using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class AbilityDefinition : BrawlerBuildOptionDefinition
    {
        [Header("Identity")]
        public string AbilityName;
        public Sprite Icon;

        [Header("Ability Taxonomy")]
        public AbilitySlotType SlotType = AbilitySlotType.MainAttack;
        public AbilityTargetingType TargetingType = AbilityTargetingType.Directional;
        public AbilityDeliveryType DeliveryType = AbilityDeliveryType.Instant;

        [Header("Cooldown")]
        public float Cooldown = 1.0f;

        [Header("AI / Combat Tags")]
        public AbilityTag[] Tags;

        [Header("Cast / Lock Rules")]
        [Tooltip("How long the action lock lasts, in seconds.")]
        public float CastDurationSeconds = 0.2f;

        [Tooltip("Whether movement is allowed during cast.")]
        public bool AllowMovementDuringCast = true;

        [Tooltip("Whether other action input is allowed during cast.")]
        public bool AllowActionInputDuringCast = false;

        [Tooltip("Whether this cast can be interrupted by stronger states like stun.")]
        public bool IsInterruptible = true;

        [Header("AI Intent")]
        public AbilityIntentType Intent;

        [Header("Aim Assist")]
        public bool AllowAimAssist = true;
        public MOBA.Core.Simulation.AimAssistMode AimAssistMode = MOBA.Core.Simulation.AimAssistMode.NearestEnemy;
        public bool AimAssistIncludeSelf = false;
        public bool PreferManualAim = true;
        public float AimAssistRangeOverride = -1f;

        [Header("Aim Assist Tuning")]
        public float AimAssistForwardBias = 2f;
        public float AimAssistDistanceBias = 1f;
        public float AimAssistIdealRange = -1f;
        public float AimAssistPlacementDistance = 3f;
        [Header("Aim Preview")]
        public AimPreviewMode PreviewMode = AimPreviewMode.Directional;

        public abstract MOBA.Core.Simulation.IAbilityLogic CreateLogic();

        public virtual float GetAIIdealRange()
        {
            return 6f;
        }

        public virtual float GetAIMaxRange()
        {
            return GetAIIdealRange();
        }

        public uint GetCastDurationTicks()
        {
            return SimulationClock.SecondsToTicks(CastDurationSeconds);
        }

        public bool HasTag(AbilityTag tag)
        {
            if (Tags == null)
                return false;

            for (int i = 0; i < Tags.Length; i++)
            {
                if (Tags[i] == tag)
                    return true;
            }

            return false;
        }
    }
}