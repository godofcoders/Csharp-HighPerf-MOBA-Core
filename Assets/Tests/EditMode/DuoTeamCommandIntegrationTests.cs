using System.Collections.Generic;
using System.Reflection;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    // Logic fixtures avoid runtime model/VFX creation and explicitly initialize
    // MonoBehaviours whose Awake is not invoked by Unity's EditMode runner.
    internal static class DuoCommandTestUtility
    {
        internal static object Invoke(object target, string method, params object[] args)
        {
            return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, args);
        }

        internal static void SetField(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        internal static void Initialize(BrawlerController brawler, BrawlerDefinition definition, TeamType team)
        {
            SetField(brawler, "_definition", definition);
            SetField(brawler, "_team", team);
            var state = new BrawlerState(definition, team) { Owner = brawler };
            typeof(BrawlerController).GetProperty("State").SetValue(brawler, state);
            CombatRegistry.Register(brawler);
        }

        internal static PlayerCommandSource AddPlayer(GameObject go)
        {
            var source = go.AddComponent<PlayerCommandSource>();
            Invoke(source, "Awake");
            return source;
        }

        internal static PowerCube AddCube(GameObject go)
        {
            var cube = go.AddComponent<PowerCube>();
            SetField(cube, "_useRuntimeVisual", false);
            Invoke(cube, "OnEnable");
            return cube;
        }
    }

    public class DuoTeamCommandIntegrationTests
    {
        private sealed class Clock : ISimulationClock
        {
            public uint CurrentTick { get; set; } = 100u;
            public float TickDelta => SimulationClock.TickDeltaTime;
        }

        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<PowerCube> _cubes = new List<PowerCube>();
        private BrawlerDefinition _definition;
        private SoloShowdownMode _mode;
        private BrawlerController _player;
        private BrawlerController _bot;
        private PlayerCommandSource _source;
        private Clock _clock;
        private ISimulationClock _previousClock;
        private GameModeId _previousMode;
        private ShowdownVariant _previousVariant;
        private SpatialGrid _previousGrid;
        private AStarSolver _previousPathfinder;

        private static void SetSimulationProperty(string name, object value)
        {
            typeof(SimulationClock).GetProperty(name).SetValue(null, value);
        }


        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        private BrawlerController Brawler(string name, TeamType team, Vector3 position)
        {
            var go = Create(name);
            go.transform.position = position;
            var brawler = go.AddComponent<BrawlerController>();
            DuoCommandTestUtility.Initialize(brawler, _definition, team);
            _mode.RegisterBrawler(brawler);
            return brawler;
        }

        private PowerCube Cube(Vector3 point)
        {
            var go = Create("CommandCube");
            go.transform.position = point;
            var cube = DuoCommandTestUtility.AddCube(go);
            _cubes.Add(cube);
            return cube;
        }

        [SetUp]
        public void SetUp()
        {
            _previousGrid = SimulationClock.Grid;
            _previousPathfinder = SimulationClock.Pathfinder;
            SetSimulationProperty("Grid", null);
            SetSimulationProperty("Pathfinder", null);
            _previousMode = SceneSelection.SelectedMode;
            _previousVariant = SceneSelection.SelectedShowdownVariant;
            ServiceProvider.TryGet<ISimulationClock>(out _previousClock);
            _clock = new Clock();
            ServiceProvider.Register<ISimulationClock>(_clock);
            SceneSelection.SelectedMode = GameModeId.SoloShowdown;
            SceneSelection.SelectedShowdownVariant = ShowdownVariant.Duo;
            _definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            _definition.BaseHealth = 2000f;
            _definition.BaseMoveSpeed = 5f;
            _mode = Create("CommandMode").AddComponent<SoloShowdownMode>();
            DuoCommandTestUtility.Invoke(_mode, "Awake");
            _player = Brawler("CommandPlayer", TeamType.Solo1, Vector3.zero);
            _bot = Brawler("CommandBot", TeamType.Solo1, Vector3.right);
            _source = DuoCommandTestUtility.AddPlayer(_player.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var cube in _cubes)
                if (cube != null) DuoCommandTestUtility.Invoke(cube, "OnDisable");
            _cubes.Clear();
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                var brawler = _objects[i].GetComponent<BrawlerController>();
                if (brawler != null) CombatRegistry.Unregister(brawler);
                Object.DestroyImmediate(_objects[i]);
            }
            _objects.Clear();
            Object.DestroyImmediate(_definition);
            SetSimulationProperty("Grid", _previousGrid);
            SetSimulationProperty("Pathfinder", _previousPathfinder);
            ServiceProvider.Unregister<ISimulationClock>(_clock);
            if (_previousClock != null) ServiceProvider.Register<ISimulationClock>(_previousClock);
            SceneSelection.SelectedMode = _previousMode;
            SceneSelection.SelectedShowdownVariant = _previousVariant;
        }

        [Test]
        public void PlayerAvailability_DoesNotDependOnAICohesionRole()
        {
            Assert.IsFalse(_mode.TryGetDuoCohesionAnchor(_player, out _));
            Assert.IsTrue(_mode.TryGetLivingDuoTeammate(_player, out var teammate));
            Assert.AreSame(_bot, teammate);
            Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.Regroup, Vector3.zero));
            Assert.IsTrue(_mode.TryGetIssuedDuoTeamCommand(_player, _clock.CurrentTick, out _, out _));
        }

        [Test]
        public void DuoRespawn_ExpiresOnSimulationTickDeadline()
        {
            DuoCommandTestUtility.Invoke(_mode, "QueueDuoRespawn", _bot);

            Assert.IsTrue(_mode.TryGetPendingTeammateRespawn(
                _player,
                out BrawlerController teammate,
                out float initialSeconds));
            Assert.AreSame(_bot, teammate);
            Assert.AreEqual(ShowdownRules.DuoRespawnDelaySeconds, initialSeconds);

            _clock.CurrentTick += SimulationTiming.SecondsToTicks(1f);
            Assert.IsTrue(_mode.TryGetPendingTeammateRespawn(
                _player,
                out _,
                out float remainingSeconds));
            Assert.AreEqual(ShowdownRules.DuoRespawnDelaySeconds - 1f, remainingSeconds);

            _clock.CurrentTick += SimulationTiming.SecondsToTicks(remainingSeconds);
            _mode.Tick(_clock.CurrentTick);
            Assert.IsFalse(_mode.IsRespawnPending(_bot));
        }

        [Test]
        public void Input_ReplacesAtCooldownBoundaryAndExpiresWithoutAffectingEnemyTeams()
        {
            var enemy = Brawler("Enemy", TeamType.Solo2, Vector3.forward * 3f);
            Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.Regroup, Vector3.zero));
            Assert.AreEqual(ShowdownRules.DuoTeamCommandCooldownTicks,
                _mode.GetDuoTeamCommandCooldownTicks(_player, _clock.CurrentTick));
            Assert.AreEqual(DuoTeamCommandIssueResult.Cooldown,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.Retreat, Vector3.zero));
            _clock.CurrentTick += ShowdownRules.DuoTeamCommandCooldownTicks;
            Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.Retreat, Vector3.zero));
            Assert.IsTrue(_mode.TryGetActiveDuoTeamCommand(_bot, _clock.CurrentTick, out var directive));
            Assert.AreEqual(DuoTeamCommandType.Retreat, directive.CommandType);
            Assert.IsFalse(_mode.TryGetActiveDuoTeamCommand(enemy, _clock.CurrentTick, out _));
            _clock.CurrentTick = directive.ExpiresTick + 1u;
            Assert.IsFalse(_mode.TryGetIssuedDuoTeamCommand(_player, _clock.CurrentTick, out _, out _));
        }

        [Test]
        public void Attack_SelectsAimedEnemyAndCancelsWhenItDies()
        {
            Brawler("NearestEnemy", TeamType.Solo2, Vector3.forward * 2f);
            var aimed = Brawler("AimedEnemy", TeamType.Solo3, Vector3.forward * 8f);
            Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.Attack, aimed.Position));
            Assert.IsTrue(_mode.TryGetActiveDuoTeamCommand(_bot, _clock.CurrentTick, out var command));
            Assert.AreEqual(aimed.EntityID, command.TargetEntityId);
            aimed.State.Stats.ApplyDamage(10000f);
            Assert.IsFalse(_mode.TryGetIssuedDuoTeamCommand(_player, _clock.CurrentTick, out _, out _));
            Assert.AreEqual(0u, _mode.GetDuoTeamCommandCooldownTicks(_player, _clock.CurrentTick));
        }

        [Test]
        public void TakeCube_SelectsNearestEligiblePickupAndClearsAfterCollection()
        {
            var far = Cube(Vector3.forward * 7f);
            var near = Cube(Vector3.forward * 2f);
            Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.TakeCube, far.transform.position));
            Assert.IsTrue(_mode.TryGetActiveDuoTeamCommand(_bot, _clock.CurrentTick, out var command));
            Assert.AreEqual(near.gameObject.GetInstanceID(), command.TargetEntityId);
            Assert.IsTrue(near.TryPickupBy(_bot.State));
            Assert.IsFalse(_mode.TryGetActiveDuoTeamCommand(_bot, _clock.CurrentTick, out _));
        }

        [Test]
        public void Input_RejectsMissingTargetsAndDeadTeammates()
        {
            Assert.AreEqual(DuoTeamCommandIssueResult.InvalidTarget,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.Attack, Vector3.zero));
            Assert.AreEqual(DuoTeamCommandIssueResult.InvalidTarget,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.TakeCube, Vector3.zero));
            _bot.State.Stats.ApplyDamage(10000f);
            Assert.AreEqual(DuoTeamCommandIssueResult.TeammateUnavailable,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.Regroup, Vector3.zero));
        }

        [Test]
        public void IssuerDeath_InvalidatesDirectiveAndDoesNotRestoreItAfterReviving()
        {
            Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.Regroup, Vector3.zero));
            _player.State.Stats.ApplyDamage(10000f);
            Assert.IsFalse(_mode.TryGetActiveDuoTeamCommand(_bot, _clock.CurrentTick, out _));
            _player.State.ResetHealthToMax();
            Assert.IsFalse(_mode.TryGetActiveDuoTeamCommand(_bot, _clock.CurrentTick, out _));
            Assert.AreEqual(0u, _mode.GetDuoTeamCommandCooldownTicks(_player, _clock.CurrentTick));
        }

        [Test]
        public void TakeCube_SkipsDisconnectedPickupAndFallsBackToChest()
        {
            bool[,] walkable = new bool[12, 12];
            for (int x = 0; x < 12; x++)
                for (int z = 0; z < 12; z++) walkable[x, z] = x != 7;
            SetSimulationProperty("Pathfinder", new AStarSolver(walkable, 1f, new Vector3(-5f, 0f, -5f)));
            SetSimulationProperty("Grid", new SpatialGrid(4f));
            Cube(new Vector3(3f, 0f, 2f)); // Across the solid wall.
            var crateObject = Create("ReachableChest");
            crateObject.transform.position = new Vector3(0f, 0f, 3f);
            var crate = crateObject.AddComponent<PowerCubeCrateController>();
            DuoCommandTestUtility.SetField(crate, "_currentHealth", 1000f);
            CombatRegistry.Register(crate);
            SimulationClock.Grid.Add(crate);
            try
            {
                Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                    _source.IssueDuoTeamCommand(DuoTeamCommandType.TakeCube, Vector3.zero));
                Assert.IsTrue(_mode.TryGetActiveDuoTeamCommand(_bot, _clock.CurrentTick, out var command));
                Assert.AreEqual(crate.EntityID, command.TargetEntityId);
            }
            finally { CombatRegistry.Unregister(crate); }
        }

        [Test]
        public void Poison_RejectsUnsafePickupAndClampsRetreatInsideSafeZone()
        {
            // The hazard's activation gate requires the Match scene name. Keep
            // the runner's unsaved test scene and restore its name in finally.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string previousSceneName = scene.name;
            scene.name = "Match";
            var managerObject = new GameObject("PoisonMatchManager");
            var zoneObject = new GameObject("CommandPoisonZone");
            var profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            try
            {
                var manager = managerObject.AddComponent<MatchManager>();
                DuoCommandTestUtility.Invoke(manager, "Awake");
                typeof(MatchManager).GetProperty("CurrentState").SetValue(manager, MatchState.Active);
                var zone = zoneObject.AddComponent<SoloShowdownPoisonZone>();
                DuoCommandTestUtility.Invoke(zone, "Awake");
                DuoCommandTestUtility.SetField(zone, "_elapsedActiveSeconds", 30f);
                Assert.IsTrue(zone.IsHazardActive);
                var cube = Cube(new Vector3(3f, 0f, 2f));
                Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                    _source.IssueDuoTeamCommand(DuoTeamCommandType.TakeCube, Vector3.zero));
                cube.transform.position = new Vector3(23f, 0f, 0f);
                var commands = new AICommandSource();
                var nav = new NavigationAgent(_bot, commands, profile);
                var executor = new AIActionExecutor(_bot, profile, nav, null, null, null, null, commands);
                Assert.IsFalse((bool)DuoCommandTestUtility.Invoke(executor, "TryExecuteDuoCommand",
                    AIActionType.Objective, new AITargetInfo(), _clock.CurrentTick, 6f, 4f, 8f));
                _clock.CurrentTick += ShowdownRules.DuoTeamCommandCooldownTicks;
                Assert.AreEqual(DuoTeamCommandIssueResult.InvalidTarget,
                    _source.IssueDuoTeamCommand(DuoTeamCommandType.TakeCube, Vector3.zero));
                _player.transform.position = new Vector3(30f, 0f, 0f);
                Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                    _source.IssueDuoTeamCommand(DuoTeamCommandType.Retreat, Vector3.zero));
                executor.Execute(AIActionType.Retreat, new AITargetInfo(), _clock.CurrentTick, 6f, 4f, 8f);
                Assert.IsTrue(nav.HasDestination);
                Assert.IsTrue(zone.IsInsideSafeZone(nav.Destination));
                Assert.That(nav.Destination.x, Is.LessThan(24f));
            }
            finally
            {
                Object.DestroyImmediate(zoneObject);
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(profile);
                scene.name = previousSceneName;
            }
        }

        [Test]
        public void Execution_TakeCubeNavigatesToLivePickupAndYieldsToEvasion()
        {
            var cube = Cube(new Vector3(3f, 0f, 2f));
            Assert.AreEqual(DuoTeamCommandIssueResult.Accepted,
                _source.IssueDuoTeamCommand(DuoTeamCommandType.TakeCube, Vector3.zero));
            cube.transform.position = new Vector3(4f, 0f, 3f);
            var profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            try
            {
                var commands = new AICommandSource();
                var nav = new NavigationAgent(_bot, commands, profile);
                var executor = new AIActionExecutor(_bot, profile, nav, null, null, null, null, commands);
                var target = new AITargetInfo();
                Assert.IsFalse((bool)DuoCommandTestUtility.Invoke(executor, "TryExecuteDuoCommand",
                    AIActionType.Evade, target, _clock.CurrentTick, 6f, 4f, 8f));
                executor.Execute(AIActionType.Objective, target, _clock.CurrentTick, 6f, 4f, 8f);
                Assert.IsTrue(nav.HasDestination);
                Assert.That(Vector3.Distance(cube.transform.position, nav.Destination), Is.LessThan(0.01f));
            }
            finally { Object.DestroyImmediate(profile); }
        }
    }
}
