using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    /// <summary>Fixed-capacity accumulation with a fixed merge window, so a continuous
    /// damage stream cannot extend a number's lifetime indefinitely.</summary>
    public sealed class CombatNumberBuffer
    {
        public const float MergeSeconds = 0.16f;
        public const float LifetimeSeconds = 0.85f;

        public struct Entry
        {
            public int EntityId;
            public bool Healing;
            public float Amount;
            public float StartedAt;
            public Vector3 Position;
        }

        private readonly Entry[] _entries;
        public int Capacity => _entries.Length;
        public Entry Get(int index) => _entries[index];
        public bool IsActive(int index, float now) => _entries[index].Amount > 0f &&
            now - _entries[index].StartedAt < LifetimeSeconds;

        public CombatNumberBuffer(int capacity = 48)
        {
            _entries = new Entry[Mathf.Max(1, capacity)];
        }

        public int Add(int entityId, bool healing, float amount, Vector3 position, float now)
        {
            if (entityId == 0 || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return -1;
            int free = -1;
            int oldest = 0;
            for (int i = 0; i < Capacity; i++)
            {
                Entry entry = _entries[i];
                if (IsActive(i, now) && entry.EntityId == entityId && entry.Healing == healing &&
                    now - entry.StartedAt <= MergeSeconds)
                {
                    entry.Amount += amount;
                    _entries[i] = entry;
                    return i;
                }
                if (!IsActive(i, now) && free < 0) free = i;
                if (entry.StartedAt < _entries[oldest].StartedAt) oldest = i;
            }
            int slot = free >= 0 ? free : oldest;
            _entries[slot] = new Entry { EntityId = entityId, Healing = healing, Amount = amount,
                Position = position, StartedAt = now };
            return slot;
        }

        public void Clear() => System.Array.Clear(_entries, 0, Capacity);
    }
}
