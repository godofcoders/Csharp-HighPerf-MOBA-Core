using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    /// <summary>
    /// "Barrage" variant of ThrownHybridAoEAbilityDefinition — fires N arcing
    /// projectiles at landing points fanned around the aim direction with a
    /// small per-shot delay, each planting an optional lingering hazard
    /// (e.g. Barley's puddle) on impact.
    ///
    /// Authoring home for Barley's super "Last Call": 6 bottles thrown in a
    /// forward-spreading pattern, each landing a puddle. Impact damage is
    /// typically 0 — the puddles are the damage source — but both knobs exist.
    /// </summary>
    [CreateAssetMenu(fileName = "ThrownVolleyAoEAbility", menuName = "MOBA/Abilities/Thrown Volley AoE")]
    public class ThrownVolleyAoEAbilityDefinition : AbilityDefinition
    {
        [Header("Throw Delivery")]
        public float ThrowRange = 7f;
        public float ThrowSpeed = 16f;
        public float ArcHeight = 1.75f;

        [Header("Impact AoE (per-bottle)")]
        public float ImpactRadius = 2.25f;
        public float EnemyDamage = 0f;
        public float AllyHeal = 0f;

        [Header("Volley")]
        [Min(1)] public int ProjectileCount = 6;
        [Min(0f)] public float DelayBetweenShots = 0.08f;
        [Tooltip("Total angular spread of landing points around the aim direction, in degrees. Bottles are evenly distributed across this arc.")]
        [Min(0f)] public float LandingSpreadAngle = 30f;
        [Tooltip("Fractional randomness applied to each bottle's landing distance. 0.2 = ±20% of ThrowRange.")]
        [Range(0f, 1f)] public float DistanceJitter = 0.2f;

        [Header("Presentation")]
        public ProjectilePresentationProfile PresentationProfile;

        [Header("Lingering Hazard")]
        public AreaHazardDefinition LingeringHazard;

        [Header("Target Rules")]
        public ProjectileHitTeamRule HitTeamRule = ProjectileHitTeamRule.EnemiesOnly;
        public bool CanAffectEnemiesOnImpact = true;
        public bool CanAffectAlliesOnImpact = false;

        public override IAbilityLogic CreateLogic()
        {
            return new MOBA.Core.Simulation.Abilities.ThrownVolleyAoEAbilityLogic(this);
        }
    }
}
