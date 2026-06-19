using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public static class GemPlacementUtility
    {
        private const float TwoPi = 6.28318530718f;
        private const float RingPhaseRadians = 0.5235987756f;
        private const int PlacementSearchBudget = 48;

        public static Vector3 GetClusterOffset(int layoutIndex, float spacing)
        {
            if (layoutIndex <= 0)
                return Vector3.zero;

            float safeSpacing = Mathf.Max(0.05f, spacing);
            int remaining = layoutIndex - 1;
            int ring = 1;

            while (true)
            {
                int slotsInRing = ring * 6;
                if (remaining < slotsInRing)
                    break;

                remaining -= slotsInRing;
                ring++;
            }

            int ringSlots = ring * 6;
            float angle = RingPhaseRadians +
                          ring * 0.17f +
                          remaining / (float)ringSlots * TwoPi;
            float radius = safeSpacing * ring;

            return new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);
        }

        public static Vector3 ResolveReadablePosition(
            Vector3 center,
            int layoutIndex,
            float spacing,
            IReadOnlyList<Gem> existingGems,
            IList<Vector3> reservedPositions)
        {
            float safeSpacing = Mathf.Max(0.05f, spacing);
            int startIndex = Mathf.Max(0, layoutIndex);

            for (int attempt = 0; attempt < PlacementSearchBudget; attempt++)
            {
                Vector3 candidate = center + GetClusterOffset(startIndex + attempt, safeSpacing);
                if (!OverlapsExisting(candidate, safeSpacing, existingGems, reservedPositions))
                    return candidate;
            }

            return center + GetClusterOffset(startIndex + PlacementSearchBudget, safeSpacing);
        }

        private static bool OverlapsExisting(
            Vector3 candidate,
            float spacing,
            IReadOnlyList<Gem> existingGems,
            IList<Vector3> reservedPositions)
        {
            float spacingSq = spacing * spacing;

            if (existingGems != null)
            {
                for (int i = 0; i < existingGems.Count; i++)
                {
                    Gem gem = existingGems[i];
                    if (gem == null || gem.IsPickedUp)
                        continue;

                    if (XZDistanceSq(candidate, gem.transform.position) < spacingSq)
                        return true;
                }
            }

            if (reservedPositions != null)
            {
                for (int i = 0; i < reservedPositions.Count; i++)
                {
                    if (XZDistanceSq(candidate, reservedPositions[i]) < spacingSq)
                        return true;
                }
            }

            return false;
        }

        private static float XZDistanceSq(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
