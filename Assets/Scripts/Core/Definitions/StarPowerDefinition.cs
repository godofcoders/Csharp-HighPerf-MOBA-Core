using UnityEngine;

namespace MOBA.Core.Definitions
{
    public abstract class StarPowerDefinition : ScriptableObject
    {
        public virtual void Install(StarPowerInstallContext context)
        {
        }

        public virtual void Uninstall(StarPowerInstallContext context)
        {
            if (context.State == null)
                return;

            context.State.RemoveAllStatModifiersFromSource(context.SourceToken);
            context.State.RemoveIncomingDamageModifiersFromSource(context.SourceToken);
            context.State.RemoveOutgoingDamageModifiersFromSource(context.SourceToken);
            context.State.RemoveIncomingMovementModifiersFromSource(context.SourceToken);
        }
    }
}