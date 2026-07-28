using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public static class PlayerQuestProgress
    {
        private const string QuestKeyPrefix = "MOBA.QuestProgress.";

        private static readonly Dictionary<string, int> _progress =
            new Dictionary<string, int>(16);

        public static QuestProgressSnapshot[] GetAllSnapshots()
        {
            QuestDefinition[] quests = QuestCatalog.All;
            QuestProgressSnapshot[] snapshots = new QuestProgressSnapshot[quests.Length];
            for (int i = 0; i < quests.Length; i++)
                snapshots[i] = GetSnapshot(quests[i]);

            return snapshots;
        }

        public static QuestProgressSnapshot GetSnapshot(QuestDefinition definition)
        {
            return new QuestProgressSnapshot(definition, GetProgress(definition));
        }

        public static int GetProgress(QuestDefinition definition)
        {
            if (definition == null)
                return 0;

            string key = BuildProgressKey(definition);
            if (_progress.TryGetValue(key, out int cached))
                return cached;

            int loaded = Mathf.Max(0, PlayerPrefs.GetInt(key, 0));
            int clamped = Mathf.Min(loaded, definition.TargetValue);
            _progress[key] = clamped;
            return clamped;
        }

        public static bool IsComplete(QuestDefinition definition)
        {
            return definition != null && GetProgress(definition) >= definition.TargetValue;
        }

        public static bool AddProgress(QuestDefinition definition, int amount)
        {
            if (definition == null || amount <= 0 || IsComplete(definition))
                return false;

            string key = BuildProgressKey(definition);
            int current = GetProgress(definition);
            int next = Mathf.Clamp(current + amount, 0, definition.TargetValue);
            if (next == current)
                return false;

            _progress[key] = next;
            PlayerPrefs.SetInt(key, next);
            PlayerPrefs.Save();
            return true;
        }

        public static void SetProgressForDebug(QuestDefinition definition, int value)
        {
            if (definition == null)
                return;

            string key = BuildProgressKey(definition);
            int clamped = Mathf.Clamp(value, 0, definition.TargetValue);
            _progress[key] = clamped;
            PlayerPrefs.SetInt(key, clamped);
            PlayerPrefs.Save();
        }

        private static string BuildProgressKey(QuestDefinition definition)
        {
            return QuestKeyPrefix + (definition != null ? definition.Id : "unknown");
        }
    }
}
