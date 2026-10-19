using System;
using System.Collections.Generic;
using System.Reflection;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MOBA.Tests.EditMode
{
    internal static class CombatFeedbackTestUtility
    {
        internal static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        internal static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        internal static BrawlerController Brawler(GameObject go, BrawlerDefinition definition, TeamType team)
        {
            var brawler = go.AddComponent<BrawlerController>();
            typeof(BrawlerController).GetField("_team", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(brawler, team);
            typeof(BrawlerController).GetField("_definition", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(brawler, definition);
            typeof(BrawlerController).GetProperty("State").SetValue(brawler, new BrawlerState(definition, team) { Owner = brawler });
            CombatRegistry.Register(brawler);
            return brawler;
        }
    }

    public class CombatNumberBufferTests
    {
        [Test]
        public void RapidHits_CombineOnlyWithinFixedWindowAndKeepDamageSeparateFromHealing()
        {
            var pool = new CombatNumberBuffer(4);
            int first = pool.Add(1, false, 10f, Vector3.zero, 0f);
            Assert.AreEqual(first, pool.Add(1, false, 20f, Vector3.zero, 0.1f));
            Assert.AreEqual(30f, pool.Get(first).Amount);
            Assert.AreNotEqual(first, pool.Add(1, true, 5f, Vector3.zero, 0.1f));
            Assert.AreNotEqual(first, pool.Add(1, false, 30f, Vector3.zero, 0.2f));
            Assert.AreNotEqual(first, pool.Add(2, false, 10f, Vector3.zero, 0.1f));
            Assert.IsFalse(pool.IsActive(first, CombatNumberBuffer.LifetimeSeconds));
        }

        [Test]
        public void Pool_StaysBoundedUnderCrowdedCombatAndReusesExpiredSlots()
        {
            var pool = new CombatNumberBuffer(3);
            for (int i = 1; i <= 100; i++) Assert.That(pool.Add(i, false, i, Vector3.zero, i * 0.001f), Is.InRange(0, 2));
            Assert.AreEqual(3, pool.Capacity);
            int slot = pool.Add(101, true, 1f, Vector3.zero, 2f);
            Assert.AreEqual(1f, pool.Get(slot).Amount);
            pool.Clear();
            for (int i = 0; i < pool.Capacity; i++) Assert.IsFalse(pool.IsActive(i, 2f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidAmounts_DoNotCreateNumbers(float amount)
        {
            Assert.AreEqual(-1, new CombatNumberBuffer().Add(1, false, amount, Vector3.zero, 0f));
        }
    }

    public class CombatNumberPresentationTests
    {
        private sealed class Clock : ISimulationClock
        {
            public uint CurrentTick { get; set; } = 300u;
            public float TickDelta => SimulationClock.TickDeltaTime;
        }
        private readonly List<GameObject> _objects = new List<GameObject>();
        private BrawlerDefinition _definition;
        private BrawlerController _player;
        private BrawlerController _enemy;
        private ISimulationClock _previousClock;
        private Clock _clock;

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        [SetUp]
        public void SetUp()
        {
            ServiceProvider.TryGet<ISimulationClock>(out _previousClock);
            _clock = new Clock();
            ServiceProvider.Register<ISimulationClock>(_clock);
            _definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            _definition.BaseHealth = 1000f;
            _definition.BaseDamage = 100f;
            _player = CombatFeedbackTestUtility.Brawler(Create("FeedbackPlayer"), _definition, TeamType.Blue);
            var source = _player.gameObject.AddComponent<PlayerCommandSource>();
            CombatFeedbackTestUtility.Invoke(source, "Awake");
            _enemy = CombatFeedbackTestUtility.Brawler(Create("FeedbackEnemy"), _definition, TeamType.Red);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                var view = _objects[i].GetComponent<CombatNumberView>();
                if (view != null) CombatFeedbackTestUtility.Invoke(view, "OnDisable");
                var brawler = _objects[i].GetComponent<BrawlerController>();
                if (brawler != null) CombatRegistry.Unregister(brawler);
                Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
            Object.DestroyImmediate(_definition);
            ServiceProvider.Unregister<ISimulationClock>(_clock);
            if (_previousClock != null) ServiceProvider.Register<ISimulationClock>(_previousClock);
        }

        [Test]
        public void DamageAndHealingEvents_UseActualHealthChange()
        {
            float damage = 0f;
            float heal = 0f;
            Action<BrawlerPresentationEvent> handler = evt =>
            {
                if (evt.EventType == BrawlerPresentationEventType.DamageTaken) damage += evt.Value;
                if (evt.EventType == BrawlerPresentationEventType.Healed) heal += evt.Value;
            };
            BrawlerPresentationEventBus.OnEvent += handler;
            try
            {
                _enemy.State.TakeDamage(2000f);
                Assert.AreEqual(1000f, damage, "Overkill must not inflate displayed damage.");
                _player.State.TakeDamage(200f);
                _player.State.Heal(500f, _player, false);
                _player.State.Heal(500f, _player, false);
                Assert.AreEqual(200f, heal, "Overheal and full-health heals must not inflate numbers.");
                _player.State.TakeDamage(100f);
                _clock.CurrentTick += SimulationClock.SecondsToTicks(4f);
                _player.State.TickHealthRegeneration(_clock.CurrentTick, 0.1f);
                Assert.AreEqual(213f, heal, 0.001f, "Regeneration reports actual restoration too.");
            }
            finally { BrawlerPresentationEventBus.OnEvent -= handler; }
        }

        [Test]
        public void View_ConsumesEventsWithoutCreatingObjectsPerHitAndFiltersHiddenHealing()
        {
            var view = Create("FeedbackNumbers").AddComponent<CombatNumberView>();
            CombatFeedbackTestUtility.Invoke(view, "Awake");
            CombatFeedbackTestUtility.Invoke(view, "OnEnable");
            int children = view.GetComponentsInChildren<Transform>(true).Length;
            var pool = CombatFeedbackTestUtility.Field<CombatNumberBuffer>(view, "_buffer");
            _enemy.State.Stealth.IsInBush = true;
            _enemy.State.Stats.ApplyDamage(100f);
            _enemy.State.Heal(50f, _enemy, false);
            Assert.IsFalse(pool.IsActive(0, Time.time));
            _enemy.State.Stealth.IsInBush = false;
            _enemy.State.TakeDamage(20f);
            _enemy.State.TakeDamage(30f);
            Assert.AreEqual(50f, pool.Get(0).Amount);
            Assert.AreEqual(children, view.GetComponentsInChildren<Transform>(true).Length);
            CombatFeedbackTestUtility.Invoke(view, "OnDisable");
            _enemy.State.TakeDamage(10f);
            Assert.IsFalse(pool.IsActive(0, Time.time));
        }
    }
}
