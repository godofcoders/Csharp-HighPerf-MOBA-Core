using System;

namespace MOBA.Core.Simulation
{
    public class HyperchargeTracker
    {
        public bool IsActive { get; private set; }
        public float ChargePercent { get; private set; } // 0 to 1

        private uint _endTick;
        private readonly float _durationTicks = 5f * 30f; // 5 seconds at 30Hz

        public void AddCharge(float amount)
        {
            if (IsActive) return;
            ChargePercent = Math.Min(1f, ChargePercent + amount);
        }

        public void Activate(uint startTick)
        {
            if (ChargePercent < 1f) return;

            IsActive = true;
            ChargePercent = 0f;
            _endTick = startTick + (uint)_durationTicks;
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