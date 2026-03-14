using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public class DashGadgetLogic : IAbilityLogic
    {
        private float _dashForce;

        public DashGadgetLogic(float force) => _dashForce = force;

        public void Execute(IAbilityUser user, AbilityContext context)
        {
            // Direct manipulation of the transform via the Bridge
            if (user is MonoBehaviour mb)
            {
                Vector3 dashVec = context.Direction.normalized * _dashForce;
                mb.transform.position += dashVec; // Instant dash
                Debug.Log("[SIM] Gadget Executed: Dash");
            }
        }

        public void Tick(uint currentTick) { }
    }
}