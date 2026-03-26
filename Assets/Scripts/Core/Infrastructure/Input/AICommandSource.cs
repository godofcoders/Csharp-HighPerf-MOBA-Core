using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public class AICommandSource : IBrawlerCommandSource
    {
        private readonly BrawlerController _owner;

        public AICommandSource(BrawlerController owner)
        {
            _owner = owner;
        }

        public void CollectCommands(List<BrawlerCommand> output, uint currentTick)
        {
            // Example: simple attack spam
            output.Add(new BrawlerCommand
            {
                Type = BrawlerCommandType.MainAttack,
                Direction = _owner.transform.forward,
                Tick = currentTick
            });
        }
    }
}