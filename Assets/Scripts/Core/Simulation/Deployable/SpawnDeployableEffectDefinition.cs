using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.Deployable;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "SpawnDeployableEffect", menuName = "MOBA/Abilities/Effects/Spawn Deployable")]
    public class SpawnDeployableEffectDefinition : AbilityEffectDefinition
    {
        [Header("Deployable")]
        [SerializeField] private DeployableDefinition _definition;

        [Header("Delivery")]
        [SerializeField] private bool _useThrownDelivery = true;
        [SerializeField] private GameObject _deliveryVisualPrefab;
        [SerializeField] private float _deliveryArcHeight = 2.5f;
        [SerializeField] private float _deliveryTravelDuration = 0.35f;

        public override bool Apply(IAbilityUser source, BrawlerController target, AbilityExecutionContext context)
        {
            if (source == null || _definition == null)
                return false;

            BrawlerController owner = source as BrawlerController;
            if (owner == null)
                return false;

            Vector3 spawnPosition;

            if (context.HasTargetPoint)
            {
                spawnPosition = context.TargetPoint;
            }
            else
            {
                Vector3 fallbackDirection = context.Direction.sqrMagnitude > 0.001f
                    ? context.Direction.normalized
                    : owner.transform.forward;

                spawnPosition = context.Origin + fallbackDirection * 2f;
            }

            if (_useThrownDelivery && _deliveryVisualPrefab != null)
            {
                GameObject go = Instantiate(_deliveryVisualPrefab, context.Origin, Quaternion.identity);
                ThrownDeployableDeliveryVisual delivery = go.GetComponent<ThrownDeployableDeliveryVisual>();

                if (delivery == null)
                {
                    delivery = go.AddComponent<ThrownDeployableDeliveryVisual>();
                }

                delivery.Initialize(
                    owner,
                    _definition,
                    owner.Team,
                    context.Origin,
                    spawnPosition,
                    _deliveryTravelDuration,
                    _deliveryArcHeight
                );

                return true;
            }

            IDeployableService deployableService = ServiceProvider.Get<IDeployableService>();
            if (deployableService == null)
                return false;

            DeployableSpawnRequest request = new DeployableSpawnRequest
            {
                Owner = owner,
                Definition = _definition,
                Position = spawnPosition,
                Team = owner.Team
            };

            deployableService.Spawn(request);
            return true;
        }
    }
}