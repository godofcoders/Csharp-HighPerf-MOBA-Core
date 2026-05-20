using System.Collections.Generic;

namespace MOBA.Core.Simulation.AI
{
    /// <summary>
    /// Tracks how many allies are currently focusing each enemy.
    ///
    /// This is a lightweight team-level signal used to reduce over-focusing
    /// and clumping. It does not forbid focus fire; it only gives scoring
    /// systems information about whether a target is already crowded.
    /// </summary>
    public sealed class AITeamFocusTracker
    {
        private readonly Dictionary<int, int> _focusCounts = new Dictionary<int, int>(16);
        private readonly Dictionary<int, int> _botToTarget = new Dictionary<int, int>(16);

        public void ReportFocus(int botEntityId, int targetEntityId)
        {
            ClearFocus(botEntityId);

            if (targetEntityId == 0)
                return;

            _botToTarget[botEntityId] = targetEntityId;

            if (!_focusCounts.ContainsKey(targetEntityId))
                _focusCounts[targetEntityId] = 0;

            _focusCounts[targetEntityId]++;
        }

        public void ClearFocus(int botEntityId)
        {
            if (!_botToTarget.TryGetValue(botEntityId, out int previousTargetId))
                return;

            _botToTarget.Remove(botEntityId);

            if (_focusCounts.TryGetValue(previousTargetId, out int count))
            {
                count--;

                if (count <= 0)
                    _focusCounts.Remove(previousTargetId);
                else
                    _focusCounts[previousTargetId] = count;
            }
        }

        public int GetFocusCount(int targetEntityId)
        {
            if (targetEntityId == 0)
                return 0;

            return _focusCounts.TryGetValue(targetEntityId, out int count)
                ? count
                : 0;
        }

        public void Clear()
        {
            _focusCounts.Clear();
            _botToTarget.Clear();
        }
    }
}