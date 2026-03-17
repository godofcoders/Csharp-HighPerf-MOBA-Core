using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public static class AITeamMemory
    {
        private struct TeamMemoryData
        {
            public bool HasHotspot;
            public Vector3 HotspotPosition;
            public uint LastSeenTick;
        }

        private static TeamMemoryData _blueMemory;
        private static TeamMemoryData _redMemory;

        public static void ReportEnemySighting(TeamType observerTeam, Vector3 position, uint currentTick)
        {
            ref TeamMemoryData memory = ref GetMemory(observerTeam);
            memory.HasHotspot = true;
            memory.HotspotPosition = position;
            memory.LastSeenTick = currentTick;
        }

        public static bool TryGetRecentHotspot(TeamType team, uint currentTick, uint maxAgeTicks, out Vector3 hotspot)
        {
            ref TeamMemoryData memory = ref GetMemory(team);

            if (memory.HasHotspot && (currentTick - memory.LastSeenTick) <= maxAgeTicks)
            {
                hotspot = memory.HotspotPosition;
                return true;
            }

            hotspot = default;
            return false;
        }

        public static void Clear(TeamType team)
        {
            ref TeamMemoryData memory = ref GetMemory(team);
            memory.HasHotspot = false;
            memory.HotspotPosition = default;
            memory.LastSeenTick = 0;
        }

        private static ref TeamMemoryData GetMemory(TeamType team)
        {
            if (team == TeamType.Blue)
                return ref _blueMemory;

            return ref _redMemory;
        }
    }
}