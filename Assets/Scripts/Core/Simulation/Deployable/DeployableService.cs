using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public interface IDeployableService
    {
        DeployableController Spawn(in DeployableSpawnRequest request);
    }

    public sealed class DeployableService : IDeployableService
    {
        public DeployableController Spawn(in DeployableSpawnRequest request)
        {
            if (request.Definition == null || request.Definition.Prefab == null)
                return null;

            IDeployableRegistry registry = ServiceProvider.Get<IDeployableRegistry>();

            if (registry != null &&
                request.Owner != null &&
                request.Definition.UniquePerOwner)
            {
                DeployableController existing = registry.GetActiveOwnedDeployable(request.Owner, request.Definition);
                if (existing != null && request.Definition.ReplaceOlderOwnedDeployable)
                {
                    existing.Despawn();
                }
            }

            GameObject instance = Object.Instantiate(request.Definition.Prefab, request.Position, Quaternion.identity);
            DeployableController controller = instance.GetComponent<DeployableController>();

            if (controller == null)
                controller = instance.AddComponent<DeployableController>();

            controller.Initialize(request);

            if (registry != null)
                registry.Register(controller);

            return controller;
        }
    }
}