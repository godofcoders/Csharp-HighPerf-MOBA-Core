using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIPerception
    {
        private readonly float _detectionRadius;
        private readonly uint _memoryDurationTicks;
        private readonly List<ISpatialEntity> _nearbyBuffer;
        private readonly AITargetScorer _targetScorer;

        public AIPerception(float detectionRadius, uint memoryDurationTicks, AITargetScorer targetScorer, int initialBufferCapacity = 32)
        {
            _detectionRadius = detectionRadius;
            _memoryDurationTicks = memoryDurationTicks;
            _targetScorer = targetScorer;
            _nearbyBuffer = new List<ISpatialEntity>(initialBufferCapacity);
        }

        public void UpdateTarget(BrawlerController self, AITargetInfo memory, uint currentTick)
        {
            if (self == null || self.State == null || self.State.IsDead)
            {
                memory.Clear();
                return;
            }

            if (SimulationClock.Grid == null)
            {
                memory.Clear();
                return;
            }

            SimulationClock.Grid.GetEntitiesInRadiusNonAlloc(self.Position, _detectionRadius, _nearbyBuffer);

            CompactAndFilterTargets(self);

            ISpatialEntity bestTarget = _targetScorer.SelectBestTarget(_nearbyBuffer, memory, currentTick);

            if (bestTarget != null)
            {
                memory.Remember(bestTarget, currentTick);
                AITeamMemory.ReportEnemySighting(self.Team, bestTarget.Position, currentTick);
                return;
            }

            if (memory.HasLiveTarget)
            {
                memory.LoseLiveTarget();
            }

            if (!memory.HasRecentMemory(currentTick, _memoryDurationTicks))
            {
                memory.Clear();
            }
        }

        private void CompactAndFilterTargets(BrawlerController self)
        {
            int writeIndex = 0;

            for (int i = 0; i < _nearbyBuffer.Count; i++)
            {
                ISpatialEntity entity = _nearbyBuffer[i];

                if (!IsValidTarget(self, entity))
                    continue;

                _nearbyBuffer[writeIndex] = entity;
                writeIndex++;
            }

            if (writeIndex < _nearbyBuffer.Count)
            {
                _nearbyBuffer.RemoveRange(writeIndex, _nearbyBuffer.Count - writeIndex);
            }
        }

        private bool IsValidTarget(BrawlerController self, ISpatialEntity entity)
        {
            if (entity == null)
                return false;

            if (entity.EntityID == self.EntityID)
                return false;

            if (entity.Team == self.Team)
                return false;

            if (entity is BrawlerController targetBrawler)
            {
                if (targetBrawler.State == null || targetBrawler.State.IsDead)
                    return false;

                if (targetBrawler.State.IsHiddenTo(self.Team))
                    return false;
            }

            return true;
        }
    }
}