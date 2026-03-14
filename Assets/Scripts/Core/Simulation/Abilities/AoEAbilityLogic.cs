using UnityEngine;
using System.Collections.Generic;

namespace MOBA.Core.Simulation.Abilities
{
    public class AoEAbilityLogic : IAbilityLogic
    {
        private float _damage;
        private float _radius;
        private GameObject _vfxPrefab; // Reference for the View to spawn

        public AoEAbilityLogic(float damage, float radius)
        {
            _damage = damage;
            _radius = radius;
        }

        public void Execute(IAbilityUser user, AbilityContext context)
        {
            // 1. Tell the View to play the "Explosion" effect at the target location
            // user.PlayVFX(context.Origin, _radius); 

            // 2. Query the SpatialGrid
            // Note: For large radii, we might check neighboring cells
            List<ISpatialEntity> targets = SimulationClock.Grid?.GetEntitiesInCell(context.Origin);

            if (targets == null) return;

            float sqrRadius = _radius * _radius;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];

                // Don't hit the caster (optional)
                if (target.EntityID == user.State.Definition.GetInstanceID()) continue;

                float distSq = (target.Position - context.Origin).sqrMagnitude;

                if (distSq <= sqrRadius)
                {
                    target.TakeDamage(_damage);
                    Debug.Log($"[SIM] AoE Hit on {target.EntityID} for {_damage} damage!");
                }
            }
        }

        public void Tick(uint currentTick) { /* Logic for 'Lingering' AoEs like poison would go here */ }
    }
}