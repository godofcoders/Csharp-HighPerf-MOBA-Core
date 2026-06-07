using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;

namespace MOBA.Tests.EditMode
{
    public class CombatLogHUDFormatterTests
    {
        [Test]
        public void ShouldDisplay_IncludesKillAndAssistButFiltersDamageByDefault()
        {
            CombatLogEntry kill = new CombatLogEntry
            {
                EventType = CombatLogEventType.Kill
            };

            CombatLogEntry assist = new CombatLogEntry
            {
                EventType = CombatLogEventType.Assist
            };

            CombatLogEntry fatalDamage = new CombatLogEntry
            {
                EventType = CombatLogEventType.Damage,
                IsFatal = true
            };

            Assert.IsTrue(CombatLogHUDFormatter.ShouldDisplay(
                kill,
                showAssists: true,
                showFatalDamage: false,
                showStatusEvents: false));

            Assert.IsTrue(CombatLogHUDFormatter.ShouldDisplay(
                assist,
                showAssists: true,
                showFatalDamage: false,
                showStatusEvents: false));

            Assert.IsFalse(CombatLogHUDFormatter.ShouldDisplay(
                fatalDamage,
                showAssists: true,
                showFatalDamage: false,
                showStatusEvents: false));
        }

        [Test]
        public void ShouldDisplay_CanIncludeFatalDamageWhenConfigured()
        {
            CombatLogEntry fatalDamage = new CombatLogEntry
            {
                EventType = CombatLogEventType.Damage,
                IsFatal = true
            };

            Assert.IsTrue(CombatLogHUDFormatter.ShouldDisplay(
                fatalDamage,
                showAssists: true,
                showFatalDamage: true,
                showStatusEvents: false));
        }

        [Test]
        public void FormatLine_UsesResolvedLabelsForKillFeed()
        {
            CombatLogEntry entry = new CombatLogEntry
            {
                EventType = CombatLogEventType.Kill,
                SourceEntityId = 10,
                TargetEntityId = 20
            };

            string line = CombatLogHUDFormatter.FormatLine(
                entry,
                ResolveTestLabel,
                includeTick: false);

            Assert.AreEqual("Blue Colt eliminated Red Jessie", line);
        }

        [Test]
        public void FormatLine_FallsBackToEntityIdWhenLabelIsMissing()
        {
            CombatLogEntry entry = new CombatLogEntry
            {
                EventType = CombatLogEventType.Assist,
                SourceEntityId = 10,
                TargetEntityId = 99
            };

            string line = CombatLogHUDFormatter.FormatLine(
                entry,
                ResolveTestLabel,
                includeTick: true);

            Assert.AreEqual("[0] Blue Colt assisted on Entity 99", line);
        }

        private static string ResolveTestLabel(int entityId)
        {
            switch (entityId)
            {
                case 10:
                    return "Blue Colt";
                case 20:
                    return "Red Jessie";
                default:
                    return null;
            }
        }
    }
}
