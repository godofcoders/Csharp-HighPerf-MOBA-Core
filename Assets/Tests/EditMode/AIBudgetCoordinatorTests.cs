using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AIBudgetCoordinatorTests
    {
        private BrawlerAIProfile _profile;

        [SetUp]
        public void SetUp()
        {
            AIBudgetCoordinator.ResetForTests();
            _profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            _profile.EnableAIBudgetEnforcement = true;
            _profile.AllowCriticalBudgetOverspend = true;
            _profile.MaxPerceptionScansPerTick = 2;
            _profile.MaxDangerRefreshesPerTick = 1;
            _profile.MaxMapResolvesPerTick = 1;
            _profile.MaxPathQueriesPerTick = 1;
        }

        [TearDown]
        public void TearDown()
        {
            AIBudgetCoordinator.ResetForTests();

            if (_profile != null)
            {
                Object.DestroyImmediate(_profile);
                _profile = null;
            }
        }

        [Test]
        public void PerceptionBudget_DefersLowPriorityWorkAfterLimit()
        {
            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePerceptionScan(10u, _profile, highPriority: false));
            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePerceptionScan(10u, _profile, highPriority: false));
            Assert.IsFalse(AIBudgetCoordinator.TryAcquirePerceptionScan(10u, _profile, highPriority: false));

            Assert.AreEqual(2, AIBudgetCoordinator.PerceptionGrants);
            Assert.AreEqual(1, AIBudgetCoordinator.PerceptionDeferrals);
            Assert.IsTrue(AIBudgetCoordinator.HasPressure(10u));
        }

        [Test]
        public void CriticalWork_CanSoftOverspendInsteadOfDeferring()
        {
            Assert.IsTrue(AIBudgetCoordinator.TryAcquireDangerRefresh(11u, _profile, highPriority: false));
            Assert.IsTrue(AIBudgetCoordinator.TryAcquireDangerRefresh(11u, _profile, highPriority: true));

            Assert.AreEqual(2, AIBudgetCoordinator.DangerGrants);
            Assert.AreEqual(0, AIBudgetCoordinator.DangerDeferrals);
            Assert.AreEqual(1, AIBudgetCoordinator.CriticalOverspends);
            Assert.IsTrue(AIBudgetCoordinator.HasPressure(11u));
        }

        [Test]
        public void CriticalWork_RespectsProfileWhenOverspendDisabled()
        {
            _profile.AllowCriticalBudgetOverspend = false;

            Assert.IsTrue(AIBudgetCoordinator.TryAcquireMapResolve(12u, _profile, highPriority: false));
            Assert.IsFalse(AIBudgetCoordinator.TryAcquireMapResolve(12u, _profile, highPriority: true));

            Assert.AreEqual(1, AIBudgetCoordinator.MapGrants);
            Assert.AreEqual(1, AIBudgetCoordinator.MapDeferrals);
            Assert.AreEqual(0, AIBudgetCoordinator.CriticalOverspends);
        }

        [Test]
        public void DisabledEnforcement_AlwaysGrantsAndDoesNotCreatePressure()
        {
            _profile.EnableAIBudgetEnforcement = false;
            _profile.MaxPathQueriesPerTick = 1;

            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePathQuery(13u, _profile, highPriority: false));
            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePathQuery(13u, _profile, highPriority: false));
            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePathQuery(13u, _profile, highPriority: false));

            Assert.AreEqual(3, AIBudgetCoordinator.PathGrants);
            Assert.AreEqual(0, AIBudgetCoordinator.PathDeferrals);
            Assert.IsFalse(AIBudgetCoordinator.HasPressure(13u));
        }

        [Test]
        public void Counters_ResetWhenTickChanges()
        {
            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePathQuery(14u, _profile, highPriority: false));
            Assert.IsFalse(AIBudgetCoordinator.TryAcquirePathQuery(14u, _profile, highPriority: false));

            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePathQuery(15u, _profile, highPriority: false));

            Assert.AreEqual(1, AIBudgetCoordinator.PathGrants);
            Assert.AreEqual(0, AIBudgetCoordinator.PathDeferrals);
            Assert.IsFalse(AIBudgetCoordinator.HasPressure(15u));
        }

        [Test]
        public void DebugSummary_ReportsGrantsDeferralsAndCriticalOverspends()
        {
            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePerceptionScan(16u, _profile, highPriority: false));
            Assert.IsTrue(AIBudgetCoordinator.TryAcquirePerceptionScan(16u, _profile, highPriority: false));
            Assert.IsFalse(AIBudgetCoordinator.TryAcquirePerceptionScan(16u, _profile, highPriority: false));
            Assert.IsTrue(AIBudgetCoordinator.TryAcquireDangerRefresh(16u, _profile, highPriority: false));
            Assert.IsTrue(AIBudgetCoordinator.TryAcquireDangerRefresh(16u, _profile, highPriority: true));

            string summary = AIBudgetCoordinator.GetDebugSummary(16u);

            StringAssert.Contains("sense=2/1", summary);
            StringAssert.Contains("danger=2/0", summary);
            StringAssert.Contains("critical=1", summary);
        }
    }
}
