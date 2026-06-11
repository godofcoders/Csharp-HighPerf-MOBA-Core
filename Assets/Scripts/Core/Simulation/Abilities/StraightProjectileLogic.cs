using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public class StraightProjectileLogic : IAbilityLogic
    {
        private readonly float _damage;
        private readonly float _range;
        private readonly float _speed;
        private readonly int _projectileCount;
        private readonly float _spreadAngle;
        private readonly float _parallelLaneSpacing;
        private readonly float _forwardSpawnOffset;
        private readonly ProjectilePresentationProfile _presentationProfile;

        public StraightProjectileLogic(
            float damage,
            float range,
            float speed,
            int projectileCount,
            float spreadAngle,
            float parallelLaneSpacing,
            float forwardSpawnOffset,
            ProjectilePresentationProfile presentationProfile)
        {
            _damage = damage;
            _range = range;
            _speed = speed;
            _projectileCount = Mathf.Max(1, projectileCount);
            _spreadAngle = Mathf.Max(0f, spreadAngle);
            _parallelLaneSpacing = Mathf.Max(0f, parallelLaneSpacing);
            _forwardSpawnOffset = Mathf.Max(0f, forwardSpawnOffset);
            _presentationProfile = presentationProfile;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user == null)
            {
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
            }

            Vector3 baseDirection = ResolveBaseDirection(context.Direction);

            for (int i = 0; i < _projectileCount; i++)
            {
                Vector3 shotDirection = ResolveShotDirection(baseDirection, i);
                Vector3 shotOrigin = ResolveShotOrigin(context.Origin, baseDirection, i);

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

            var result = AbilityExecutionResult.Succeeded(context.AbilityDefinition, context.SlotType);
            result.SpawnedProjectile = true;
            result.ProjectileCount = _projectileCount;
            result.ConsumedResource = true;

            return result;
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
