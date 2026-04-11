using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Simulation.Deployable
{
    public class ThrownDeployableDeliveryVisual : MonoBehaviour
    {
        private BrawlerController _owner;
        private DeployableDefinition _deployableDefinition;
        private TeamType _team;

        private Vector3 _startPoint;
        private Vector3 _targetPoint;
        private float _travelDuration;
        private float _arcHeight;
        private float _elapsed;

        private bool _initialized;

        public void Initialize(
            BrawlerController owner,
            DeployableDefinition deployableDefinition,
            TeamType team,
            Vector3 startPoint,
            Vector3 targetPoint,
            float travelDuration,
            float arcHeight)
        {
            _owner = owner;
            _deployableDefinition = deployableDefinition;
            _team = team;

            _startPoint = startPoint;
            _targetPoint = targetPoint;
            _travelDuration = Mathf.Max(0.05f, travelDuration);
            _arcHeight = Mathf.Max(0f, arcHeight);

            transform.position = _startPoint;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
                return;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _travelDuration);

            Vector3 basePos = Vector3.Lerp(_startPoint, _targetPoint, t);
            float arcOffset = 4f * _arcHeight * t * (1f - t);
            Vector3 newPos = basePos + Vector3.up * arcOffset;

            Vector3 flatDir = _targetPoint - transform.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flatDir.normalized);

            transform.position = newPos;

            if (t >= 1f)
            {
                SpawnDeployable();
                Destroy(gameObject);
            }
        }

        private void SpawnDeployable()
        {
            if (_owner == null || _deployableDefinition == null)
                return;

            IDeployableService deployableService = ServiceProvider.Get<IDeployableService>();
            if (deployableService == null)
                return;

            DeployableSpawnRequest request = new DeployableSpawnRequest
            {
                Owner = _owner,
                Definition = _deployableDefinition,
                Position = _targetPoint,
                Team = _team
            };

            deployableService.Spawn(request);
        }
    }
}