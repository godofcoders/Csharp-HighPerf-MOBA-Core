using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MOBA.Tests.EditMode
{
    public class BreakableObjectTests
    {
        private sealed class DummyAbilityDefinition : AbilityDefinition
        {
            public override IAbilityLogic CreateLogic()
            {
                return null;
            }
        }

        private GameObject _gameObject;
        private BreakableObjectDefinition _definition;
        private AbilityDefinition _requiredAbility;
        private AbilityDefinition _wrongAbility;

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);

            if (_definition != null)
                Object.DestroyImmediate(_definition);

            if (_requiredAbility != null)
                Object.DestroyImmediate(_requiredAbility);

            if (_wrongAbility != null)
                Object.DestroyImmediate(_wrongAbility);
        }

        [Test]
        public void CanReceiveDamage_RespectsSuperAndSourceAbilityRules()
        {
            BreakableObjectController breakable = CreateBreakable();
            _requiredAbility = ScriptableObject.CreateInstance<DummyAbilityDefinition>();
            _wrongAbility = ScriptableObject.CreateInstance<DummyAbilityDefinition>();

            _definition.RequiresSuperDamage = true;
            _definition.RequiredSourceAbility = _requiredAbility;

            Assert.IsFalse(breakable.CanReceiveDamage(MakeDamageContext(false, _requiredAbility)));
            Assert.IsFalse(breakable.CanReceiveDamage(MakeDamageContext(true, _wrongAbility)));
            Assert.IsTrue(breakable.CanReceiveDamage(MakeDamageContext(true, _requiredAbility)));
        }

        [Test]
        public void TakeDamage_DestroysAndDisablesCollisionWhenHealthReachesZero()
        {
            BoxCollider collider = CreateGameObjectWithCollider();
            BreakableObjectController breakable = _gameObject.AddComponent<BreakableObjectController>();
            _definition = MakeDefinition();
            _definition.MaxHealth = 100f;
            _definition.DestroyGameObjectOnDeath = false;
            _definition.BlocksNavigation = false;

            breakable.Initialize(_definition);

            breakable.TakeDamage(40f);
            Assert.IsFalse(breakable.IsDestroyed);
            Assert.AreEqual(60f, breakable.CurrentHealth);
            Assert.IsTrue(collider.enabled);

            breakable.TakeDamage(60f);
            Assert.IsTrue(breakable.IsDestroyed);
            Assert.IsFalse(collider.enabled);
        }

        [Test]
        public void SetWalkableCircle_UpdatesSolverAndBackingMapData()
        {
            MapData map = new MapData(3, 3, 1f, Vector3.zero);
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                    map.WalkabilityGrid[x, y] = true;
            }

            map.WalkabilityGrid[1, 1] = false;
            AStarSolver solver = new AStarSolver(map);

            Assert.IsFalse(solver.IsWalkable(new Vector2Int(1, 1)));

            int changed = solver.SetWalkableCircle(map.GetWorldPos(1, 1), 0.6f, true);

            Assert.Greater(changed, 0);
            Assert.IsTrue(solver.IsWalkable(new Vector2Int(1, 1)));
            Assert.IsTrue(map.WalkabilityGrid[1, 1]);
        }

        private BreakableObjectController CreateBreakable()
        {
            CreateGameObjectWithCollider();
            BreakableObjectController breakable = _gameObject.AddComponent<BreakableObjectController>();
            _definition = MakeDefinition();
            breakable.Initialize(_definition);
            return breakable;
        }

        private BoxCollider CreateGameObjectWithCollider()
        {
            _gameObject = new GameObject("BreakableObjectTest");
            return _gameObject.AddComponent<BoxCollider>();
        }

        private static BreakableObjectDefinition MakeDefinition()
        {
            BreakableObjectDefinition definition = ScriptableObject.CreateInstance<BreakableObjectDefinition>();
            definition.MaxHealth = 100f;
            definition.CollisionRadius = 0.5f;
            definition.DestroyGameObjectOnDeath = false;
            definition.BlocksNavigation = false;
            return definition;
        }

        private DamageContext MakeDamageContext(bool isSuper, AbilityDefinition sourceAbility)
        {
            return new DamageContext
            {
                Target = _gameObject.GetComponent<BreakableObjectController>(),
                Damage = 50f,
                Type = DamageType.Projectile,
                SourceAbility = sourceAbility,
                IsSuper = isSuper
            };
        }
    }
}
