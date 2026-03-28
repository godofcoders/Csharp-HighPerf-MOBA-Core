using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public struct AbilityTargetRequest
    {
        public BrawlerController Source;
        public Vector3 Origin;
        public Vector3 Direction;
        public float Range;
        public AbilityTargetTeamRule TeamRule;
        public AbilityTargetSelectionRule SelectionRule;
        public bool IncludeSelf;
        public bool RequireAlive;
    }
}