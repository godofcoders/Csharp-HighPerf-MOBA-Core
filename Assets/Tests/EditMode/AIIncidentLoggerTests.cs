using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIIncidentLoggerTests
    {
        [SetUp]
        public void SetUp()
        {
            AIIncidentLogger.ResetForTests();
        }

        [Test]
        public void Record_RateLimitsRepeatedIncidentTypePerBot()
        {
            AIIncidentLogger.Record(10, AIIncidentType.MovementStall, 100u, "first", 30u);
            AIIncidentLogger.Record(10, AIIncidentType.MovementStall, 110u, "ignored", 30u);

            AIIncidentSnapshot snapshot = AIIncidentLogger.GetLatestForBot(10);

            Assert.AreEqual(AIIncidentType.MovementStall, snapshot.Type);
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("first", snapshot.Detail);
        }

        [Test]
        public void Record_KeepsLatestIncidentAcrossTypes()
        {
            AIIncidentLogger.Record(10, AIIncidentType.MovementStall, 100u, "stall", 0u);
            AIIncidentLogger.Record(10, AIIncidentType.RouteBlocked, 120u, "route", 0u);

            AIIncidentSnapshot snapshot = AIIncidentLogger.GetLatestForBot(10);

            Assert.AreEqual(AIIncidentType.RouteBlocked, snapshot.Type);
            Assert.AreEqual("route", snapshot.Detail);
        }
    }
}
