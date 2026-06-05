namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIIdleHesitationContext
    {
        public readonly uint Tick;
        public readonly AIActionScore ChosenAction;
        public readonly bool HasLiveTarget;
        public readonly bool HasRecentTargetMemory;
        public readonly bool HasDestination;
        public readonly bool HasDanger;
        public readonly uint RecoveryTicks;
        public readonly uint CooldownTicks;
        public readonly float LowScoreThreshold;

        public AIIdleHesitationContext(
            uint tick,
            AIActionScore chosenAction,
            bool hasLiveTarget,
            bool hasRecentTargetMemory,
            bool hasDestination,
            bool hasDanger,
            uint recoveryTicks,
            uint cooldownTicks,
            float lowScoreThreshold)
        {
            Tick = tick;
            ChosenAction = chosenAction;
            HasLiveTarget = hasLiveTarget;
            HasRecentTargetMemory = hasRecentTargetMemory;
            HasDestination = hasDestination;
            HasDanger = hasDanger;
            RecoveryTicks = recoveryTicks == 0u ? 1u : recoveryTicks;
            CooldownTicks = cooldownTicks == 0u ? 1u : cooldownTicks;
            LowScoreThreshold = lowScoreThreshold;
        }
    }

    public readonly struct AIIdleHesitationDecision
    {
        public readonly bool IsHesitating;
        public readonly bool ShouldRecover;
        public readonly uint ElapsedTicks;
        public readonly string Reason;

        public AIIdleHesitationDecision(
            bool isHesitating,
            bool shouldRecover,
            uint elapsedTicks,
            string reason)
        {
            IsHesitating = isHesitating;
            ShouldRecover = shouldRecover;
            ElapsedTicks = elapsedTicks;
            Reason = string.IsNullOrEmpty(reason) ? "none" : reason;
        }

        public static AIIdleHesitationDecision None(string reason)
        {
            return new AIIdleHesitationDecision(false, false, 0u, reason);
        }

        public string GetDebugSummary(uint nextRecoveryTick)
        {
            if (!IsHesitating && !ShouldRecover)
                return $"Idle=None Reason={Reason} Next={nextRecoveryTick}";

            return
                $"Idle={(ShouldRecover ? "Recover" : "Watch")} " +
                $"Elapsed={ElapsedTicks} " +
                $"Reason={Reason} " +
                $"Next={nextRecoveryTick}";
        }
    }

    public sealed class AIIdleHesitationMemory
    {
        private uint _hesitationStartedTick;
        private uint _nextRecoveryTick;
        private uint _lastRecoveryTick;
        private string _lastReason = "none";

        public uint LastRecoveryTick => _lastRecoveryTick;
        public uint NextRecoveryTick => _nextRecoveryTick;
        public string LastReason => _lastReason;

        public void Reset()
        {
            _hesitationStartedTick = 0u;
            _nextRecoveryTick = 0u;
            _lastRecoveryTick = 0u;
            _lastReason = "none";
        }

        public AIIdleHesitationDecision Evaluate(
            in AIIdleHesitationContext context)
        {
            if (!IsHesitationCandidate(context, out string reason))
            {
                _hesitationStartedTick = 0u;
                _lastReason = reason;
                return AIIdleHesitationDecision.None(reason);
            }

            if (context.Tick < _nextRecoveryTick)
            {
                _lastReason = "cooldown";
                return AIIdleHesitationDecision.None("cooldown");
            }

            if (_hesitationStartedTick == 0u)
                _hesitationStartedTick = context.Tick;

            uint elapsed = context.Tick - _hesitationStartedTick;
            if (elapsed < context.RecoveryTicks)
            {
                _lastReason = reason;
                return new AIIdleHesitationDecision(
                    true,
                    false,
                    elapsed,
                    reason);
            }

            _lastRecoveryTick = context.Tick;
            _nextRecoveryTick = context.Tick + context.CooldownTicks;
            _hesitationStartedTick = 0u;
            _lastReason = reason;

            return new AIIdleHesitationDecision(
                true,
                true,
                elapsed,
                reason);
        }

        public string GetDebugSummary()
        {
            if (_lastRecoveryTick == 0u)
                return $"Idle=None Reason={_lastReason} Next={_nextRecoveryTick}";

            return
                $"Idle=Recovered " +
                $"Tick={_lastRecoveryTick} " +
                $"Reason={_lastReason} " +
                $"Next={_nextRecoveryTick}";
        }

        private static bool IsHesitationCandidate(
            in AIIdleHesitationContext context,
            out string reason)
        {
            if (context.HasLiveTarget)
            {
                reason = "combat";
                return false;
            }

            if (context.HasRecentTargetMemory)
            {
                reason = "recent_target";
                return false;
            }

            if (context.HasDestination)
            {
                reason = "has_destination";
                return false;
            }

            if (context.HasDanger)
            {
                reason = "danger";
                return false;
            }

            if (IsEmergencyAction(context.ChosenAction.ActionType))
            {
                reason = "emergency_action";
                return false;
            }

            if (context.ChosenAction.Score > context.LowScoreThreshold &&
                context.ChosenAction.ActionType != AIActionType.Wander &&
                context.ChosenAction.ActionType != AIActionType.None)
            {
                reason = "confident_action";
                return false;
            }

            reason = "no_target_no_destination";
            return true;
        }

        private static bool IsEmergencyAction(AIActionType actionType)
        {
            return actionType == AIActionType.Retreat ||
                   actionType == AIActionType.Evade ||
                   actionType == AIActionType.UseSuper ||
                   actionType == AIActionType.Peel;
        }
    }
}
