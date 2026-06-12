using System;

namespace MOBA.Core.Simulation
{
    public class HyperchargeTracker
    {
        public bool IsActive { get; private set; }
        public float ChargePercent { get; private set; } // 0 to 1

        private uint _startTick;
        private uint _endTick;
        private uint _durationTicks;

        public void AddCharge(float amount)
        {
            if (IsActive)
                return;

            ChargePercent = Math.Min(1f, ChargePercent + amount);
        }

        public void Activate(uint startTick, float durationSeconds)
        {
            if (ChargePercent < 1f)
                return;

            if (durationSeconds <= 0f)
                durationSeconds = 5f;

            IsActive = true;
            ChargePercent = 0f;

            // Convert seconds → ticks via SimulationClock.SecondsToTicks, the
            // single source of truth for the conversion. Pinned by
            // HyperchargeTrackerTests.Activate_ConvertsDurationSeconds_ToTicks_AtTPS
            // and the zero-duration fallback test in the same fixture.
            uint durationTicks = SimulationClock.SecondsToTicks(durationSeconds);
            _startTick = startTick;
            _durationTicks = durationTicks;
            _endTick = startTick + durationTicks;
        }

        public void Tick(uint currentTick, Action onDeactivate)
        {
            if (IsActive && currentTick >= _endTick)
            {
                IsActive = false;
                onDeactivate?.Invoke();
            }
        }

        /// <summary>
        /// Resets the tracker in place: clears activity, charge, and the end
        /// tick. Preferred over allocating a new HyperchargeTracker instance
        /// because references held elsewhere stay valid. Mirrors the Reset
        /// pattern on SuperChargeTracker.
        /// </summary>
        public void Reset()
        {
            IsActive = false;
            ChargePercent = 0f;
            _startTick = 0;
            _endTick = 0;
            _durationTicks = 0;
        }

        public float GetRemainingSeconds(uint currentTick)
        {
            if (!IsActive || currentTick >= _endTick)
                return 0f;

            return (_endTick - currentTick) * SimulationClock.TickDeltaTime;
        }

        public float GetRemainingPercent(uint currentTick)
        {
            if (!IsActive || _durationTicks == 0)
                return 0f;

            if (currentTick <= _startTick)
                return 1f;

            if (currentTick >= _endTick)
                return 0f;

            return (float)(_endTick - currentTick) / _durationTicks;
        }
    }
}
