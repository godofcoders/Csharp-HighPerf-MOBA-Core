using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public enum DamageType
    {
        Projectile,
        AoE,
        Melee,
        Ability
    }

    public struct DamageContext
    {
        public BrawlerController Attacker;
        public ISpatialEntity Target;

        public float Damage;
        public DamageType Type;

        public Vector3 HitPosition;
        public Vector3 Direction;

        public bool IsCritical;

        public AbilityDefinition SourceAbility;
        public bool IsSuper;
    }
}