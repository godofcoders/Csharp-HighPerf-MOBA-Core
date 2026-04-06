using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.Abilities
{
    public class StraightProjectileLogic : IAbilityLogic
    {
        private readonly float _damage;
        private readonly float _range;
        private readonly float _speed;
        private readonly int _projectileCount;
        private readonly ProjectilePresentationProfile _presentationProfile;

        public StraightProjectileLogic(
            float damage,
            float range,
            float speed,
            int projectileCount,
            ProjectilePresentationProfile presentationProfile)
        {
            _damage = damage;
            _range = range;
            _speed = speed;
            _projectileCount = projectileCount;
            _presentationProfile = presentationProfile;
        }

        public AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context)
        {
            if (user == null)
            {
                return AbilityExecutionResult.Failed(context.AbilityDefinition, context.SlotType);
            }

            for (int i = 0; i < _projectileCount; i++)
            {
                user.FireProjectile(
                    context.Origin,
                    context.Direction,
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

        public void Tick(uint currentTick) { }
    }
}