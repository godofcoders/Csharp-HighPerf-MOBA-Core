using System.Collections.Generic;

namespace MOBA.Core.Simulation.AI
{
    /// <summary>
    /// Tracks the current high-level action each AI bot is contributing to
    /// its team. This feeds soft role coordination; it never hard-locks
    /// behavior, so emergency actions can still win through utility scoring.
    /// </summary>
    public sealed class AITeamActionTracker
    {
        private const int ActionSlotCount = (int)AIActionType.Objective + 1;

        private struct ActionRecord
        {
            public AIActionType ActionType;
            public uint Tick;
        }

        private readonly int[] _actionCounts = new int[ActionSlotCount];
        private readonly Dictionary<int, ActionRecord> _botToAction = new Dictionary<int, ActionRecord>(16);
        private readonly List<int> _staleBotBuffer = new List<int>(16);

        public void ReportAction(int botEntityId, AIActionType actionType, uint currentTick)
        {
            if (botEntityId == 0)
                return;

            ClearAction(botEntityId);

            if (!IsTrackedAction(actionType))
                return;

            _botToAction[botEntityId] = new ActionRecord
            {
                ActionType = actionType,
                Tick = currentTick
            };
            _actionCounts[(int)actionType]++;
        }

        public void ClearAction(int botEntityId)
        {
            if (botEntityId == 0)
                return;

            if (!_botToAction.TryGetValue(botEntityId, out ActionRecord previousRecord))
                return;

            _botToAction.Remove(botEntityId);

            AIActionType previousAction = previousRecord.ActionType;
            if (!IsTrackedAction(previousAction))
                return;

            int index = (int)previousAction;
            _actionCounts[index] = _actionCounts[index] > 0
                ? _actionCounts[index] - 1
                : 0;
        }

        public int GetActionCount(AIActionType actionType, uint currentTick, uint maxAgeTicks)
        {
            if (!IsTrackedAction(actionType))
                return 0;

            PurgeStale(currentTick, maxAgeTicks);

            return _actionCounts[(int)actionType];
        }

        public int GetActionCountExcluding(
            AIActionType actionType,
            int excludedBotEntityId,
            uint currentTick,
            uint maxAgeTicks)
        {
            int count = GetActionCount(actionType, currentTick, maxAgeTicks);

            if (count <= 0 || excludedBotEntityId == 0)
                return count;

            if (_botToAction.TryGetValue(excludedBotEntityId, out ActionRecord record) &&
                record.ActionType == actionType)
            {
                count--;
            }

            return count > 0 ? count : 0;
        }

        public void Clear()
        {
            for (int i = 0; i < _actionCounts.Length; i++)
            {
                _actionCounts[i] = 0;
            }

            _botToAction.Clear();
            _staleBotBuffer.Clear();
        }

        private void PurgeStale(uint currentTick, uint maxAgeTicks)
        {
            _staleBotBuffer.Clear();

            foreach (var pair in _botToAction)
            {
                if ((currentTick - pair.Value.Tick) > maxAgeTicks)
                    _staleBotBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _staleBotBuffer.Count; i++)
            {
                ClearAction(_staleBotBuffer[i]);
            }
        }

        private static bool IsTrackedAction(AIActionType actionType)
        {
            return actionType > AIActionType.None &&
                   (int)actionType < ActionSlotCount;
        }
    }
}
