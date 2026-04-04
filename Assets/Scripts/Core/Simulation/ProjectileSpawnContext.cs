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
    }
}