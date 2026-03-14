using UnityEngine;

namespace MOBA.Core.Simulation
{
    public struct AbilityContext
    {
        public Vector3 Origin;      // Where the shot starts
        public Vector3 Direction;   // Which way the brawler is aiming
        public uint StartTick;      // When the ability was triggered
        public float PowerLevel;    // For "hold to charge" attacks
    }
}