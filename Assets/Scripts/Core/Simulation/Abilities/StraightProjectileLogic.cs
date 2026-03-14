using UnityEngine;

namespace MOBA.Core.Simulation.Abilities
{
    public class StraightProjectileLogic : IAbilityLogic
    {
        private float _damage;
        private float _range;
        private float _speed;
        private int _projectileCount;

        public StraightProjectileLogic(float damage, float range, float speed, int count)
        {
            _damage = damage;
            _range = range;
            _speed = speed;
            _projectileCount = count;
        }

        public void Execute(IAbilityUser user, AbilityContext context)
        {
            // Logic: Create N projectiles with a slight delay or offset
            for (int i = 0; i < _projectileCount; i++)
            {
                // In a AAA engine, we might queue these to fire over several ticks
                user.FireProjectile(context.Origin, context.Direction, _speed, _range, _damage);
            }
        }

        public void Tick(uint currentTick) { /* Not needed for instant fire */ }
    }
}