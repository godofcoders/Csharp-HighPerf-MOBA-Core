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
        private GameObject _goalObject;

        [TearDown]
        public void TearDown()
        {
            ServiceProvider.Unregister<IAIGameModeMacroStateProvider>();
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>();

            if (_ballObject != null)
                Object.DestroyImmediate(_ballObject);

            if (_goalObject != null)
                Object.DestroyImmediate(_goalObject);

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

        [Test]
        public void TryScoreGoalAt_RecordsRegisteredGoalZone()
        {
            BrawlBallMode mode = CreateMode();
            BrawlBallGoalController goal = CreateGoal(
                TeamType.Red,
                new Vector3(0f, 0f, 4f),
                new Vector3(4f, 1f, 2f));

            mode.RegisterGoal(goal);

            bool scored = mode.TryScoreGoalAt(
                new Vector3(0.5f, 0f, 4.25f),
                currentTick: 12u,
                out TeamType scoringTeam);

            Assert.IsTrue(scored);
            Assert.AreEqual(TeamType.Red, scoringTeam);
            Assert.AreEqual(0, mode.BlueGoals);
            Assert.AreEqual(1, mode.RedGoals);
        }

        [Test]
        public void TryScoreGoalAt_IgnoresBallOutsideGoalZone()
        {
            BrawlBallMode mode = CreateMode();
            BrawlBallGoalController goal = CreateGoal(
                TeamType.Blue,
                new Vector3(0f, 0f, 4f),
                new Vector3(4f, 1f, 2f));

            mode.RegisterGoal(goal);

            bool scored = mode.TryScoreGoalAt(
                new Vector3(0f, 0f, 1f),
                currentTick: 12u,
                out TeamType scoringTeam);

            Assert.IsFalse(scored);
            Assert.AreEqual(TeamType.Neutral, scoringTeam);
            Assert.AreEqual(0, mode.BlueGoals);
            Assert.AreEqual(0, mode.RedGoals);
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

        private BrawlBallGoalController CreateGoal(
            TeamType scoringTeam,
            Vector3 position,
            Vector3 zoneSize)
        {
            _goalObject = new GameObject("BrawlBall_TestGoal");
            _goalObject.transform.position = position;
            BrawlBallGoalController goal = _goalObject.AddComponent<BrawlBallGoalController>();
            goal.ConfigureForDebug(scoringTeam, zoneSize);
            return goal;
        }
    }
}
