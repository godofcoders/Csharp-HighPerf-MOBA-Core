using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public struct DeployableSpawnRequest
    {
        public BrawlerController Owner;
        public TeamType Team;
        public DeployableDefinition Definition;
        public Vector3 Position;
        public Vector3 Direction;
    }
}