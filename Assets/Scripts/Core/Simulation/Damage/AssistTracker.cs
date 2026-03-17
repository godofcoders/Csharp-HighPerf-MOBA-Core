using System.Collections.Generic;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AssistTracker
    {
        private readonly Dictionary<int, AssistEntry> _entries = new Dictionary<int, AssistEntry>(8);

        public void RecordHit(int attackerEntityId, float damage, uint currentTick)
        {
            if (attackerEntityId == 0 || damage <= 0f)
                return;

            if (_entries.TryGetValue(attackerEntityId, out var entry))
            {
                entry.DamageContributed += damage;
                entry.LastHitTick = currentTick;
                _entries[attackerEntityId] = entry;
            }
            else
            {
                _entries[attackerEntityId] = new AssistEntry
                {
                    AttackerEntityId = attackerEntityId,
                    DamageContributed = damage,
                    LastHitTick = currentTick
                };
            }
        }

        public List<int> GetAssistContributors(uint currentTick, uint assistWindowTicks, int killerEntityId)
        {
            var result = ListPool<int>.Get();

            foreach (var kvp in _entries)
            {
                var entry = kvp.Value;
                if (entry.AttackerEntityId == killerEntityId)
                    continue;

                if ((currentTick - entry.LastHitTick) <= assistWindowTicks)
                {
                    result.Add(entry.AttackerEntityId);
                }
            }

            return result;
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}