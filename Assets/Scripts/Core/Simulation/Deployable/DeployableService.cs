using MOBA.Core.Definitions;
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

            GameObject instance = Object.Instantiate(request.Definition.Prefab, request.Position, Quaternion.identity);
            DeployableController controller = instance.GetComponent<DeployableController>();

            if (controller == null)
            {
                controller = instance.AddComponent<DeployableController>();
            }

            controller.Initialize(request);
            return controller;
        }
    }
}