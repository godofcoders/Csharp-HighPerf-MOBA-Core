using MOBA.Core.Definitions;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIFailureRecoveryMemory
    {
        private const int AbilitySlotCount = 3;

        private readonly int[] _failedCastCounts = new int[AbilitySlotCount];
        private readonly uint[] _lastFailedCastTicks = new uint[AbilitySlotCount];
        private readonly uint[] _abilitySuppressedUntilTicks = new uint[AbilitySlotCount];

        private uint _nextNavigationRecoveryTick;
        private uint _lastRecoveryTick;
        private AIFailureRecoveryReason _lastRecoveryReason;
        private int _navigationRecoveryCount;
        private AbilitySlotType _lastSuppressedSlot;
        private uint _lastSuppressedUntilTick;

        public AIFailureRecoveryReason LastRecoveryReason => _lastRecoveryReason;
        public uint LastRecoveryTick => _lastRecoveryTick;
        public int NavigationRecoveryCount => _navigationRecoveryCount;
        public AbilitySlotType LastSuppressedSlot => _lastSuppressedSlot;
        public uint LastSuppressedUntilTick => _lastSuppressedUntilTick;

        public bool TryCreateNavigationRecovery(
            AIFailureRecoverySignal signal,
            BrawlerAIProfile profile,
            uint currentTick,
            out AIFailureRecoveryRequest request)
        {
            request = default;

            if (profile == null || !profile.EnableFailureRecovery || !signal.IsActive)
                return false;

            if (currentTick < _nextNavigationRecoveryTick)
                return false;

            if (!IsNavigationSignalStrongEnough(signal, profile))
                return false;

            _navigationRecoveryCount++;
            _lastRecoveryReason = signal.Reason;
            _lastRecoveryTick = currentTick;

            uint cooldownTicks = profile.FailureRecoveryCooldownTicks == 0u
                ? 1u
                : profile.FailureRecoveryCooldownTicks;

            _nextNavigationRecoveryTick = currentTick + cooldownTicks;

            request = new AIFailureRecoveryRequest
            {
                Reason = signal.Reason,
                Tick = currentTick,
                ConsecutiveCount = signal.ConsecutiveCount,
                RecoveryIndex = _navigationRecoveryCount,
                SideSign = (_navigationRecoveryCount % 2 == 0) ? -1 : 1,
                Destination = signal.Destination,
                DistanceToDestination = signal.DistanceToDestination
            };

            return true;
        }

        public void RecordAbilityResult(
            AbilitySlotType slotType,
            bool success,
            uint currentTick,
            BrawlerAIProfile profile)
        {
            int index = GetSlotIndex(slotType);
            if (index < 0)
                return;

            if (success)
            {
                _failedCastCounts[index] = 0;
                _lastFailedCastTicks[index] = 0u;
                _abilitySuppressedUntilTicks[index] = 0u;
                return;
            }

            if (profile == null || !profile.EnableFailureRecovery)
                return;

            uint memoryTicks = profile.FailedCastMemoryTicks == 0u
                ? 1u
                : profile.FailedCastMemoryTicks;

            if (_lastFailedCastTicks[index] == 0u ||
                currentTick - _lastFailedCastTicks[index] > memoryTicks)
            {
                _failedCastCounts[index] = 0;
            }

            _failedCastCounts[index]++;
            _lastFailedCastTicks[index] = currentTick;

            int limit = Mathf.Max(1, profile.FailedCastRecoveryLimit);
            if (_failedCastCounts[index] < limit)
                return;

            uint suppressionTicks = profile.FailedCastSuppressionTicks == 0u
                ? 1u
                : profile.FailedCastSuppressionTicks;

            _abilitySuppressedUntilTicks[index] = currentTick + suppressionTicks;
            _failedCastCounts[index] = 0;
            _lastRecoveryReason = AIFailureRecoveryReason.FailedCast;
            _lastRecoveryTick = currentTick;
            _lastSuppressedSlot = slotType;
            _lastSuppressedUntilTick = _abilitySuppressedUntilTicks[index];
        }

        public bool IsAbilitySuppressed(AbilitySlotType slotType, uint currentTick)
        {
            int index = GetSlotIndex(slotType);
            return index >= 0 && currentTick < _abilitySuppressedUntilTicks[index];
        }

        public string GetDebugSummary(uint currentTick)
        {
            if (_lastRecoveryReason == AIFailureRecoveryReason.None)
                return "Recovery=None";

            string suppressed = "None";
            for (int i = 0; i < AbilitySlotCount; i++)
            {
                if (currentTick >= _abilitySuppressedUntilTicks[i])
                    continue;

                suppressed = $"{GetSlotName(i)}->{_abilitySuppressedUntilTicks[i]}";
                break;
            }

            return
                $"Recovery={_lastRecoveryReason} " +
                $"tick={_lastRecoveryTick} " +
                $"nav={_navigationRecoveryCount} " +
                $"nextNav={_nextNavigationRecoveryTick} " +
                $"supp={suppressed}";
        }

        private static bool IsNavigationSignalStrongEnough(
            AIFailureRecoverySignal signal,
            BrawlerAIProfile profile)
        {
            switch (signal.Reason)
            {
                case AIFailureRecoveryReason.NavigationStall:
                    return signal.ConsecutiveCount >= Mathf.Max(1, profile.NavigationStuckSampleLimit);

                case AIFailureRecoveryReason.BlockedRoute:
                    return signal.ConsecutiveCount >= Mathf.Max(1, profile.BlockedRouteRecoveryLimit);

                case AIFailureRecoveryReason.StaleDestination:
                    return signal.DestinationAgeTicks >= profile.StaleDestinationRecoveryTicks &&
                           signal.ProgressDistance <= Mathf.Max(0f, profile.StaleDestinationProgressThreshold);

                default:
                    return false;
            }
        }

        private static int GetSlotIndex(AbilitySlotType slotType)
        {
            switch (slotType)
            {
                case AbilitySlotType.MainAttack:
                    return 0;

                case AbilitySlotType.Super:
                    return 1;

                case AbilitySlotType.Gadget:
                    return 2;

                default:
                    return -1;
            }
        }

        private static string GetSlotName(int index)
        {
            switch (index)
            {
                case 0:
                    return "Main";

                case 1:
                    return "Super";

                case 2:
                    return "Gadget";

                default:
                    return "?";
            }
        }
    }
}
