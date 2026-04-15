using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public class AreaHazardService : MonoBehaviour, IAreaHazardService
    {
        private void Awake()
        {
            ServiceProvider.Register<IAreaHazardService>(this);
        }

        public void SpawnHazard(in AreaHazardSpawnRequest request)
        {
            if (request.Definition == null)
                return;

            GameObject go = new GameObject($"AreaHazard_{request.Definition.name}");
            go.transform.position = request.Position;

            AreaHazardController controller = go.AddComponent<AreaHazardController>();
            controller.Initialize(request);
        }
    }
}