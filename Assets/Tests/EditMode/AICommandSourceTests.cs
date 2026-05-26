using System.Collections.Generic;
using MOBA.Core.Simulation;
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
}
