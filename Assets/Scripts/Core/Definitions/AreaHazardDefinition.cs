using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "AreaHazard", menuName = "MOBA/Hazards/Area Hazard")]
    public class AreaHazardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string HazardName = "Area Hazard";

        [Header("Shape / Lifetime")]
        public float Radius = 2.25f;
        public float DurationSeconds = 2.5f;
        public float TickIntervalSeconds = 0.5f;

        [Header("Effect")]
        public float DamagePerTick = 180f;
        public AbilityTargetTeamRule TargetTeamRule = AbilityTargetTeamRule.Enemy;

        [Header("Presentation")]
        public GameObject VisualPrefab;
    }
}