using UnityEngine;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AITeamCoordinator
    {
        private readonly BrawlerController _self;

        private const uint FocusMemoryTicks = 90;
        private const uint RegroupMemoryTicks = 30;
        private const uint PeelMemoryTicks = 60;
        private const uint EnemyHotspotMemoryTicks = 120;
        private const uint ThreatCenterMemoryTicks = 75;

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
                AITeamBlackboard.ReportEnemyHotspot(
                    _self.Team,
                    targetBrawler.Position,
                    currentTick,
                    1.25f);
            }

            if (_self.State.ThreatTracker != null)
            {
                int highestThreatId = _self.State.ThreatTracker.GetHighestThreatTarget(currentTick, 240);
                if (highestThreatId != 0)
                {
                    float threat = _self.State.ThreatTracker.GetThreat(highestThreatId, currentTick, 240);
                    float selfHealthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
                    float urgency = BuildPeelUrgency(threat, selfHealthRatio);

                    AITeamBlackboard.ReportAllyUnderThreat(_self.Team, _self, currentTick, urgency);

                    ISpatialEntity threatEntity = CombatRegistry.GetEntity(highestThreatId);
                    if (threatEntity != null && threatEntity.Team != _self.Team)
                    {
                        AITeamBlackboard.ReportThreatCenter(
                            _self.Team,
                            threatEntity.Position,
                            currentTick,
                            Mathf.Clamp(threat / 350f, 1f, 4f));
                    }
                }
            }

            float healthRatio = _self.State.CurrentHealth / Mathf.Max(1f, _self.State.MaxHealth.Value);
            if (healthRatio <= 0.30f)
            {
                AITeamBlackboard.ReportRegroupPoint(
                    _self.Team,
                    _self.Position,
                    currentTick,
                    BuildRegroupUrgency(healthRatio));
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

        public bool TryGetEnemyHotspot(uint currentTick, out Vector3 position, out float pressure)
        {
            return AITeamBlackboard.TryGetEnemyHotspot(
                _self.Team,
                currentTick,
                EnemyHotspotMemoryTicks,
                out position,
                out pressure);
        }

        public bool TryGetThreatCenter(uint currentTick, out Vector3 position, out float pressure)
        {
            return AITeamBlackboard.TryGetThreatCenter(
                _self.Team,
                currentTick,
                ThreatCenterMemoryTicks,
                out position,
                out pressure);
        }

        public void ReportTargetFocusCount(int targetEntityId)
        {
            if (_self == null)
                return;

            AITeamBlackboard.ReportTargetFocusCount(
                _self.Team,
                _self.EntityID,
                targetEntityId);
        }

        public void ClearTargetFocusCount()
        {
            if (_self == null)
                return;

            AITeamBlackboard.ClearTargetFocusCount(
                _self.Team,
                _self.EntityID);
        }

        public int GetTargetFocusCount(int targetEntityId)
        {
            if (_self == null)
                return 0;

            return AITeamBlackboard.GetTargetFocusCount(
                _self.Team,
                targetEntityId);
        }

        public int GetTargetFocusCountExcludingSelf(int targetEntityId)
        {
            if (_self == null)
                return 0;

            return AITeamBlackboard.GetTargetFocusCountExcluding(
                _self.Team,
                targetEntityId,
                _self.EntityID);
        }

        private float BuildPeelUrgency(float threat, float selfHealthRatio)
        {
            float healthUrgency = 1f - Mathf.Clamp01(selfHealthRatio);
            float gemUrgency = _self.State != null ? _self.State.CarriedGemCount * 0.35f : 0f;

            return 1f +
                   Mathf.Clamp(threat / 500f, 0f, 3f) +
                   healthUrgency * 2f +
                   gemUrgency;
        }

        private float BuildRegroupUrgency(float selfHealthRatio)
        {
            float healthUrgency = 1f - Mathf.Clamp01(selfHealthRatio);
            float gemUrgency = _self.State != null ? _self.State.CarriedGemCount * 0.25f : 0f;

            return 1f + healthUrgency * 2f + gemUrgency;
        }

    }
}
