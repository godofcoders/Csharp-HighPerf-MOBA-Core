using UnityEngine;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.Abilities
{
    public class DashGadgetLogic : IAbilityLogic
    {
        private readonly float _dashForce;

        public DashGadgetLogic(float force)
        {
            _dashForce = force;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user is not MonoBehaviour mb)
            {
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
            }

            Vector3 dashVec = context.Direction.normalized * _dashForce;
            mb.transform.position += dashVec;

            Debug.Log("[SIM] Gadget Executed: Dash");

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.ConsumedResource = true;
            return result;
        }

        public void Tick(uint currentTick) { }
    }
}