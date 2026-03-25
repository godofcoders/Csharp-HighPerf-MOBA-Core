using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class PassiveDefinition : BrawlerBuildOptionDefinition
    {
        [Header("Passive Identity")]
        public string PassiveName;
        [TextArea] public string Description;
        public PassiveCategory Category = PassiveCategory.StarPower;

        [Header("Slot Rules")]
        public PassiveSlotType[] AllowedSlotTypes;

        [Header("Family Rules")]
        public PassiveFamilyDefinition Family;
        public bool IsUniqueInFamily = false;

        [Header("Compatibility Rules")]
        public PassiveDefinition[] IncompatiblePassives;

        public virtual void Install(PassiveInstallContext context)
        {
        }

        public virtual IPassiveRuntime CreateRuntime(PassiveInstallContext context)
        {
            return null;
        }

        public virtual void Uninstall(PassiveInstallContext context)
        {
            if (context.State == null)
                return;

            context.State.RemoveAllStatModifiersFromSource(context.SourceToken);
            context.State.RemoveIncomingDamageModifiersFromSource(context.SourceToken);
            context.State.RemoveOutgoingDamageModifiersFromSource(context.SourceToken);
            context.State.RemoveIncomingMovementModifiersFromSource(context.SourceToken);
        }

        public bool IsExplicitlyIncompatibleWith(PassiveDefinition other)
        {
            if (other == null || IncompatiblePassives == null)
                return false;

            for (int i = 0; i < IncompatiblePassives.Length; i++)
            {
                if (IncompatiblePassives[i] == other)
                    return true;
            }

            return false;
        }

        public bool CanEquipInSlot(PassiveSlotType slotType)
        {
            if (slotType == PassiveSlotType.None)
                return false;

            if (AllowedSlotTypes == null || AllowedSlotTypes.Length == 0)
                return true;

            for (int i = 0; i < AllowedSlotTypes.Length; i++)
            {
                if (AllowedSlotTypes[i] == slotType)
                    return true;
            }

            return false;
        }
    }
}