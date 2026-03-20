using System.Collections.Generic;

namespace MOBA.Core.Simulation
{
    public interface ICombatLogService
    {
        void AddEntry(CombatLogEntry entry);
        IReadOnlyList<CombatLogEntry> GetRecentEntries();
        void Clear();
    }

    public sealed class CombatLogService : ICombatLogService
    {
        private readonly List<CombatLogEntry> _entries = new List<CombatLogEntry>(256);
        private const int MaxEntries = 512;

        public void AddEntry(CombatLogEntry entry)
        {
            if (_entries.Count >= MaxEntries)
            {
                _entries.RemoveAt(0);
            }

            _entries.Add(entry);
        }

        public IReadOnlyList<CombatLogEntry> GetRecentEntries()
        {
            return _entries;
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}