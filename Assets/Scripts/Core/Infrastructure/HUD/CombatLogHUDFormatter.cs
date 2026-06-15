using System;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public static class CombatLogHUDFormatter
    {
        private const string GoodColor = "#7CFF8A";
        private const string BadColor = "#FF6B6B";
        private const string AssistColor = "#8FC7FF";
        private const string HealColor = "#7CFF8A";
        private const string StatusColor = "#DDB6FF";
        private const string NeutralColor = "#FFD166";

        public static bool ShouldDisplay(
            CombatLogEntry entry,
            bool showAssists,
            bool showFatalDamage,
            bool showStatusEvents,
            bool showHeals = true)
        {
            switch (entry.EventType)
            {
                case CombatLogEventType.Kill:
                case CombatLogEventType.SuperUsed:
                    return true;

                case CombatLogEventType.Assist:
                    return showAssists;

                case CombatLogEventType.Damage:
                    return showFatalDamage && entry.IsFatal;

                case CombatLogEventType.Heal:
                    return showHeals;

                case CombatLogEventType.StatusApplied:
                case CombatLogEventType.StatusRemoved:
                    return showStatusEvents;

                default:
                    return false;
            }
        }

        public static string FormatFeedLine(
            CombatLogEntry entry,
            Func<int, string> entityLabelResolver,
            Func<int, TeamType> entityTeamResolver,
            TeamType localTeam,
            int localEntityId,
            bool includeTick,
            bool useLocalPerspectiveLabels,
            bool useRichText)
        {
            string prefix = includeTick ? $"[{entry.Tick}] " : string.Empty;
            string tag = GetEventTag(entry.EventType);
            string tagColor = GetEventColor(entry, entityTeamResolver, localTeam);

            if (useRichText)
                tag = WrapColor($"<b>{tag}</b>", tagColor);

            string source = ResolveDisplayLabel(
                entry.SourceEntityId,
                entityLabelResolver,
                entityTeamResolver,
                localTeam,
                localEntityId,
                useLocalPerspectiveLabels);

            string target = ResolveDisplayLabel(
                entry.TargetEntityId,
                entityLabelResolver,
                entityTeamResolver,
                localTeam,
                localEntityId,
                useLocalPerspectiveLabels);

            return prefix + tag + " " + FormatMessage(entry, source, target);
        }

        public static string FormatLine(
            CombatLogEntry entry,
            Func<int, string> entityLabelResolver,
            bool includeTick)
        {
            string prefix = includeTick ? $"[{entry.Tick}] " : string.Empty;
            string source = ResolveEntityLabel(entry.SourceEntityId, entityLabelResolver);
            string target = ResolveEntityLabel(entry.TargetEntityId, entityLabelResolver);

            switch (entry.EventType)
            {
                case CombatLogEventType.Kill:
                    return prefix + $"{source} eliminated {target}";

                case CombatLogEventType.Assist:
                    return prefix + $"{source} assisted on {target}";

                case CombatLogEventType.Damage:
                    return entry.IsFatal
                        ? prefix + $"{source} finished {target}"
                        : prefix + $"{source} hit {target} for {RoundPositive(entry.Value)}";

                case CombatLogEventType.Heal:
                    return prefix + $"{source} healed {target} for {RoundPositive(entry.Value)}";

                case CombatLogEventType.StatusApplied:
                    return prefix + $"{source} applied {entry.StatusEffectType} to {target}";

                case CombatLogEventType.StatusRemoved:
                    return prefix + $"{entry.StatusEffectType} ended on {target}";

                case CombatLogEventType.SuperUsed:
                    return prefix + $"{source} used Super";

                default:
                    return prefix + $"{source} affected {target}";
            }
        }

        private static string FormatMessage(
            CombatLogEntry entry,
            string source,
            string target)
        {
            switch (entry.EventType)
            {
                case CombatLogEventType.Kill:
                    return $"{source} eliminated {target}";

                case CombatLogEventType.Assist:
                    return $"{source} assisted on {target}";

                case CombatLogEventType.Damage:
                    return entry.IsFatal
                        ? $"{source} finished {target}"
                        : $"{source} hit {target} for {RoundPositive(entry.Value)}";

                case CombatLogEventType.Heal:
                    return $"{source} healed {target} for {RoundPositive(entry.Value)}";

                case CombatLogEventType.StatusApplied:
                    return $"{source} applied {entry.StatusEffectType} to {target}";

                case CombatLogEventType.StatusRemoved:
                    return $"{entry.StatusEffectType} ended on {target}";

                case CombatLogEventType.SuperUsed:
                    return $"{source} used Super";

                default:
                    return $"{source} affected {target}";
            }
        }

        private static string ResolveDisplayLabel(
            int entityId,
            Func<int, string> entityLabelResolver,
            Func<int, TeamType> entityTeamResolver,
            TeamType localTeam,
            int localEntityId,
            bool useLocalPerspectiveLabels)
        {
            string rawLabel = ResolveEntityLabel(entityId, entityLabelResolver);

            if (!useLocalPerspectiveLabels ||
                entityId == 0 ||
                localTeam == TeamType.Neutral ||
                localEntityId == 0)
            {
                return rawLabel;
            }

            if (entityId == localEntityId)
                return "You";

            TeamType entityTeam = entityTeamResolver != null
                ? entityTeamResolver(entityId)
                : TeamType.Neutral;

            string compactLabel = StripTeamPrefix(rawLabel);

            if (entityTeam == TeamType.Neutral)
                return compactLabel;

            return TeamRelationshipUtility.AreAllies(entityTeam, localTeam)
                ? $"Ally {compactLabel}"
                : $"Enemy {compactLabel}";
        }

        private static string GetEventTag(CombatLogEventType eventType)
        {
            switch (eventType)
            {
                case CombatLogEventType.Kill:
                case CombatLogEventType.Damage:
                    return "[KO]";

                case CombatLogEventType.Assist:
                    return "[AST]";

                case CombatLogEventType.Heal:
                    return "[HEAL]";

                case CombatLogEventType.StatusApplied:
                case CombatLogEventType.StatusRemoved:
                    return "[FX]";

                case CombatLogEventType.SuperUsed:
                    return "[SUPER]";

                default:
                    return "[INFO]";
            }
        }

        private static string GetEventColor(
            CombatLogEntry entry,
            Func<int, TeamType> entityTeamResolver,
            TeamType localTeam)
        {
            if (entry.EventType == CombatLogEventType.Heal)
                return HealColor;

            if (entry.EventType == CombatLogEventType.Assist)
                return AssistColor;

            if (entry.EventType == CombatLogEventType.StatusApplied ||
                entry.EventType == CombatLogEventType.StatusRemoved)
                return StatusColor;

            if (localTeam == TeamType.Neutral || entityTeamResolver == null)
                return NeutralColor;

            TeamType sourceTeam = entityTeamResolver(entry.SourceEntityId);
            TeamType targetTeam = entityTeamResolver(entry.TargetEntityId);

            if (TeamRelationshipUtility.AreAllies(sourceTeam, localTeam) &&
                TeamRelationshipUtility.AreEnemies(targetTeam, localTeam))
                return GoodColor;

            if (sourceTeam != TeamType.Neutral &&
                TeamRelationshipUtility.AreEnemies(sourceTeam, localTeam) &&
                TeamRelationshipUtility.AreAllies(targetTeam, localTeam))
                return BadColor;

            return NeutralColor;
        }

        private static string WrapColor(string text, string color)
        {
            return $"<color={color}>{text}</color>";
        }

        private static string StripTeamPrefix(string label)
        {
            if (label.StartsWith("Blue ", StringComparison.Ordinal))
                return label.Substring(5);

            if (label.StartsWith("Red ", StringComparison.Ordinal))
                return label.Substring(4);

            return label;
        }

        public static string ResolveEntityLabel(
            int entityId,
            Func<int, string> entityLabelResolver)
        {
            if (entityId == 0)
                return "Unknown";

            if (entityLabelResolver != null)
            {
                string resolved = entityLabelResolver(entityId);
                if (!string.IsNullOrWhiteSpace(resolved))
                    return resolved;
            }

            return $"Entity {entityId}";
        }

        private static int RoundPositive(float value)
        {
            if (value <= 0f)
                return 0;

            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
