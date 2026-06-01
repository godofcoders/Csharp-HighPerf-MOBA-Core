using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIObjectiveMemoryRuntimeTests
    {
        private GameObject _objectiveObject;

        [SetUp]
        public void SetUp()
        {
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceProvider.Unregister<IAIRuntimeObjectiveProvider>();

            if (_objectiveObject != null)
                Object.DestroyImmediate(_objectiveObject);
        }

        [Test]
        public void TryGetBestObjective_UsesRuntimeProvider_WhenNoScenePointExists()
        {
            ServiceProvider.Register<IAIRuntimeObjectiveProvider>(
                new FakeRuntimeObjectiveProvider(
                    AIObjectiveType.Ball,
                    new Vector3(5f, 0f, 2f),
                    90f,
                    "RuntimeBall"));

            var memory = new AIObjectiveMemory();

            bool found = memory.TryGetBestObjective(
                Vector3.zero,
                AIObjectiveType.Ball,
                TeamType.Blue,
                out AIObjectiveCandidate objective);

            Assert.IsTrue(found);
            Assert.IsTrue(objective.IsRuntime);
            Assert.AreEqual(AIObjectiveType.Ball, objective.ObjectiveType);
            Assert.AreEqual("RuntimeBall", objective.Name);
        }

        [Test]
        public void TryGetBestObjective_CanPreferAuthoredPoint_OverRuntimeFallback()
        {
            _objectiveObject = new GameObject("AuthoredHotZone");
            _objectiveObject.transform.position = Vector3.one;

            AIObjectivePoint point = _objectiveObject.AddComponent<AIObjectivePoint>();
            point.ObjectiveType = AIObjectiveType.HotZone;
            point.Weight = 110f;

            ServiceProvider.Register<IAIRuntimeObjectiveProvider>(
                new FakeRuntimeObjectiveProvider(
                    AIObjectiveType.Ball,
                    new Vector3(2f, 0f, 0f),
                    40f,
                    "RuntimeBall"));

            var memory = new AIObjectiveMemory();
            memory.Register(point);

            bool found = memory.TryGetBestObjective(
                Vector3.zero,
                AIObjectiveType.HotZone,
                TeamType.Blue,
                out AIObjectiveCandidate objective);

            Assert.IsTrue(found);
            Assert.IsFalse(objective.IsRuntime);
            Assert.AreEqual(AIObjectiveType.HotZone, objective.ObjectiveType);
            Assert.AreEqual("AuthoredHotZone", objective.Name);
        }

        private sealed class FakeRuntimeObjectiveProvider : IAIRuntimeObjectiveProvider
        {
            private readonly AIObjectiveCandidate _objective;

            public FakeRuntimeObjectiveProvider(
                AIObjectiveType objectiveType,
                Vector3 position,
                float weight,
                string name)
            {
                _objective = new AIObjectiveCandidate(
                    objectiveType,
                    position,
                    weight,
                    2f,
                    name,
                    true);
            }

            public GameModeId ModeId => GameModeId.BrawlBall;

            public bool TryGetRuntimeObjective(
                TeamType team,
                AIObjectiveType preferredType,
                Vector3 selfPosition,
                out AIObjectiveCandidate objective)
            {
                objective = _objective;
                return team != TeamType.Neutral;
            }
        }
    }
}
