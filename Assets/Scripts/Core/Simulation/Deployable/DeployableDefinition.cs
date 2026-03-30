using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "DeployableDefinition", menuName = "MOBA/Deployables/Deployable Definition")]
    public class DeployableDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DeployableName;
        public GameObject Prefab;

        [Header("Runtime")]
        public MOBA.Core.Simulation.DeployableType DeployableType;
        public float LifetimeSeconds = 8f;
        public float MaxHealth = 2000f;

        [Header("Combat")]
        public float DetectionRadius = 6f;
        public float ActionIntervalSeconds = 1f;

        [Header("Offense/Support")]
        public AbilityDefinition AbilityDefinition;

        [Header("Pulse / Zone")]
        public float PulseIntervalSeconds = 1f;
        public float PulseRadius = 5f;

        [Header("Capabilities")]
        public bool CanReceiveHealing = true;
        public bool CanReceiveShield = true;
        public bool CanReceiveStatusEffects = true;

        [Header("Ownership Rules")]
        public bool UniquePerOwner = false;
        public bool ReplaceOlderOwnedDeployable = true;
    }
}