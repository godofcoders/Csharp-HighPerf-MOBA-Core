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
        public float Timestamp; // When was this pressed?
    }

    public class InputBuffer
    {
        private float _bufferWindow = 0.2f; // Command stays valid for 200ms
        private BufferedCommand _pendingCommand;

        public void Enqueue(InputCommandType type, Vector3 direction)
        {
            _pendingCommand = new BufferedCommand
            {
                Type = type,
                Direction = direction,
                Timestamp = Time.time
            };
        }

        public BufferedCommand Consume()
        {
            // If the command is too old, it's "stale" and ignored
            if (Time.time - _pendingCommand.Timestamp > _bufferWindow)
            {
                Clear();
            }

            var cmd = _pendingCommand;
            Clear(); // Once consumed, the buffer is empty
            return cmd;
        }

        public void Clear() => _pendingCommand = new BufferedCommand { Type = InputCommandType.None };
        public bool HasPending => _pendingCommand.Type != InputCommandType.None;
    }
}