using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public struct GameplayThreatInfo
    {
        public BrawlerController Owner;
        public TeamType Team;
        public Vector3 Position;
        public Vector3 Direction;
        public float Radius;
        public float Damage;
        public float TimeToImpact;
        public bool IsProjectile;
        public bool IsAreaHazard;
        public bool IsSuper;
    }
}
