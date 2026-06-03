using MOBA.Core.Simulation.AI;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class AIChaseDisengageMemoryTests
    {
        [Test]
        public void Evaluate_StartsAndContinuesCommittedLowHealthChase()
        {
            AIChaseDisengageMemory memory = new AIChaseDisengageMemory();
            AIChaseDisengageContext context = BaseContext();

            AIChaseDisengageDecision start = memory.Evaluate(context);
            context.Tick += 5u;
            AIChaseDisengageDecision follow = memory.Evaluate(context);

            Assert.IsTrue(start.ShouldChase);
            Assert.AreEqual("start_low", start.Reason);
            Assert.IsTrue(follow.IsActive);
            Assert.IsTrue(follow.ShouldChase);
            Assert.Greater(follow.ScoreDelta, 0f);
        }

        [Test]
        public void Evaluate_Disengages_WhenChaseExceedsTimebox()
        {
            AIChaseDisengageMemory memory = new AIChaseDisengageMemory();
            AIChaseDisengageContext context = BaseContext();
            context.MaxTicks = 10u;

            memory.Evaluate(context);
            context.Tick += 11u;

            AIChaseDisengageDecision decision = memory.Evaluate(context);

            Assert.IsTrue(decision.ShouldDisengage);
            Assert.AreEqual("timebox", decision.Reason);
            Assert.Less(decision.ScoreDelta, 0f);
        }

        [Test]
        public void Evaluate_DeniesBadMapChase_WhenTargetIsNotValuable()
        {
            AIChaseDisengageMemory memory = new AIChaseDisengageMemory();
            AIChaseDisengageContext context = BaseContext();
            context.PreserveLaneShape = true;
            context.TargetInBadMapPosition = true;

            AIChaseDisengageDecision decision = memory.Evaluate(context);

            Assert.IsTrue(decision.ShouldDisengage);
            Assert.IsFalse(decision.ShouldChase);
            Assert.AreEqual("deny_bad_map", decision.Reason);
        }

        [Test]
        public void Evaluate_DeniesOpeningChase_WhenTargetIsTooFar()
        {
            AIChaseDisengageMemory memory = new AIChaseDisengageMemory();
            AIChaseDisengageContext context = BaseContext();
            context.Distance = 12f;

            AIChaseDisengageDecision decision = memory.Evaluate(context);

            Assert.IsTrue(decision.ShouldDisengage);
            Assert.IsFalse(decision.ShouldChase);
            Assert.AreEqual("deny_distance", decision.Reason);
        }

        [Test]
        public void Evaluate_AllowsBadMapPressure_WhenTargetCarriesGems()
        {
            AIChaseDisengageMemory memory = new AIChaseDisengageMemory();
            AIChaseDisengageContext context = BaseContext();
            context.TargetHealthRatio = 0.75f;
            context.TargetCarriedGems = 4;
            context.PreserveLaneShape = true;
            context.TargetInBadMapPosition = true;

            AIChaseDisengageDecision decision = memory.Evaluate(context);

            Assert.IsTrue(decision.ShouldChase);
            Assert.AreEqual("start_valuable", decision.Reason);
        }

        private static AIChaseDisengageContext BaseContext()
        {
            return new AIChaseDisengageContext
            {
                TargetEntityId = 900,
                Tick = 100u,
                Distance = 5f,
                TargetHealthRatio = 0.20f,
                ChaseHealthThreshold = 0.35f,
                MaxChaseDistance = 8f,
                SelfCarriedGems = 0,
                TargetCarriedGems = 0,
                PreserveLaneShape = false,
                TargetInBadMapPosition = false,
                CommitTicks = 18u,
                MaxTicks = 90u,
                CooldownTicks = 30u,
                BreakDistanceMultiplier = 1.35f,
                CommitScoreBonus = 10f,
                DisengageScorePenalty = 42f,
                BadMapPenalty = 24f
            };
        }
    }
}
