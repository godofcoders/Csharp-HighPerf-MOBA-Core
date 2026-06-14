using System.Collections.Generic;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Simulation
{
    public sealed class VisibilityTickSystem : ITickable
    {
        private readonly MapData _map;
        private readonly List<BrawlerController> _brawlers = new List<BrawlerController>(16);

        public VisibilityTickSystem(MapData map)
        {
            _map = map;
        }

        public void Tick(uint currentTick)
        {
            if (_map == null)
                return;

            CombatRegistry.GetBrawlersNonAlloc(_brawlers);
            VisibilitySystem.UpdateVisibility(_brawlers, _map);
        }
    }
}
