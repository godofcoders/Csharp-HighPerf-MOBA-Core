using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AISpacingUtility
    {
        private readonly BrawlerController _self;
        private readonly List<ISpatialEntity> _nearbyBuffer;

        public AISpacingUtility(BrawlerController self, int initialCapacity = 16)
        {
            _self = self;
            _nearbyBuffer = new List<ISpatialEntity>(initialCapacity);
        }

        public Vector3 GetAntiClumpOffset(float radius)
        {
            if (_self == null || SimulationClock.Grid == null)
                return Vector3.zero;

            _nearbyBuffer.Clear();
            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(_self.Position, radius, _nearbyBuffer);

            Vector3 push = Vector3.zero;

            for (int i = 0; i < _nearbyBuffer.Count; i++)
            {
                var entity = _nearbyBuffer[i];
                if (entity == null || entity.EntityID == _self.EntityID)
                    continue;

                if (entity.Team != _self.Team)
                    continue;

                Vector3 away = _self.Position - entity.Position;
                away.y = 0f;

                float distSq = away.sqrMagnitude;
                if (distSq <= 0.0001f)
                    continue;

                float dist = Mathf.Sqrt(distSq);
                float weight = 1f - Mathf.Clamp01(dist / radius);

                push += away.normalized * weight;
            }

            return push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.zero;
        }

        public Vector3 GetPreferredRangePosition(Vector3 targetPosition, float preferredRange, float antiClumpRadius, float antiClumpWeight)
        {
            Vector3 toSelf = (_self.Position - targetPosition);
            toSelf.y = 0f;

            if (toSelf.sqrMagnitude <= 0.0001f)
                toSelf = _self.transform.forward;

            Vector3 rangeAnchor = targetPosition + toSelf.normalized * preferredRange;
            Vector3 antiClump = GetAntiClumpOffset(antiClumpRadius) * antiClumpWeight;

            Vector3 final = rangeAnchor + antiClump;
            final.y = _self.Position.y;
            return final;
        }

        public Vector3 GetRetreatPosition(Vector3 threatPosition, float retreatDistance, float antiClumpRadius, float antiClumpWeight)
        {
            Vector3 away = (_self.Position - threatPosition);
            away.y = 0f;

            if (away.sqrMagnitude <= 0.0001f)
                away = _self.transform.forward;

            Vector3 retreat = _self.Position + away.normalized * retreatDistance;
            Vector3 antiClump = GetAntiClumpOffset(antiClumpRadius) * antiClumpWeight;

            Vector3 final = retreat + antiClump;
            final.y = _self.Position.y;
            return final;
        }
    }
}