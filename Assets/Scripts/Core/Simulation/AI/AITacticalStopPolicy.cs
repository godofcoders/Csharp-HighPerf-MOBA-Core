namespace MOBA.Core.Simulation.AI
{
    public readonly struct AITacticalStopDecision
    {
        public readonly bool CanHoldStop;
        public readonly bool ShouldAbandon;
        public readonly uint HeldTicks;
        public readonly string Reason;

        public AITacticalStopDecision(
            bool canHoldStop,
            bool shouldAbandon,
            uint heldTicks,
            string reason)
        {
            CanHoldStop = canHoldStop;
            ShouldAbandon = shouldAbandon;
            HeldTicks = heldTicks;
            Reason = string.IsNullOrEmpty(reason) ? "none" : reason;
        }

        public string GetDebugSummary()
        {
            return
                $"Stop={(CanHoldStop ? "Hold" : "Fallback")} " +
                $"Held={HeldTicks} " +
                $"Reason={Reason}";
        }
    }

    public static class AITacticalStopPolicy
    {
        public static AITacticalStopDecision Evaluate(
            bool isStopLegal,
            uint currentTick,
            uint stopStartedTick,
            uint maxHoldTicks,
            string reason)
        {
            uint safeMaxHoldTicks = maxHoldTicks == 0u ? 1u : maxHoldTicks;
            uint heldTicks = stopStartedTick == 0u || currentTick < stopStartedTick
                ? 0u
                : currentTick - stopStartedTick;

            if (!isStopLegal)
            {
                return new AITacticalStopDecision(
                    false,
                    true,
                    heldTicks,
                    $"illegal_{reason}");
            }

            if (heldTicks >= safeMaxHoldTicks)
            {
                return new AITacticalStopDecision(
                    false,
                    true,
                    heldTicks,
                    $"max_hold_{reason}");
            }

            return new AITacticalStopDecision(
                true,
                false,
                heldTicks,
                reason);
        }
    }
}
