using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "MineTrapDeployable", menuName = "MOBA/Deployables/Mine Trap")]
    public class MineTrapDeployableDefinition : DeployableDefinition
    {
        [Header("Mine Trap")]
        public float TriggerRadius = 0.85f;
        public float ExplosionRadius = 2.15f;
        public float Damage = 1800f;
        public float ArmDelaySeconds = 0.35f;
        public float DetonationDelaySeconds = 1.5f;
        public bool HideWhenArmed = true;

        private void OnValidate()
        {
            DeployableType = MOBA.Core.Simulation.DeployableType.Trap;
            CanReceiveHealing = false;
            CanReceiveShield = false;
            CanReceiveStatusEffects = false;
        }
    }
}
