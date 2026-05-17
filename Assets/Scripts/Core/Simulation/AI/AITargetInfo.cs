using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public class AITargetInfo
    {
        public ISpatialEntity Target { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public uint LastSeenTick { get; private set; }

        public bool HasLiveTarget
        {
            get
            {
                if (Target == null)
                    return false;

                if (Target is BrawlerController bc)
                {
                    return bc.State != null &&
                           !bc.State.IsDead;
                }

                return true;
            }
        }

        public void Remember(ISpatialEntity target, uint currentTick)
        {
            Target = target;
            LastKnownPosition = target.Position;
            LastSeenTick = currentTick;
        }

        public void RefreshLastKnownPosition(uint currentTick)
        {
            if (Target == null) return;

            LastKnownPosition = Target.Position;
            LastSeenTick = currentTick;
        }

        public void LoseLiveTarget()
        {
            Target = null;
        }

        public bool HasRecentMemory(uint currentTick, uint memoryDurationTicks)
        {
            if (LastSeenTick == 0)
                return false;

            return (currentTick - LastSeenTick) <= memoryDurationTicks;
        }

        public void Clear()
        {
            Target = null;
            LastKnownPosition = Vector3.zero;
            LastSeenTick = 0;
        }
    }
}