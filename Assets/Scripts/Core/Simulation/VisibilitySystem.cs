using UnityEngine;
using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Simulation
{
    public struct VisibilityRuleConfig
    {
        public readonly bool EnableProximityReveal;
        public readonly float ProximityRevealDistance;

        public VisibilityRuleConfig(bool enableProximityReveal, float proximityRevealDistance)
        {
            EnableProximityReveal = enableProximityReveal;
            ProximityRevealDistance = Mathf.Max(0f, proximityRevealDistance);
        }

        public static VisibilityRuleConfig Default => new VisibilityRuleConfig(false, 2f);

        public float ProximityRevealDistanceSq => ProximityRevealDistance * ProximityRevealDistance;
    }

    public static class VisibilitySystem
    {
        public static void UpdateVisibility(List<BrawlerController> allBrawlers, MapData map)
        {
            UpdateVisibility(allBrawlers, map, VisibilityRuleConfig.Default);
        }

        public static void UpdateVisibility(
            List<BrawlerController> allBrawlers,
            MapData map,
            VisibilityRuleConfig rules)
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
                if (isInBush && rules.EnableProximityReveal)
                {
                    for (int j = 0; j < allBrawlers.Count; j++)
                    {
                        BrawlerController other = allBrawlers[j];
                        if (other == brawler ||
                            !SpatialEntityUtility.IsAlive(other) ||
                            other.State == null ||
                            !TeamRelationshipUtility.AreEnemies(other.Team, brawler.Team))
                        {
                            continue;
                        }

                        if ((other.Position - brawler.Position).sqrMagnitude <= rules.ProximityRevealDistanceSq)
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
