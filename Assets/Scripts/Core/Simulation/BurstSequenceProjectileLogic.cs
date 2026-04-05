using System.Collections;
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

            brawler.RunTimedBurst(FireBurstRoutine(brawler, baseDirection, context));

            return AbilityExecutionResult.Succeeded(_definition, context.SlotType);
        }

        public void Tick(uint currentTick)
        {
        }

        private IEnumerator FireBurstRoutine(BrawlerController brawler, Vector3 baseDirection, AbilityExecutionContext context)
        {
            int count = Mathf.Max(1, _definition.ProjectileCount);

            for (int i = 0; i < count; i++)
            {
                Vector3 shotOrigin = ResolveShotOrigin(brawler, i);
                Vector3 shotDirection = ApplySpread(baseDirection);

                brawler.FireProjectile(
     shotOrigin,
     shotDirection,
     _definition.Speed,
     _definition.Range,
     _definition.Damage,
     _definition,
     context.SlotType,
     context.IsSuper,
     context.IsGadget,
     _definition.PresentationProfile
 );

                if (i < count - 1 && _definition.DelayBetweenShots > 0f)
                    yield return new WaitForSeconds(_definition.DelayBetweenShots);
            }
        }

        private Vector3 ResolveShotOrigin(BrawlerController brawler, int shotIndex)
        {
            if (!_definition.AlternateMuzzles)
                return brawler.GetCastPosition();

            bool usePrimary = (shotIndex % 2 == 0);
            return usePrimary
                ? brawler.GetPrimaryFirePosition()
                : brawler.GetSecondaryFirePosition();
        }

        private Vector3 ApplySpread(Vector3 baseDirection)
        {
            if (_definition.RandomSpreadAngle <= 0f)
                return baseDirection;

            float randomYaw = Random.Range(-_definition.RandomSpreadAngle, _definition.RandomSpreadAngle);
            Vector3 dir = Quaternion.Euler(0f, randomYaw, 0f) * baseDirection;
            return dir.normalized;
        }
    }
}