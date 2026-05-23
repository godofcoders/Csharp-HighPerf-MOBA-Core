using System.Collections.Generic;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIDangerMemoryTests
    {
        [Test]
        public void EvaluateThreats_DirectProjectileCreatesLateralAvoidance()
        {
            AIDangerMemory memory = new AIDangerMemory();
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            profile.DangerReactionTimeSeconds = 1f;
            profile.DangerPersonalSpace = 0.5f;

            List<GameplayThreatInfo> threats = new List<GameplayThreatInfo>
            {
                new GameplayThreatInfo
                {
                    Team = TeamType.Red,
                    Position = Vector3.zero,
                    Direction = Vector3.forward,
                    Radius = 0.85f,
                    Damage = 300f,
                    TimeToImpact = 0.1f,
                    IsProjectile = true
                }
            };

            memory.EvaluateThreats(
                Vector3.zero,
                0.5f,
                1000f,
                threats,
                profile,
                10u);

            Assert.IsTrue(memory.HasDanger);
            Assert.Greater(memory.Pressure, 0f);
            Assert.Greater(Mathf.Abs(memory.AvoidanceDirection.x), 0.5f);
            Assert.Less(Mathf.Abs(memory.AvoidanceDirection.z), 0.5f);
        }

        [Test]
        public void EvaluateThreats_NoThreatClearsDanger()
        {
            AIDangerMemory memory = new AIDangerMemory();
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();

            memory.EvaluateThreats(
                Vector3.zero,
                0.5f,
                1000f,
                new List<GameplayThreatInfo>
                {
                    new GameplayThreatInfo
                    {
                        Team = TeamType.Red,
                        Position = Vector3.zero,
                        Radius = 1f,
                        Damage = 100f,
                        TimeToImpact = 0f,
                        IsAreaHazard = true
                    }
                },
                profile,
                5u);

            Assert.IsTrue(memory.HasDanger);

            memory.EvaluateThreats(
                Vector3.zero,
                0.5f,
                1000f,
                new List<GameplayThreatInfo>(),
                profile,
                6u);

            Assert.IsFalse(memory.HasDanger);
            Assert.AreEqual(0f, memory.Pressure);
        }

        [Test]
        public void EvaluateThreats_SuperHighDamageThreatWinsOverWeakThreat()
        {
            AIDangerMemory memory = new AIDangerMemory();
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            profile.DangerReactionTimeSeconds = 1f;

            Vector3 weakPosition = new Vector3(0.3f, 0f, 0f);
            Vector3 superPosition = new Vector3(1.0f, 0f, 0f);

            List<GameplayThreatInfo> threats = new List<GameplayThreatInfo>
            {
                new GameplayThreatInfo
                {
                    Team = TeamType.Red,
                    Position = weakPosition,
                    Radius = 0.6f,
                    Damage = 80f,
                    TimeToImpact = 0.1f,
                    IsProjectile = true
                },
                new GameplayThreatInfo
                {
                    Team = TeamType.Red,
                    Position = superPosition,
                    Radius = 1.2f,
                    Damage = 700f,
                    TimeToImpact = 0f,
                    IsAreaHazard = true,
                    IsSuper = true
                }
            };

            memory.EvaluateThreats(
                Vector3.zero,
                0.5f,
                1000f,
                threats,
                profile,
                12u);

            Assert.IsTrue(memory.HasDanger);
            Assert.AreEqual(superPosition, memory.ThreatPosition);
            Assert.IsTrue(memory.PrimaryThreat.IsSuper);
        }
    }
}
