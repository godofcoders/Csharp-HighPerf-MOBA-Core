using System.Collections.Generic;
using UnityEngine.Pool;

namespace MOBA.Core.Simulation.AI
{
    public sealed class ThreatTracker
    {
        private readonly Dictionary<int, ThreatEntry> _entries = new Dictionary<int, ThreatEntry>(8);

        public void AddThreat(int attackerEntityId, float amount, uint currentTick)
        {
            if (attackerEntityId == 0 || amount <= 0f)
                return;

            if (_entries.TryGetValue(attackerEntityId, out var entry))
            {
                entry.Threat += amount;
                entry.LastUpdatedTick = currentTick;
                _entries[attackerEntityId] = entry;
            }
            else
            {
                _entries[attackerEntityId] = new ThreatEntry
                {
                    AttackerEntityId = attackerEntityId,
                    Threat = amount,
                    LastUpdatedTick = currentTick
                };
            }
        }

        public float GetThreat(int attackerEntityId, uint currentTick, uint forgetAfterTicks)
        {
            if (!_entries.TryGetValue(attackerEntityId, out var entry))
                return 0f;

            uint age = currentTick - entry.LastUpdatedTick;
            if (age > forgetAfterTicks)
                return 0f;

            float decay = 1f - ((float)age / forgetAfterTicks);
            return entry.Threat * decay;
        }

        public int GetHighestThreatTarget(uint currentTick, uint forgetAfterTicks)
        {
            int bestId = 0;
            float bestThreat = 0f;

            foreach (var kvp in _entries)
            {
                float threat = GetThreat(kvp.Key, currentTick, forgetAfterTicks);
                if (threat > bestThreat)
                {
                    bestThreat = threat;
                    bestId = kvp.Key;
                }
            }

            return bestId;
        }

        public void ClearExpired(uint currentTick, uint forgetAfterTicks)
        {
            if (_entries.Count == 0)
                return;

            var toRemove = ListPool<int>.Get();
            foreach (var kvp in _entries)
            {
                if ((currentTick - kvp.Value.LastUpdatedTick) > forgetAfterTicks)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                _entries.Remove(toRemove[i]);
            }

            ListPool<int>.Release(toRemove);
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}