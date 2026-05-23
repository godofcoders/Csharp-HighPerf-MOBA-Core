using UnityEngine;
using MOBA.Core.Simulation;
using System.Collections.Generic;

namespace MOBA.Core.Infrastructure
{
    public class AreaHazardService : MonoBehaviour, IAreaHazardService, IAreaHazardThreatProvider
    {
        private readonly List<AreaHazardController> _activeHazards = new List<AreaHazardController>(16);

        private void Awake()
        {
            ServiceProvider.Register<IAreaHazardService>(this);
            ServiceProvider.Register<IAreaHazardThreatProvider>(this);
        }

        public void SpawnHazard(in AreaHazardSpawnRequest request)
        {
            if (request.Definition == null)
                return;

            GameObject go = new GameObject($"AreaHazard_{request.Definition.name}");
            go.transform.position = request.Position;

            AreaHazardController controller = go.AddComponent<AreaHazardController>();
            controller.Initialize(request, this);
            _activeHazards.Add(controller);
        }

        public void AppendAreaHazardThreatsNonAlloc(
            Vector3 observerPosition,
            TeamType observerTeam,
            float scanRadius,
            List<GameplayThreatInfo> results)
        {
            if (results == null)
                return;

            float scanRadiusSq = scanRadius * scanRadius;

            for (int i = _activeHazards.Count - 1; i >= 0; i--)
            {
                AreaHazardController hazard = _activeHazards[i];
                if (hazard == null)
                {
                    _activeHazards.RemoveAt(i);
                    continue;
                }

                if (!hazard.CanThreatenTeam(observerTeam))
                    continue;

                Vector3 delta = observerPosition - hazard.Position;
                delta.y = 0f;

                if (delta.sqrMagnitude > scanRadiusSq)
                    continue;

                float threatRadius = hazard.Radius + 0.35f;
                if (delta.sqrMagnitude > threatRadius * threatRadius)
                    continue;

                results.Add(new GameplayThreatInfo
                {
                    Owner = hazard.Owner,
                    Team = hazard.Team,
                    Position = hazard.Position,
                    Direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.zero,
                    Radius = threatRadius,
                    Damage = hazard.DamagePerTick,
                    TimeToImpact = 0f,
                    IsProjectile = false,
                    IsAreaHazard = true,
                    IsSuper = hazard.IsSuper
                });
            }
        }

        public void Unregister(AreaHazardController controller)
        {
            for (int i = _activeHazards.Count - 1; i >= 0; i--)
            {
                AreaHazardController hazard = _activeHazards[i];
                if (hazard == null || ReferenceEquals(hazard, controller))
                    _activeHazards.RemoveAt(i);
            }
        }
    }
}
