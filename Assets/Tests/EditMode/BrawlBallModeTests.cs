using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class BrawlBallModeTests
    {
        private GameObject _modeObject;
        private GameObject _ballObject;

        [TearDown]
        public void TearDown()
        {
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>();
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>();

            if (_ballObject != null)
                Object.DestroyImmediate(_ballObject);

            if (_modeObject != null)
                Object.DestroyImmediate(_modeObject);
        }

        [Test]
        public void RuntimeObjective_UsesRegisteredLooseBallPosition()
        {
            BrawlBallMode mode = CreateMode();
            BrawlBallController ball = CreateBall(new Vector3(3f, 0.42f, -2f));

            mode.RegisterBall(ball);

            bool found = mode.TryGetRuntimeObjective(
                TeamType.Blue,
                AIObjectiveType.Ball,
                Vector3.zero,
                out AIObjectiveCandidate objective);

            Assert.IsTrue(found);
            Assert.AreEqual(AIObjectiveType.Ball, objective.ObjectiveType);
            Assert.AreEqual(ball.CurrentPosition, objective.Position);
            Assert.AreEqual(AIObjectiveControlState.Neutral, objective.ControlState);
        }

        [Test]
        public void RecordGoal_IncrementsScoreAndClearsCarrier()
        {
            BrawlBallMode mode = CreateMode();

            mode.RecordGoal(TeamType.Blue);

            Assert.AreEqual(1, mode.BlueGoals);
            Assert.AreEqual(0, mode.RedGoals);
            Assert.IsNull(mode.BallCarrier);
        }

        private BrawlBallMode CreateMode()
        {
            _modeObject = new GameObject("BrawlBallMode_Test");
            return _modeObject.AddComponent<BrawlBallMode>();
        }

        private BrawlBallController CreateBall(Vector3 position)
        {
            _ballObject = new GameObject("BrawlBall_TestBall");
            _ballObject.transform.position = position;
            return _ballObject.AddComponent<BrawlBallController>();
        }
    }
}
