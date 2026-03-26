using System.Collections.Generic;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public static class BrawlerDebugTracker
    {
        private static readonly Dictionary<int, BrawlerDebugSnapshot> _snapshots = new Dictionary<int, BrawlerDebugSnapshot>();

        public static void UpdateSnapshot(BrawlerController controller, BrawlerDebugSnapshot snapshot)
        {
            if (controller == null || snapshot == null)
                return;

            _snapshots[controller.EntityID] = snapshot;
        }

        public static BrawlerDebugSnapshot GetSnapshot(int entityId)
        {
            _snapshots.TryGetValue(entityId, out BrawlerDebugSnapshot snapshot);
            return snapshot;
        }

        public static void Remove(BrawlerController controller)
        {
            if (controller == null)
                return;

            _snapshots.Remove(controller.EntityID);
        }
    }
}