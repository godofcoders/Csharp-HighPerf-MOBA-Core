using System;

namespace MOBA.Core.Simulation
{
    public sealed class SuperChargeTracker
    {
        public float ChargePercent { get; private set; } // 0..1

        public bool IsReady => ChargePercent >= 1f;

        public void AddCharge(float amount)
        {
            if (amount <= 0f)
                return;

            ChargePercent = Math.Min(1f, ChargePercent + amount);
        }

        public bool TryConsume()
        {
            if (!IsReady)
                return false;

            ChargePercent = 0f;
            return true;
        }

        public void Reset(bool readyOnSpawn = false)
        {
            ChargePercent = readyOnSpawn ? 1f : 0f;
        }
    }
}