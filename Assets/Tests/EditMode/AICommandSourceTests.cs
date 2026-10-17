using System.Collections.Generic;
using MOBA.Core.Simulation;
using MOBA.Core.Infrastructure;
using MOBA.Core.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AICommandSourceTests
    {
        [Test]
        public void ClearQueuedCommands_RemovesPendingCommands()
        {
            AICommandSource source = new AICommandSource();
            List<BrawlerCommand> commands = new List<BrawlerCommand>(4);

            source.QueueMove(Vector3.forward);
            source.QueueMainAttack(Vector3.right);
            source.QueueGadget(Vector3.left);
            source.QueueSuper(Vector3.back);
            source.QueueHypercharge();

            source.ClearQueuedCommands();
            source.CollectCommands(commands, 10u);

            Assert.AreEqual(0, commands.Count);
        }

        [Test]
        public void ClearQueuedCommands_AllowsFutureCommands()
        {
            AICommandSource source = new AICommandSource();
            List<BrawlerCommand> commands = new List<BrawlerCommand>(2);

            source.QueueMainAttack(Vector3.right);
            source.ClearQueuedCommands();
            source.QueueMove(Vector3.forward);
            source.CollectCommands(commands, 11u);

            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual(BrawlerCommandType.Move, commands[0].Type);
        }
    }

    public class DuoTeamCommandHUDTests
    {
        private sealed class Clock : ISimulationClock
        {
            public uint CurrentTick => 100u;
            public float TickDelta => SimulationClock.TickDeltaTime;
        }

        [Test]
        public void Wheel_RegroupClickIssuesCommandAndInstallationDoesNotDuplicate()
        {
            var previousMode = SceneSelection.SelectedMode;
            var previousVariant = SceneSelection.SelectedShowdownVariant;
            ServiceProvider.TryGet<ISimulationClock>(out var previousClock);
            var clock = new Clock();
            var definition = ScriptableObject.CreateInstance<BrawlerDefinition>();
            definition.BaseHealth = 2000f;
            definition.BaseMoveSpeed = 5f;
            var modeObject = new GameObject("HUDModeTest");
            var playerObject = new GameObject("HUDPlayerTest");
            var botObject = new GameObject("HUDBotTest");
            DuoTeamCommandHUD hud = null;
            try
            {
                SceneSelection.SelectedMode = GameModeId.SoloShowdown;
                SceneSelection.SelectedShowdownVariant = ShowdownVariant.Duo;
                ServiceProvider.Register<ISimulationClock>(clock);
                var mode = modeObject.AddComponent<SoloShowdownMode>();
                var player = playerObject.AddComponent<BrawlerController>();
                var bot = botObject.AddComponent<BrawlerController>();
                player.InitializeFromMatchmaking(definition, TeamType.Solo1);
                bot.InitializeFromMatchmaking(definition, TeamType.Solo1);
                playerObject.AddComponent<PlayerCommandSource>();
                mode.RegisterBrawler(player);
                mode.RegisterBrawler(bot);
                DuoTeamCommandHUD.EnsureInstalled();
                hud = Object.FindObjectOfType<DuoTeamCommandHUD>();
                DuoTeamCommandHUD.EnsureInstalled();
                Assert.AreEqual(1, Object.FindObjectsOfType<DuoTeamCommandHUD>().Length);
                hud.SendMessage("Update");
                var root = hud.transform.Find("DuoCommandCanvas/DuoCommands");
                Assert.IsTrue(root.gameObject.activeSelf);
                var wheel = root.Find("CommandWheel").gameObject;
                Assert.IsFalse(wheel.activeSelf);
                root.Find("CommandToggle").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.IsTrue(wheel.activeSelf);
                wheel.transform.Find("REGROUP").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.IsFalse(wheel.activeSelf);
                Assert.IsTrue(mode.TryGetActiveDuoTeamCommand(bot, clock.CurrentTick, out var directive));
                Assert.AreEqual(DuoTeamCommandType.Regroup, directive.CommandType);

                SceneSelection.SelectedShowdownVariant = ShowdownVariant.Solo;
                hud.SendMessage("Update");
                Assert.IsFalse(root.gameObject.activeSelf);
            }
            finally
            {
                if (hud != null) Object.DestroyImmediate(hud.gameObject);
                Object.DestroyImmediate(botObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(modeObject);
                Object.DestroyImmediate(definition);
                ServiceProvider.Unregister<ISimulationClock>(clock);
                if (previousClock != null)
                    ServiceProvider.Register<ISimulationClock>(previousClock);
                SceneSelection.SelectedMode = previousMode;
                SceneSelection.SelectedShowdownVariant = previousVariant;
            }
        }
    }
}
