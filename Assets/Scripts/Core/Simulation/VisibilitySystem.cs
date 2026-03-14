using UnityEngine;
using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Simulation
{
    public static class VisibilitySystem
    {
        private const float RevealDistanceSq = 4f; // Enemies within 2m reveal you in bush

        public static void UpdateVisibility(List<BrawlerController> allBrawlers, MapData map)
        {
            foreach (var brawler in allBrawlers)
            {
                // 1. Check Grid for Bush
                var coords = GetGridCoords(brawler.Position, map);
                brawler.State.IsInBush = map.BushGrid[coords.x, coords.y];

                // 2. Proximity Reveal
                bool proximityReveal = false;
                foreach (var other in allBrawlers)
                {
                    if (other == brawler || other.Team == brawler.Team) continue;

                    if ((other.Position - brawler.Position).sqrMagnitude < RevealDistanceSq)
                    {
                        proximityReveal = true;
                        break;
                    }
                }
                brawler.State.IsRevealed = proximityReveal;
            }
        }

        private static Vector2Int GetGridCoords(Vector3 pos, MapData map)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((pos.x - map.Origin.x) / map.CellSize), 0, map.WalkabilityGrid.GetLength(0) - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((pos.z - map.Origin.z) / map.CellSize), 0, map.WalkabilityGrid.GetLength(1) - 1);
            return new Vector2Int(x, y);
        }
    }
}