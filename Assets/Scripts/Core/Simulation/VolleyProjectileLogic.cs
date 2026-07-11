using System.Collections;
using UnityEngine;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public sealed class VolleyProjectileLogic : IAbilityLogic
    {
        private readonly VolleyProjectileAbilityDefinition _definition;

        public VolleyProjectileLogic(VolleyProjectileAbilityDefinition definition)
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

            brawler.RunTimedBurst(FireVolleyRoutine(brawler, baseDirection, context));

            return AbilityExecutionResult.Succeeded(_definition, context.SlotType);
        }

        public void Tick(uint currentTick)
        {
        }

        private IEnumerator FireVolleyRoutine(BrawlerController brawler, Vector3 baseDirection, AbilityExecutionContext context)
        {
            int count = Mathf.Max(1, _definition.ProjectileCount);
            float totalSpread = Mathf.Max(0f, _definition.SpreadAngle);
            float step = count > 1 ? totalSpread / (count - 1) : 0f;
            float startAngle = -totalSpread * 0.5f;

            for (int i = 0; i < count; i++)
            {
                if (!SpatialEntityUtility.IsAlive(brawler) || brawler.State == null || brawler.State.IsDead)
                    yield break;

                float angle = startAngle + step * i;
                Vector3 shotDirection = Quaternion.Euler(0f, angle, 0f) * baseDirection;
                FireSingleProjectile(brawler, shotDirection.normalized, context);

                if (i < count - 1 && _definition.DelayBetweenShots > 0f)
                    yield return new WaitForSeconds(_definition.DelayBetweenShots);
            }
        }

        private void FireSingleProjectile(BrawlerController brawler, Vector3 direction, AbilityExecutionContext context)
        {
            brawler.FireProjectile(
                brawler.GetCastPosition(),
                direction,
                _definition.Speed,
                _definition.Range,
                _definition.Damage,
                _definition,
                context.SlotType,
                context.IsSuper,
                context.IsGadget,
                _definition.PresentationProfile
            );
        }
    }
}
