using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class BurstSequenceProjectileLogic : IAbilityLogic
    {
        private readonly BurstSequenceProjectileAbilityDefinition _definition;

        public BurstSequenceProjectileLogic(BurstSequenceProjectileAbilityDefinition definition)
        {
            _definition = definition;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (_definition == null || user == null)
                return AbilityExecutionResult.Failed(_definition, context.SlotType);

            if (!(user is BrawlerController brawler))
                return AbilityExecutionResult.Failed(_definition, context.SlotType);

            Vector3 baseDirection = context.Direction.sqrMagnitude > 0.001f
                ? context.Direction.normalized
                : brawler.transform.forward;

            Vector3 origin = brawler.transform.position;
            int count = Mathf.Max(1, _definition.ProjectileCount);

            for (int i = 0; i < count; i++)
            {
                float forwardOffset = _definition.ForwardSpacing * i;

                // Spawn bullets one behind another in the firing lane.
                Vector3 shotOrigin = origin - (baseDirection * forwardOffset);

                Vector3 shotDirection = baseDirection;

                if (_definition.RandomSpreadAngle > 0f)
                {
                    float randomYaw = Random.Range(-_definition.RandomSpreadAngle, _definition.RandomSpreadAngle);
                    shotDirection = Quaternion.Euler(0f, randomYaw, 0f) * shotDirection;
                    shotDirection.Normalize();
                }

                brawler.FireProjectile(
                    shotOrigin,
                    shotDirection,
                    _definition.Speed,
                    _definition.Range,
                    _definition.Damage,
                    _definition,
                    context.SlotType,
                    context.IsSuper,
                    context.IsGadget
                );
            }

            return AbilityExecutionResult.Succeeded(_definition, context.SlotType);
        }

        public void Tick(uint currentTick)
        {
        }
    }
}