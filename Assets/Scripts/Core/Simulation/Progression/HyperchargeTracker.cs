using System;

namespace MOBA.Core.Simulation
{
    public class HyperchargeTracker
    {
        public bool IsActive { get; private set; }
        public float ChargePercent { get; private set; } // 0 to 1

        private uint _endTick;

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

            // Convert seconds → ticks using the simulation's actual tick rate.
            // Previously hardcoded `durationSeconds * 30f`, which broke silently if
            // the sim's TPS ever changed. Dividing by TickDeltaTime (1f / TPS) gives
            // the correct tick count for any TPS, and one source of truth stays in
            // SimulationClock.
            //
            // The `+ 0.5f` rounds-to-nearest before truncation. SimulationClock.TickDeltaTime
            // is 1f/30f, which is ~0.033333335f in IEEE float, so a naive
            // (uint)(seconds / TickDeltaTime) computes 59.9999... and truncates to 59
            // for a 2s input — designers typing "2.0s hypercharge duration" would
            // silently get 59 ticks instead of 60. Pinned by
            // HyperchargeTrackerTests.Activate_ConvertsDurationSeconds_ToTicks_AtTPS
            // and the zero-duration fallback test in the same fixture. Same fix
            // shape as BrawlerCooldowns.StartCooldown.
            uint durationTicks = (uint)(durationSeconds / SimulationClock.TickDeltaTime + 0.5f);
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
            _endTick = 0;
        }
    }
}