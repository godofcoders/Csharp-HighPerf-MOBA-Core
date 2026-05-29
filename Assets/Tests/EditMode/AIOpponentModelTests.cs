using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIOpponentModelTests
    {
        [SetUp]
        public void SetUp()
        {
            AIOpponentModel.ResetForTests();
        }

        [Test]
        public void RecordMovementSample_LearnsLateralDodgeBias()
        {
            AIOpponentModel.RecordMovementSample(
                TeamType.Blue,
                200,
                observerPosition: Vector3.zero,
                opponentPosition: new Vector3(6f, 0f, 0f),
                opponentHealthRatio: 1f,
                hasObjectivePoint: false,
                objectivePoint: Vector3.zero,
                objectiveRadius: 4.5f,
                currentTick: 1u);

            AIOpponentModel.RecordMovementSample(
                TeamType.Blue,
                200,
                observerPosition: Vector3.zero,
                opponentPosition: new Vector3(6f, 0f, -2f),
                opponentHealthRatio: 1f,
                hasObjectivePoint: false,
                objectivePoint: Vector3.zero,
                objectiveRadius: 4.5f,
                currentTick: 31u);

            Assert.IsTrue(AIOpponentModel.TryGetSnapshot(
                TeamType.Blue,
                200,
                31u,
                360u,
                out AIOpponentHabitSnapshot snapshot));

            Assert.AreEqual(AIOpponentDodgeBias.Right, snapshot.DodgeBias);
            Assert.Greater(snapshot.DodgeConfidence, 0.10f);
        }

        [Test]
        public void RecordDamage_TracksTargetPreferenceAndLowHealthGreed()
        {
            AIOpponentModel.RecordDamage(
                TeamType.Blue,
                opponentEntityId: 201,
                targetEntityId: 10,
                opponentHealthRatio: 0.25f,
                normalizedDamagePressure: 0.30f,
                currentTick: 5u);

            Assert.IsTrue(AIOpponentModel.TryGetSnapshot(
                TeamType.Blue,
                201,
                5u,
                360u,
                out AIOpponentHabitSnapshot snapshot));

            Assert.AreEqual(10, snapshot.PreferredTargetEntityId);
            Assert.Greater(snapshot.TargetPreferenceConfidence, 0.15f);
            Assert.Greater(snapshot.LowHealthGreed, 0.30f);
            Assert.Greater(snapshot.Aggression, 0.20f);
        }

        [Test]
        public void RecordMovementSample_TracksObjectiveNeglect()
        {
            for (uint tick = 1u; tick <= 90u; tick += 15u)
            {
                AIOpponentModel.RecordMovementSample(
                    TeamType.Blue,
                    202,
                    observerPosition: Vector3.zero,
                    opponentPosition: new Vector3(12f, 0f, 0f),
                    opponentHealthRatio: 1f,
                    hasObjectivePoint: true,
                    objectivePoint: Vector3.zero,
                    objectiveRadius: 4.5f,
                    currentTick: tick);
            }

            float neglect = AIOpponentModel.GetMaxObjectiveNeglect(TeamType.Blue, 90u);
            Assert.Greater(neglect, 0.15f);
        }

        [Test]
        public void ApplyDodgeHabitToVelocity_AddsLearnedLateralLead()
        {
            AIOpponentHabitSnapshot snapshot = new AIOpponentHabitSnapshot(
                opponentEntityId: 203,
                lastUpdatedTick: 1u,
                lastPosition: new Vector3(6f, 0f, 0f),
                estimatedVelocity: Vector3.zero,
                aggression: 0f,
                dodgeLeftBias: 0f,
                dodgeRightBias: 0.75f,
                dodgeConfidence: 0.75f,
                dodgeBias: AIOpponentDodgeBias.Right,
                preferredTargetEntityId: 0,
                targetPreferenceConfidence: 0f,
                lowHealthGreed: 0f,
                objectiveNeglect: 0f);

            Vector3 adjusted = AIOpponentModel.ApplyDodgeHabitToVelocity(
                shooterPosition: Vector3.zero,
                targetPosition: new Vector3(6f, 0f, 0f),
                baseVelocity: Vector3.zero,
                snapshot: snapshot,
                strength: 1f);

            Assert.Less(adjusted.z, -0.1f);
        }
    }
}
