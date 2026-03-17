using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public class AIPerception
    {
        private readonly float _detectionRadius;
        private readonly uint _memoryDurationTicks;
        private readonly List<ISpatialEntity> _nearbyBuffer;

        public AIPerception(float detectionRadius, uint memoryDurationTicks, int initialBufferCapacity = 32)
        {
            _detectionRadius = detectionRadius;
            _memoryDurationTicks = memoryDurationTicks;
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

            ISpatialEntity bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < _nearbyBuffer.Count; i++)
            {
                var entity = _nearbyBuffer[i];

                if (!IsValidTarget(self, entity))
                    continue;

                float score = ScoreTarget(self, memory, entity);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = entity;
                }
            }

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

        private float ScoreTarget(BrawlerController self, AITargetInfo memory, ISpatialEntity entity)
        {
            Vector3 delta = entity.Position - self.Position;
            float distSq = delta.sqrMagnitude;

            float score = -distSq;

            if (memory.HasLiveTarget && memory.Target != null && memory.Target.EntityID == entity.EntityID)
            {
                score += 20f;
            }

            if (entity is BrawlerController targetBrawler && targetBrawler.State != null)
            {
                float healthRatio = targetBrawler.State.CurrentHealth / Mathf.Max(1f, targetBrawler.State.MaxHealth.Value);
                score += (1f - healthRatio) * 10f;
            }

            return score;
        }
    }
}