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
        public Color BaseTint = new Color(0.68f, 0.50f, 0.30f, 1f);
        public Color HitFlashColor = new Color(1f, 0.74f, 0.22f, 1f);
        public float HitFlashSeconds = 0.08f;
        public Color CriticalTint = new Color(0.43f, 0.31f, 0.19f, 1f);
        [Range(0f, 1f)]
        public float CriticalHealthPercent = 0.35f;

        [Header("Fallback Destroyed Visual")]
        public bool SpawnFallbackDebris = true;
        [Range(0, 12)]
        public int FallbackDebrisPieces = 5;
        public float FallbackDebrisLifetimeSeconds = 1.8f;
        public Color FallbackDebrisColor = new Color(0.42f, 0.34f, 0.26f, 1f);
    }
}
