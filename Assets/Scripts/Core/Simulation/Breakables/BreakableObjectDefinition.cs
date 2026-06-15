using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    [CreateAssetMenu(fileName = "BreakableObject", menuName = "MOBA/Breakable Object Definition")]
    public sealed class BreakableObjectDefinition : ScriptableObject
    {
        [Header("Health")]
        public float MaxHealth = 1200f;

        [Header("Collision")]
        public float CollisionRadius = 0.55f;
        public bool BlocksNavigation = true;
        public float NavigationClearRadius = 0.75f;

        [Header("Damage Rules")]
        public bool CanBeDamagedByProjectiles = true;
        public bool CanBeDamagedByAreaEffects = true;
        public bool RequiresSuperDamage;
        [Tooltip("Optional. If assigned, only this exact ability asset can damage this object.")]
        public AbilityDefinition RequiredSourceAbility;

        [Header("Lifecycle")]
        public bool DestroyGameObjectOnDeath = true;
        public GameObject DestroyedVisualPrefab;

        [Header("Presentation")]
        public Color HitFlashColor = new Color(1f, 0.74f, 0.22f, 1f);
        public float HitFlashSeconds = 0.08f;
        public Color CriticalTint = new Color(0.46f, 0.46f, 0.46f, 1f);
        [Range(0f, 1f)]
        public float CriticalHealthPercent = 0.35f;
    }
}
