using System;
using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MOBA.Tests.EditMode
{
    public class CombatHitFeedbackTests
    {
        private sealed class Clock : ISimulationClock
        {
            public uint CurrentTick => 300u;
            public float TickDelta => SimulationClock.TickDeltaTime;
        }
        private readonly List<GameObject> _objects = new List<GameObject>();
        private BrawlerDefinition _definition;
        private BrawlerController _player;
        private BrawlerController _enemy;
        private ISimulationClock _previousClock;
        private ICombatLogService _previousLog;
        private bool _hadVolume;
        private float _previousVolume;
        private CombatHitFeedbackView _view;

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
            ServiceProvider.TryGet<ICombatLogService>(out _previousLog);
            ServiceProvider.Register<ISimulationClock>(new Clock());
            ServiceProvider.Register<ICombatLogService>(new CombatLogService());
            _hadVolume = PlayerPrefs.HasKey(CombatFeedbackSettings.EffectsVolumeKey);
            _previousVolume = PlayerPrefs.GetFloat(CombatFeedbackSettings.EffectsVolumeKey);
            CombatFeedbackSettings.EffectsVolume = 0.75f;
            _definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            _definition.BaseHealth = 1000f;
            _definition.BaseDamage = 100f;
            _player = CombatFeedbackTestUtility.Brawler(Create("HitPlayer"), _definition, TeamType.Blue);
            // Isolate feedback accounting from the game's global damage balance multiplier.
            _player.State.Damage.SetBaseValue(_definition.BaseDamage);
            var source = _player.gameObject.AddComponent<PlayerCommandSource>();
            CombatFeedbackTestUtility.Invoke(source, "Awake");
            _enemy = CombatFeedbackTestUtility.Brawler(Create("HitEnemy"), _definition, TeamType.Red);
        }

        [TearDown]
        public void TearDown()
        {
            if (_view != null)
            {
                CombatFeedbackTestUtility.Invoke(_view, "OnDisable");
                CombatFeedbackTestUtility.Invoke(_view, "OnDestroy");
            }
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                var brawler = _objects[i].GetComponent<BrawlerController>();
                if (brawler != null) CombatRegistry.Unregister(brawler);
                Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
            Object.DestroyImmediate(_definition);
            ServiceProvider.Unregister<ISimulationClock>();
            ServiceProvider.Unregister<ICombatLogService>();
            if (_previousClock != null) ServiceProvider.Register<ISimulationClock>(_previousClock);
            if (_previousLog != null) ServiceProvider.Register<ICombatLogService>(_previousLog);
            if (_hadVolume) PlayerPrefs.SetFloat(CombatFeedbackSettings.EffectsVolumeKey, _previousVolume);
            else PlayerPrefs.DeleteKey(CombatFeedbackSettings.EffectsVolumeKey);
            PlayerPrefs.Save();
        }

        private DamageResultContext Hit(float damage)
        {
            DamageResultContext result = default;
            Action<DamageResultContext> handler = evt => result = evt;
            DamageEventBus.OnDamageApplied += handler;
            try
            {
                new DamageService().ApplyDamage(new DamageContext { Attacker = _player, Target = _enemy,
                    Damage = damage, Type = DamageType.Projectile, HitPosition = _enemy.Position });
            }
            finally { DamageEventBus.OnDamageApplied -= handler; }
            return result;
        }

        [Test]
        public void ResolvedHits_DistinguishHealthShieldMixedAndOverkillWithoutChangingDamageSemantics()
        {
            var normal = Hit(100f);
            Assert.AreEqual(100f, normal.HealthDamageApplied);
            Assert.AreEqual(0f, normal.ShieldAbsorbed);
            Assert.IsTrue(CombatHitFeedbackView.ShouldConfirm(normal));
            _enemy.State.AddShield(150f);
            var shield = Hit(100f);
            Assert.AreEqual(0f, shield.HealthDamageApplied);
            Assert.AreEqual(100f, shield.ShieldAbsorbed);
            Assert.IsTrue(CombatHitFeedbackView.ShouldConfirm(shield));
            var mixed = Hit(100f);
            Assert.AreEqual(50f, mixed.HealthDamageApplied);
            Assert.AreEqual(50f, mixed.ShieldAbsorbed);
            var fatal = Hit(2000f);
            Assert.AreEqual(850f, fatal.HealthDamageApplied);
            Assert.AreEqual(2000f, fatal.FinalDamageApplied, "Existing simulation consumers retain their original damage field.");
            Assert.IsTrue(CombatHitFeedbackView.ShouldConfirm(fatal));
            Assert.IsFalse(CombatHitFeedbackView.ShouldConfirm(Hit(100f)), "A dead target must not confirm another hit.");
        }

        [Test]
        public void ExistingShieldModifier_DoesNotAbsorbDamageTwice()
        {
            _enemy.State.AddShield(100f);
            _enemy.State.IncomingDamageModifiers.Add(new DamageModifier(DamageModifierType.Shield, 0f));
            var result = Hit(150f);
            Assert.AreEqual(100f, result.ShieldAbsorbed);
            Assert.AreEqual(50f, result.HealthDamageApplied);
            Assert.AreEqual(950f, _enemy.State.CurrentHealth);
        }

        [Test]
        public void EnvironmentalDamage_ConsumesOrdinaryShieldPoolToo()
        {
            _enemy.State.AddShield(150f);
            new DamageService().ApplyDamage(new DamageContext { Target = _enemy, Damage = 100f });
            Assert.AreEqual(50f, _enemy.State.ShieldHealth);
            Assert.AreEqual(1000f, _enemy.State.CurrentHealth);
        }

        [Test]
        public void Confirmation_IgnoresMissesAlliesAIAndHiddenTargets()
        {
            var result = new DamageResultContext { Damage = new DamageContext { Attacker = _player, Target = _enemy } };
            Assert.IsFalse(CombatHitFeedbackView.ShouldConfirm(result));
            result.HealthDamageApplied = 10f;
            Assert.IsTrue(CombatHitFeedbackView.ShouldConfirm(result));
            _enemy.State.Stealth.IsInBush = true;
            Assert.IsFalse(CombatHitFeedbackView.ShouldConfirm(result));
            _enemy.State.Stealth.IsInBush = false;
            result.Damage.Attacker = _enemy;
            Assert.IsFalse(CombatHitFeedbackView.ShouldConfirm(result));
            result.Damage.Attacker = _player;
            result.Damage.Target = _player;
            Assert.IsFalse(CombatHitFeedbackView.ShouldConfirm(result));
        }

        [Test]
        public void IncomingShield_IsVisibleToThePlayerWithoutConfirmingAnEnemyHitAsTheirOwn()
        {
            var result = new DamageResultContext { Damage = new DamageContext { Attacker = _enemy, Target = _player },
                ShieldAbsorbed = 100f };
            Assert.IsFalse(CombatHitFeedbackView.ShouldConfirm(result));
            Assert.IsTrue(CombatHitFeedbackView.ShouldShowIncomingShield(result));
            result.ShieldAbsorbed = 0f;
            result.HealthDamageApplied = 100f;
            Assert.IsFalse(CombatHitFeedbackView.ShouldShowIncomingShield(result));
        }

        [Test]
        public void View_ReusesMarkersThrottlesAudioAndCanDisableAndReenable()
        {
            _view = Create("HitFeedback").AddComponent<CombatHitFeedbackView>();
            CombatFeedbackTestUtility.Invoke(_view, "Awake");
            CombatFeedbackTestUtility.Invoke(_view, "OnEnable");
            int children = _view.GetComponentsInChildren<Transform>(true).Length;
            _enemy.State.AddShield(500f);
            Hit(10f);
            float next = CombatFeedbackTestUtility.Field<float>(_view, "_nextShieldSound");
            for (int i = 0; i < 20; i++) Hit(1f);
            Assert.AreEqual(next, CombatFeedbackTestUtility.Field<float>(_view, "_nextShieldSound"));
            Assert.AreEqual(children, _view.GetComponentsInChildren<Transform>(true).Length);
            Assert.That(_view.GetComponentInChildren<Canvas>().GetComponentsInChildren<Text>(true),
                Has.Some.Matches<Text>(text => text.text == "SHIELD"));
            CombatFeedbackTestUtility.Invoke(_view, "OnDisable");
            Assert.IsFalse(_view.GetComponentInChildren<Canvas>(true).gameObject.activeSelf);
            CombatFeedbackTestUtility.Invoke(_view, "OnEnable");
            Assert.IsTrue(_view.GetComponentInChildren<Canvas>(true).gameObject.activeSelf);
        }

        [Test]
        public void VolumeSlider_MutesOnlyEffectsAndPersistsTheValue()
        {
            _view = Create("VolumeFeedback").AddComponent<CombatHitFeedbackView>();
            CombatFeedbackTestUtility.Invoke(_view, "Awake");
            CombatFeedbackTestUtility.Invoke(_view, "OnEnable");
            var slider = _view.GetComponentInChildren<Slider>(true);
            slider.value = 0f;
            Assert.AreEqual(0f, CombatFeedbackSettings.EffectsVolume);
            Assert.AreEqual(0f, PlayerPrefs.GetFloat(CombatFeedbackSettings.EffectsVolumeKey));
            Assert.That(_view.GetComponentsInChildren<Text>(true), Has.Some.Matches<Text>(text => text.text == "SFX MUTED"));
            Hit(100f);
            Assert.AreEqual(0f, CombatFeedbackTestUtility.Field<float>(_view, "_nextHitSound"));
            CombatFeedbackSettings.EffectsVolume = 2f;
            Assert.AreEqual(1f, CombatFeedbackSettings.EffectsVolume);
            CombatFeedbackSettings.EffectsVolume = float.NaN;
            Assert.AreEqual(CombatFeedbackSettings.DefaultEffectsVolume, CombatFeedbackSettings.EffectsVolume);
        }
    }
}
