using System.Collections;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public class StraightProjectileLogic : IAbilityLogic
    {
        private readonly float _damage;
        private readonly float _range;
        private readonly float _speed;
        private readonly int _projectileCount;
        private readonly float _delayBetweenProjectiles;
        private readonly float _spreadAngle;
        private readonly float _parallelLaneSpacing;
        private readonly float _forwardSpawnOffset;
        private readonly ProjectilePresentationProfile _presentationProfile;
        private readonly bool _damageScalesWithDistance;
        private readonly float _minDamageMultiplier;
        private readonly float _damageScaleStartRatio;

        public StraightProjectileLogic(
            float damage,
            float range,
            float speed,
            int projectileCount,
            float delayBetweenProjectiles,
            float spreadAngle,
            float parallelLaneSpacing,
            float forwardSpawnOffset,
            ProjectilePresentationProfile presentationProfile,
            bool damageScalesWithDistance = false,
            float minDamageMultiplier = 1f,
            float damageScaleStartRatio = 0f)
        {
            _damage = damage;
            _range = range;
            _speed = speed;
            _projectileCount = Mathf.Max(1, projectileCount);
            _delayBetweenProjectiles = Mathf.Max(0f, delayBetweenProjectiles);
            _spreadAngle = Mathf.Max(0f, spreadAngle);
            _parallelLaneSpacing = Mathf.Max(0f, parallelLaneSpacing);
            _forwardSpawnOffset = Mathf.Max(0f, forwardSpawnOffset);
            _presentationProfile = presentationProfile;
            _damageScalesWithDistance = damageScalesWithDistance;
            _minDamageMultiplier = Mathf.Clamp(minDamageMultiplier, 0.05f, 1f);
            _damageScaleStartRatio = Mathf.Clamp01(damageScaleStartRatio);
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user == null)
            {
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
            }

            Vector3 baseDirection = ResolveBaseDirection(context.Direction);

            if (_projectileCount > 1 &&
                _delayBetweenProjectiles > 0f &&
                user is BrawlerController brawler)
            {
                brawler.RunTimedBurst(FireSequenceRoutine(user, context, baseDirection));
            }
            else
            {
                for (int i = 0; i < _projectileCount; i++)
                {
                    FireSingleProjectile(user, context, baseDirection, i);
                }
            }

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.SpawnedProjectile = true;
            result.ProjectileCount = _projectileCount;
            result.ConsumedResource = true;

            return result;
        }

        private IEnumerator FireSequenceRoutine(
            IAbilityUser user,
            AbilityExecutionContext context,
            Vector3 baseDirection)
        {
            for (int i = 0; i < _projectileCount; i++)
            {
                FireSingleProjectile(user, context, baseDirection, i);

                if (i < _projectileCount - 1)
                    yield return new WaitForSeconds(_delayBetweenProjectiles);
            }
        }

        private void FireSingleProjectile(
            IAbilityUser user,
            AbilityExecutionContext context,
            Vector3 baseDirection,
            int shotIndex)
        {
            Vector3 shotDirection = ResolveShotDirection(baseDirection, shotIndex);
            Vector3 shotOrigin = ResolveShotOrigin(
                ResolveSourceOrigin(user, context.Origin),
                baseDirection,
                shotIndex);

            if (_damageScalesWithDistance)
            {
                FireDistanceScaledProjectile(
                    user,
                    context,
                    shotOrigin,
                    shotDirection);
                return;
            }

            user.FireProjectile(
                shotOrigin,
                shotDirection,
                _speed,
                _range,
                _damage,
                context.AbilityDefinition,
                context.SlotType,
                context.IsSuper,
                context.IsGadget,
                _presentationProfile);
        }

        private void FireDistanceScaledProjectile(
            IAbilityUser user,
            AbilityExecutionContext context,
            Vector3 shotOrigin,
            Vector3 shotDirection)
        {
            if (user is not BrawlerController brawler)
            {
                user.FireProjectile(
                    shotOrigin,
                    shotDirection,
                    _speed,
                    _range,
                    _damage,
                    context.AbilityDefinition,
                    context.SlotType,
                    context.IsSuper,
                    context.IsGadget,
                    _presentationProfile);
                return;
            }

            IProjectileService projectileService = ServiceProvider.Get<IProjectileService>();
            if (projectileService == null)
            {
                user.FireProjectile(
                    shotOrigin,
                    shotDirection,
                    _speed,
                    _range,
                    _damage,
                    context.AbilityDefinition,
                    context.SlotType,
                    context.IsSuper,
                    context.IsGadget,
                    _presentationProfile);
                return;
            }

            ProjectileSpawnContext spawnContext = new ProjectileSpawnContext
            {
                Owner = brawler,
                SourceAbility = context.AbilityDefinition,
                SlotType = context.SlotType,
                Origin = shotOrigin,
                Direction = shotDirection,
                Speed = _speed,
                Range = _range,
                Damage = _damage,
                DamageScalesWithDistance = true,
                MinDamageMultiplier = _minDamageMultiplier,
                DamageScaleStartRatio = _damageScaleStartRatio,
                Team = brawler.Team,
                IsSuper = context.IsSuper,
                IsGadget = context.IsGadget,
                IsHybrid = false,
                HitTeamRule = ProjectileHitTeamRule.EnemiesOnly,
                DeliveryType = ProjectileDeliveryType.DirectHit,
                PresentationProfile = _presentationProfile
            };

            projectileService.FireProjectile(spawnContext);
        }

        private Vector3 ResolveSourceOrigin(IAbilityUser user, Vector3 fallbackOrigin)
        {
            return user is BrawlerController brawler
                ? brawler.Position
                : fallbackOrigin;
        }

        private Vector3 ResolveBaseDirection(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.forward;
        }

        private Vector3 ResolveShotDirection(Vector3 baseDirection, int shotIndex)
        {
            if (_projectileCount <= 1 || _spreadAngle <= 0.001f)
                return baseDirection;

            float t = _projectileCount == 1
                ? 0.5f
                : shotIndex / (float)(_projectileCount - 1);
            float angle = Mathf.Lerp(-_spreadAngle * 0.5f, _spreadAngle * 0.5f, t);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * baseDirection;
            direction.y = 0f;

            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : baseDirection;
        }

        private Vector3 ResolveShotOrigin(Vector3 origin, Vector3 baseDirection, int shotIndex)
        {
            Vector3 spawnOrigin = origin + baseDirection * _forwardSpawnOffset;

            if (_projectileCount <= 1 || _parallelLaneSpacing <= 0.001f)
                return spawnOrigin;

            Vector3 right = new Vector3(baseDirection.z, 0f, -baseDirection.x);
            if (right.sqrMagnitude <= 0.001f)
                return spawnOrigin;

            right.Normalize();

            float laneOffset = (shotIndex % 2 == 0 ? -0.5f : 0.5f) * _parallelLaneSpacing;
            return spawnOrigin + right * laneOffset;
        }

        public void Tick(uint currentTick) { }
    }
}
