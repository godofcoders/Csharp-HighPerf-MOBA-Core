using UnityEngine;

namespace MOBA.Core.Simulation
{
    public enum BrawlerCommandType
    {
        None = 0,
        Move = 1,
        MainAttack = 2,
        Gadget = 3,
        Super = 4,
        Hypercharge = 5
    }

    public struct BrawlerCommand
    {
        public BrawlerCommandType Type;
        public Vector3 Direction;
        public Vector3 TargetPoint;
        public bool HasTargetPoint;
        public uint Tick;
    }
}