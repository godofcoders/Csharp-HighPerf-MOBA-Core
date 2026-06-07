using System.Collections.Generic;

namespace MOBA.Core.Simulation.AI
{
    public enum AIIncidentType
    {
        None,
        MovementStall,
        RouteBlocked,
        StaleDestination,
        PathBudgetStarvation,
        GemIntentInvalid,
        ObjectiveIntentInvalid,
        ObjectiveNeglect,
        TacticalStop
    }

    public readonly struct AIIncidentSnapshot
    {
        public readonly int BotEntityId;
        public readonly AIIncidentType Type;
        public readonly uint Tick;
        public readonly int Count;
        public readonly string Detail;

        public AIIncidentSnapshot(
            int botEntityId,
            AIIncidentType type,
            uint tick,
            int count,
            string detail)
        {
            BotEntityId = botEntityId;
            Type = type;
            Tick = tick;
            Count = count;
            Detail = string.IsNullOrEmpty(detail) ? "none" : detail;
        }

        public string GetDebugSummary()
        {
            if (Type == AIIncidentType.None)
                return "Incident=None";

            return
                $"Incident={Type} " +
                $"tick={Tick} " +
                $"count={Count} " +
                $"detail={Detail}";
        }
    }

    public static class AIIncidentLogger
    {
        private const uint DefaultRateLimitTicks = 30u;

        private struct IncidentRecord
        {
            public uint Tick;
            public int Count;
            public string Detail;
        }

        private static readonly Dictionary<int, Dictionary<AIIncidentType, IncidentRecord>> _records =
            new Dictionary<int, Dictionary<AIIncidentType, IncidentRecord>>(64);

        public static void Record(
            int botEntityId,
            AIIncidentType type,
            uint currentTick,
            string detail,
            uint rateLimitTicks = DefaultRateLimitTicks)
        {
            if (botEntityId == 0 || type == AIIncidentType.None)
                return;

            if (!_records.TryGetValue(botEntityId, out Dictionary<AIIncidentType, IncidentRecord> botRecords))
            {
                botRecords = new Dictionary<AIIncidentType, IncidentRecord>(8);
                _records[botEntityId] = botRecords;
            }

            botRecords.TryGetValue(type, out IncidentRecord record);
            if (record.Count > 0 &&
                rateLimitTicks > 0u &&
                currentTick >= record.Tick &&
                currentTick - record.Tick < rateLimitTicks)
            {
                return;
            }

            record.Tick = currentTick;
            record.Count++;
            record.Detail = string.IsNullOrEmpty(detail) ? "none" : detail;
            botRecords[type] = record;
        }

        public static AIIncidentSnapshot GetLatestForBot(int botEntityId)
        {
            if (!_records.TryGetValue(botEntityId, out Dictionary<AIIncidentType, IncidentRecord> botRecords))
                return new AIIncidentSnapshot(botEntityId, AIIncidentType.None, 0u, 0, "none");

            AIIncidentType bestType = AIIncidentType.None;
            IncidentRecord bestRecord = default;

            foreach (KeyValuePair<AIIncidentType, IncidentRecord> pair in botRecords)
            {
                if (bestType == AIIncidentType.None || pair.Value.Tick >= bestRecord.Tick)
                {
                    bestType = pair.Key;
                    bestRecord = pair.Value;
                }
            }

            return new AIIncidentSnapshot(
                botEntityId,
                bestType,
                bestRecord.Tick,
                bestRecord.Count,
                bestRecord.Detail);
        }

        public static string GetDebugSummary(int botEntityId)
        {
            return GetLatestForBot(botEntityId).GetDebugSummary();
        }

        public static void ResetForTests()
        {
            _records.Clear();
        }
    }
}
