using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AITeamCoordinator
    {
        private readonly BrawlerController _self;

        private const uint FocusMemoryTicks = 90;
        private const uint RegroupMemoryTicks = 120;
        private const uint PeelMemoryTicks = 60;

        public AITeamCoordinator(BrawlerController self)
        {
            _self = self;
        }

        public void UpdateTeamSignals(AITargetInfo targetInfo, uint currentTick)
        {
            if (_self == null || _self.State == null || _self.State.IsDead)
                return;

            if (targetInfo.HasLiveTarget && targetInfo.Target is BrawlerController targetBrawler)
            {
                AITeamBlackboard.ReportFocusTarget(_self.Team, targetBrawler, currentTick);
            }

            if (_self.State.ThreatTracker != null)
            {
                int highestThreatId = _self.State.ThreatTracker.GetHighestThreatTarget(currentTick, 240);
                if (highestThreatId != 0)
                {
                    AITeamBlackboard.ReportAllyUnderThreat(_self.Team, _self, currentTick);
                }
            }

            float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
            if (selfHealthRatio <= 0.30f)
            {
                AITeamBlackboard.ReportRegroupPoint(_self.Team, _self.Position, currentTick);
            }
        }

        public bool TryGetFocusTarget(uint currentTick, out BrawlerController target)
        {
            return AITeamBlackboard.TryGetFocusTarget(_self.Team, currentTick, FocusMemoryTicks, out target);
        }

        public bool TryGetRegroupPoint(uint currentTick, out Vector3 point)
        {
            return AITeamBlackboard.TryGetRegroupPoint(_self.Team, currentTick, RegroupMemoryTicks, out point);
        }

        public bool TryGetAllyUnderThreat(uint currentTick, out BrawlerController ally)
        {
            return AITeamBlackboard.TryGetAllyUnderThreat(_self.Team, currentTick, PeelMemoryTicks, out ally);
        }
    }
}