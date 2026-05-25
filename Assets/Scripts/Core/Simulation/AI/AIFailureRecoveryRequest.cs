using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public struct AIFailureRecoveryRequest
    {
        public AIFailureRecoveryReason Reason;
        public uint Tick;
        public int ConsecutiveCount;
        public int RecoveryIndex;
        public int SideSign;
        public Vector3 Destination;
        public float DistanceToDestination;
    }
}
