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

            uint durationTicks = (uint)(durationSeconds * 30f);
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
    }
}