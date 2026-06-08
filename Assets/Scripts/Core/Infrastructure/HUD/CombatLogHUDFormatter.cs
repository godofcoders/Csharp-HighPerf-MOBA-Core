using System;
using MOBA.Core.Simulation;

namespace MOBA.Core.Infrastructure
{
    public static class CombatLogHUDFormatter
    {
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
