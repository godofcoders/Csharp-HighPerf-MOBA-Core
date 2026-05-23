using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIReactiveMemoryTests
    {
        private sealed class DummySpatialEntity : ISpatialEntity
        {
            public int EntityID { get; set; }
            public Vector3 Position { get; set; }
            public float CollisionRadius => 0.5f;
            public TeamType Team { get; set; }

            public void TakeDamage(float amount)
            {
            }
        }

        [Test]
        public void RecordDamage_RemembersAttackerAndPressure()
        {
            AIReactiveMemory memory = new AIReactiveMemory();
            DummySpatialEntity attacker = new DummySpatialEntity
            {
                EntityID = 42,
                Position = new Vector3(4f, 0f, 0f),
                Team = TeamType.Red
            };

            memory.RecordDamage(
                attacker,
                Vector3.zero,
                Vector3.right,
                250f,
                1000f,
                100u);

            Assert.IsTrue(memory.TryGetRecentAttacker(100u, 45u, out ISpatialEntity recentAttacker));
            Assert.AreSame(attacker, recentAttacker);
            Assert.AreEqual(42, memory.LastAttackerId);
            Assert.Greater(memory.GetDamagePressure(100u, 45u), 0f);
        }

        [Test]
        public void GetDamagePressure_DecaysAndExpires()
        {
            AIReactiveMemory memory = new AIReactiveMemory();
            DummySpatialEntity attacker = new DummySpatialEntity
            {
                EntityID = 7,
                Position = new Vector3(0f, 0f, 4f),
                Team = TeamType.Red
            };

            memory.RecordDamage(
                attacker,
                Vector3.zero,
                Vector3.forward,
                500f,
                1000f,
                10u);

            float immediatePressure = memory.GetDamagePressure(10u, 40u);
            float agedPressure = memory.GetDamagePressure(30u, 40u);

            Assert.Greater(immediatePressure, agedPressure);
            Assert.Greater(agedPressure, 0f);
            Assert.AreEqual(0f, memory.GetDamagePressure(51u, 40u));
            Assert.IsFalse(memory.TryGetRecentAttacker(51u, 40u, out _));
        }
    }
}
