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
        public void ShouldDisplay_CanIncludeHealsWhenConfigured()
        {
            CombatLogEntry heal = new CombatLogEntry
            {
                EventType = CombatLogEventType.Heal
            };

            Assert.IsTrue(CombatLogHUDFormatter.ShouldDisplay(
                heal,
                showAssists: true,
                showFatalDamage: false,
                showStatusEvents: false,
                showHeals: true));

            Assert.IsFalse(CombatLogHUDFormatter.ShouldDisplay(
                heal,
                showAssists: true,
                showFatalDamage: false,
                showStatusEvents: false,
                showHeals: false));
        }

        [Test]
        public void ShouldDisplay_IncludesBreakableDestroyedByDefault()
        {
            CombatLogEntry entry = new CombatLogEntry
            {
                EventType = CombatLogEventType.BreakableDestroyed
            };

            Assert.IsTrue(CombatLogHUDFormatter.ShouldDisplay(
                entry,
                showAssists: false,
                showFatalDamage: false,
                showStatusEvents: false,
                showHeals: false));
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

        [Test]
        public void FormatLine_FormatsHealEvents()
        {
            CombatLogEntry entry = new CombatLogEntry
            {
                EventType = CombatLogEventType.Heal,
                SourceEntityId = 10,
                TargetEntityId = 20,
                Value = 120.4f
            };

            string line = CombatLogHUDFormatter.FormatLine(
                entry,
                ResolveTestLabel,
                includeTick: false);

            Assert.AreEqual("Blue Colt healed Red Jessie for 120", line);
        }

        [Test]
        public void FormatLine_FormatsBreakableDestroyedEvents()
        {
            CombatLogEntry entry = new CombatLogEntry
            {
                EventType = CombatLogEventType.BreakableDestroyed,
                SourceEntityId = 10,
                TargetEntityId = 99
            };

            string line = CombatLogHUDFormatter.FormatLine(
                entry,
                ResolveTestLabel,
                includeTick: false);

            Assert.AreEqual("Blue Colt broke cover", line);
        }

        [Test]
        public void FormatFeedLine_UsesPerspectiveLabelsAndEventTags()
        {
            CombatLogEntry entry = new CombatLogEntry
            {
                EventType = CombatLogEventType.Kill,
                SourceEntityId = 10,
                TargetEntityId = 20
            };

            string line = CombatLogHUDFormatter.FormatFeedLine(
                entry,
                ResolveTestLabel,
                ResolveTestTeam,
                TeamType.Blue,
                localEntityId: 10,
                includeTick: false,
                useLocalPerspectiveLabels: true,
                useRichText: false);

            Assert.AreEqual("[KO] You eliminated Enemy Jessie", line);
        }

        [Test]
        public void FormatFeedLine_CanEmitRichTextEventTags()
        {
            CombatLogEntry entry = new CombatLogEntry
            {
                EventType = CombatLogEventType.Kill,
                SourceEntityId = 20,
                TargetEntityId = 10
            };

            string line = CombatLogHUDFormatter.FormatFeedLine(
                entry,
                ResolveTestLabel,
                ResolveTestTeam,
                TeamType.Blue,
                localEntityId: 10,
                includeTick: false,
                useLocalPerspectiveLabels: true,
                useRichText: true);

            Assert.AreEqual("<color=#FF6B6B><b>[KO]</b></color> Enemy Jessie eliminated You", line);
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

        private static TeamType ResolveTestTeam(int entityId)
        {
            switch (entityId)
            {
                case 10:
                    return TeamType.Blue;
                case 20:
                    return TeamType.Red;
                default:
                    return TeamType.Neutral;
            }
        }
    }
}
