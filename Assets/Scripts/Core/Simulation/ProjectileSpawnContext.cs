using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public struct ProjectileSpawnContext
    {
        public BrawlerController Owner;
        public AbilityDefinition SourceAbility;
        public AbilitySlotType SlotType;

        public Vector3 Origin;
        public Vector3 Direction;

        public float Speed;
        public float Range;
        public float Damage;

        public TeamType Team;
        public float SuperChargeOnHit;

        public bool IsSuper;
        public bool IsGadget;

        public bool IsHybrid;
        public float AllyHealAmount;
        public float EnemyDamageAmount;

        public ProjectileHitTeamRule HitTeamRule;

        public ProjectileDeliveryType DeliveryType;
        public Vector3 TargetPoint;

        public bool HasHybridAoEImpact;
        public float ImpactRadius;
        public float ImpactEnemyDamage;
        public float ImpactAllyHeal;

        public bool UseArcMotion;
        public float ArcHeight;
        public float TravelDistance;

        public ProjectilePresentationProfile PresentationProfile;
        public bool IsChainProjectile;
        public int RemainingBounces;
        public float BounceRadius;
        public AreaHazardDefinition LingeringHazardDefinition;
        public bool CanAffectEnemiesOnImpact;
        public bool CanAffectAlliesOnImpact;
    }
}