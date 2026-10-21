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

        public static uint SecondsToIntervalTicks(float seconds)
        {
            if (float.IsNaN(seconds) || seconds <= 0f)
                return 0u;

            return Math.Max(1u, SecondsToTicks(seconds));
        }
    }

    public sealed class SimulationStepSequence
    {
        public int StepCount { get; }
        public int NextStepIndex { get; private set; }
        public uint NextStepTick { get; private set; }
        public uint IntervalTicks { get; }
        public bool IsComplete => NextStepIndex >= StepCount;

        public SimulationStepSequence(
            int stepCount,
            uint firstStepTick,
            uint intervalTicks)
        {
            if (stepCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(stepCount));

            StepCount = stepCount;
            NextStepTick = firstStepTick;
            IntervalTicks = intervalTicks;
        }

        public bool TryTakeStep(uint currentTick, out int stepIndex)
        {
            stepIndex = -1;
            if (IsComplete || currentTick < NextStepTick)
                return false;

            stepIndex = NextStepIndex++;
            if (!IsComplete)
                NextStepTick = AddSaturated(NextStepTick, IntervalTicks);

            return true;
        }

        private static uint AddSaturated(uint left, uint right)
        {
            ulong sum = (ulong)left + right;
            return sum >= uint.MaxValue ? uint.MaxValue : (uint)sum;
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
