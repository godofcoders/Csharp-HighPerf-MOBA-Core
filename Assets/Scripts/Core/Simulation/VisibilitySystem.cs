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
            if (allBrawlers == null)
                return;

            for (int i = 0; i < allBrawlers.Count; i++)
            {
                BrawlerController brawler = allBrawlers[i];
                if (!SpatialEntityUtility.IsAlive(brawler) || brawler.State == null)
                    continue;

                bool isInBush = false;
                if (map != null && map.BushGrid != null)
                {
                    Vector2Int coords = map.GetGridCoords(brawler.Position);
                    isInBush = map.IsBush(coords);
                }

                brawler.State.IsInBush = isInBush;

                bool proximityReveal = false;
                if (isInBush)
                {
                    for (int j = 0; j < allBrawlers.Count; j++)
                    {
                        BrawlerController other = allBrawlers[j];
                        if (other == brawler ||
                            !SpatialEntityUtility.IsAlive(other) ||
                            other.State == null ||
                            other.Team == brawler.Team)
                        {
                            continue;
                        }

                        if ((other.Position - brawler.Position).sqrMagnitude <= RevealDistanceSq)
                        {
                            proximityReveal = true;
                            break;
                        }
                    }
                }

                brawler.State.IsProximityRevealed = proximityReveal;
            }
        }
    }
}
