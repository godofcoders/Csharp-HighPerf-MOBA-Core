using UnityEngine;

namespace MOBA.Core.Simulation
{
    public enum InputCommandType
    {
        None,
        MainAttack,
        Super,
        Gadget,
        Hypercharge
    }

    public struct BufferedCommand
    {
        public InputCommandType Type;
        public Vector3 Direction;
        public uint EnqueuedTick;
        public Vector3 TargetPoint;
        public bool HasTargetPoint;
    }

    public class InputBuffer
    {
        private readonly uint _bufferWindowTicks;
        private BufferedCommand _pendingCommand;

        public InputBuffer(float bufferWindowSeconds = 0.22f)
        {
            _bufferWindowTicks = SimulationClock.SecondsToTicks(Mathf.Max(0f, bufferWindowSeconds));
        }

        public void Enqueue(
            InputCommandType type,
            Vector3 direction,
            Vector3 targetPoint,
            bool hasTargetPoint,
            uint currentTick)
        {
            _pendingCommand = new BufferedCommand
            {
                Type = type,
                Direction = direction,
                EnqueuedTick = currentTick,
                TargetPoint = targetPoint,
                HasTargetPoint = hasTargetPoint
            };
        }

        public bool TryPeek(uint currentTick, out BufferedCommand command)
        {
            if (!HasPending || IsExpired(currentTick))
            {
                Clear();
                command = default;
                return false;
            }

            command = _pendingCommand;
            return true;
        }

        public bool TryConsume(uint currentTick, out BufferedCommand command)
        {
            if (!TryPeek(currentTick, out command))
                return false;

            Clear();
            return true;
        }

        public void Clear() => _pendingCommand = new BufferedCommand { Type = InputCommandType.None };
        public bool HasPending => _pendingCommand.Type != InputCommandType.None;

        private bool IsExpired(uint currentTick)
        {
            return HasPending && currentTick - _pendingCommand.EnqueuedTick > _bufferWindowTicks;
        }
    }
}
