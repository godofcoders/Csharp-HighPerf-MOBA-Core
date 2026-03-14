namespace MOBA.Core.Simulation
{
    public class TickProcessor
    {
        private float _tickRate; // e.g., 0.033f for 30Hz
        private float _accumulator;
        private uint _currentTick;

        public uint CurrentTick => _currentTick;

        public TickProcessor(int ticksPerSecond)
        {
            _tickRate = 1f / ticksPerSecond;
        }

        // Returns how many ticks occurred this frame
        public int Update(float deltaTime)
        {
            _accumulator += deltaTime;
            int ticksToProcess = 0;

            while (_accumulator >= _tickRate)
            {
                _accumulator -= _tickRate;
                _currentTick++;
                ticksToProcess++;
            }

            return ticksToProcess;
        }
    }
}