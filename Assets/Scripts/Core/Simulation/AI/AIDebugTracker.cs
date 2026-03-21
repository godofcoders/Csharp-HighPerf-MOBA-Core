using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIDebugTracker
    {
        private static readonly Dictionary<int, AIDebugSnapshot> _snapshots = new Dictionary<int, AIDebugSnapshot>(64);

        public static void Register(BrawlerController brawler)
        {
            if (brawler == null)
                return;

            if (!_snapshots.ContainsKey(brawler.EntityID))
            {
                _snapshots[brawler.EntityID] = new AIDebugSnapshot();
            }
        }

        public static void Unregister(BrawlerController brawler)
        {
            if (brawler == null)
                return;

            _snapshots.Remove(brawler.EntityID);
        }

        public static AIDebugSnapshot GetSnapshot(int entityId)
        {
            _snapshots.TryGetValue(entityId, out var snapshot);
            return snapshot;
        }

        public static void UpdateSnapshot(BrawlerController brawler, AIDebugSnapshot snapshot)
        {
            if (brawler == null || snapshot == null)
                return;

            _snapshots[brawler.EntityID] = snapshot;
        }

        public static IEnumerable<KeyValuePair<int, AIDebugSnapshot>> GetAll()
        {
            return _snapshots;
        }
    }
}