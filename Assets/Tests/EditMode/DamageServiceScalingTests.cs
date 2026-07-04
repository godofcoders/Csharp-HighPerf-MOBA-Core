using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class DamageServiceScalingTests
    {
        private sealed class TestClock : ISimulationClock
        {
            public uint CurrentTick => 0u;
            public float TickDelta => 1f / 30f;
        }

        private GameObject _attackerObject;
        private GameObject _targetObject;
        private BrawlerDefinition _attackerDefinition;
        private BrawlerDefinition _targetDefinition;

        [SetUp]
        public void SetUp()
        {
            ServiceProvider.Clear();
            ServiceProvider.Register<ISimulationClock>(new TestClock());
            ServiceProvider.Register<ICombatLogService>(new CombatLogService());
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackerObject != null)
            {
                CombatRegistry.Unregister(_attackerObject.GetComponent<BrawlerController>());
                Object.DestroyImmediate(_attackerObject);
            }

            if (_targetObject != null)
            {
                CombatRegistry.Unregister(_targetObject.GetComponent<BrawlerController>());
                Object.DestroyImmediate(_targetObject);
            }

            if (_attackerDefinition != null)
                Object.DestroyImmediate(_attackerDefinition);

            if (_targetDefinition != null)
                Object.DestroyImmediate(_targetDefinition);

            ServiceProvider.Clear();
        }

        [Test]
        public void ApplyDamage_ScalesByAttackerRuntimeDamageStat()
        {
            _attackerDefinition = CreateDefinition("Attacker", baseHealth: 1000f, baseDamage: 100f);
            _targetDefinition = CreateDefinition("Target", baseHealth: 1000f, baseDamage: 100f);

            BrawlerController attacker = CreateBrawler("Attacker", _attackerDefinition, TeamType.Blue, powerLevel: 11);
            BrawlerController target = CreateBrawler("Target", _targetDefinition, TeamType.Red, powerLevel: 1);

            new DamageService().ApplyDamage(new DamageContext
            {
                Attacker = attacker,
                Target = target,
                Damage = 100f,
                Type = DamageType.Projectile,
                Direction = Vector3.forward,
                HitPosition = target.Position
            });

            Assert.AreEqual(850f, target.State.CurrentHealth, 0.001f);
        }

        private static BrawlerDefinition CreateDefinition(string name, float baseHealth, float baseDamage)
        {
            BrawlerDefinition definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            definition.BrawlerName = name;
            definition.BaseHealth = baseHealth;
            definition.BaseMoveSpeed = 5f;
            definition.BaseDamage = baseDamage;
            definition.ProgressionBonuses = null;
            definition.SuperChargeSources = null;
            return definition;
        }

        private BrawlerController CreateBrawler(
            string objectName,
            BrawlerDefinition definition,
            TeamType team,
            int powerLevel)
        {
            GameObject gameObject = new GameObject(objectName);
            BrawlerController controller = gameObject.AddComponent<BrawlerController>();
            controller.InitializeFromMatchmaking(definition, team, powerLevelOverride: powerLevel);

            if (team == TeamType.Blue)
                _attackerObject = gameObject;
            else
                _targetObject = gameObject;

            return controller;
        }
    }
}
