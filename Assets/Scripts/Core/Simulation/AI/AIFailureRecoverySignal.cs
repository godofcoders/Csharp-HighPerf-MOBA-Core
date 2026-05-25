using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public struct AIFailureRecoverySignal
    {
        public AIFailureRecoveryReason Reason;
        public uint Tick;
        public int ConsecutiveCount;
        public Vector3 Destination;
        public float DistanceToDestination;
        public uint DestinationAgeTicks;
        public float ProgressDistance;

        public bool IsActive => Reason != AIFailureRecoveryReason.None;
    }
}
