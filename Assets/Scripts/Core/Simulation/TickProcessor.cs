using System;

namespace MOBA.Core.Simulation
{
    public static class SimulationTiming
    {
        public const int TicksPerSecond = 30;
        public const float TickDeltaSeconds = 1f / TicksPerSecond;

        public static uint SecondsToTicks(float seconds)
        {
            if (float.IsNaN(seconds) || seconds <= 0f)
                return 0u;

            double ticks = seconds * TicksPerSecond;
            if (double.IsInfinity(ticks) || ticks >= uint.MaxValue)
                return uint.MaxValue;

            return (uint)Math.Floor(ticks + 0.5d);
        }

        public static float TicksToSeconds(uint ticks)
        {
            return ticks * TickDeltaSeconds;
        }
    }

    public class TickProcessor
    {
        private readonly double _tickInterval;
        private double _accumulator;
        private uint _currentTick;

        public uint CurrentTick => _currentTick;
        public float TickDelta => (float)_tickInterval;

        public TickProcessor(int ticksPerSecond)
        {
            if (ticksPerSecond <= 0)
                throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));

            _tickInterval = 1d / ticksPerSecond;
        }

        // Returns how many ticks occurred this frame
        public int Update(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
                return 0;

            _accumulator += deltaTime;
            int ticksToProcess = 0;

            while (_accumulator >= _tickInterval)
            {
                _accumulator -= _tickInterval;
                _currentTick++;
                ticksToProcess++;
            }

            return ticksToProcess;
        }
    }
}
