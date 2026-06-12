using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class InputBufferTests
    {
        [Test]
        public void TryPeek_ReturnsCommand_InsideBufferWindow()
        {
            var buffer = new InputBuffer(bufferWindowSeconds: 0.10f);
            Vector3 direction = Vector3.forward;
            Vector3 targetPoint = new Vector3(3f, 0f, 4f);

            buffer.Enqueue(InputCommandType.MainAttack, direction, targetPoint, true, currentTick: 100);

            Assert.IsTrue(buffer.TryPeek(currentTick: 103, out BufferedCommand command));
            Assert.AreEqual(InputCommandType.MainAttack, command.Type);
            Assert.AreEqual(direction, command.Direction);
            Assert.AreEqual(targetPoint, command.TargetPoint);
            Assert.IsTrue(command.HasTargetPoint);
            Assert.IsTrue(buffer.HasPending);
        }

        [Test]
        public void TryPeek_ClearsCommand_AfterBufferWindow()
        {
            var buffer = new InputBuffer(bufferWindowSeconds: 0.10f);
            buffer.Enqueue(InputCommandType.Super, Vector3.right, Vector3.zero, false, currentTick: 10);

            Assert.IsFalse(buffer.TryPeek(currentTick: 14, out BufferedCommand command));
            Assert.AreEqual(InputCommandType.None, command.Type);
            Assert.IsFalse(buffer.HasPending);
        }

        [Test]
        public void TryConsume_ReturnsCommand_AndClearsPendingState()
        {
            var buffer = new InputBuffer(bufferWindowSeconds: 0.20f);
            buffer.Enqueue(InputCommandType.Gadget, Vector3.left, Vector3.zero, false, currentTick: 20);

            Assert.IsTrue(buffer.TryConsume(currentTick: 21, out BufferedCommand command));
            Assert.AreEqual(InputCommandType.Gadget, command.Type);
            Assert.IsFalse(buffer.HasPending);
        }

        [Test]
        public void Enqueue_OverwritesPreviousCommand()
        {
            var buffer = new InputBuffer(bufferWindowSeconds: 0.20f);
            buffer.Enqueue(InputCommandType.MainAttack, Vector3.forward, Vector3.zero, false, currentTick: 1);
            buffer.Enqueue(InputCommandType.Super, Vector3.right, new Vector3(2f, 0f, 0f), true, currentTick: 2);

            Assert.IsTrue(buffer.TryPeek(currentTick: 2, out BufferedCommand command));
            Assert.AreEqual(InputCommandType.Super, command.Type);
            Assert.AreEqual(Vector3.right, command.Direction);
            Assert.IsTrue(command.HasTargetPoint);
        }
    }
}
