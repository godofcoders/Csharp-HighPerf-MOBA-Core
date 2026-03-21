namespace MOBA.Core.Simulation
{
    public sealed class StunEffect : IStatusEffectInstance
    {
        public StatusEffectType Type => StatusEffectType.Stun;
        public uint StartTick { get; private set; }
        public uint EndTick { get; private set; }

        public StunEffect(float durationSeconds, uint currentTick)
        {
            StartTick = currentTick;
            EndTick = currentTick + SecondsToTicks(durationSeconds);
        }

        public void Apply(BrawlerState state, uint currentTick)
        {
            state.IsStunned = true;

            state.EnterActionState(
                BrawlerActionStateType.Stunned,
                currentTick,
                EndTick - currentTick,
                false,
                false,
                false);
        }

        public void Tick(BrawlerState state, uint currentTick)
        {
            if (state.ActionState.StateType == BrawlerActionStateType.Stunned)
            {
                uint remaining = EndTick > currentTick ? EndTick - currentTick : 0;

                state.EnterActionState(
                    BrawlerActionStateType.Stunned,
                    currentTick,
                    remaining,
                    false,
                    false,
                    false);
            }
        }

        public void Remove(BrawlerState state, uint currentTick)
        {
            state.IsStunned = false;

            if (state.ActionState.StateType == BrawlerActionStateType.Stunned)
            {
                state.ClearActionState();
            }
        }

        public bool CanMerge(StatusEffectContext context)
        {
            return context.Type == StatusEffectType.Stun;
        }

        public void Merge(StatusEffectContext context, uint currentTick)
        {
            uint newEnd = currentTick + SecondsToTicks(context.Duration);
            if (newEnd > EndTick)
                EndTick = newEnd;
        }

        public bool IsExpired(uint currentTick)
        {
            return currentTick >= EndTick;
        }

        private static uint SecondsToTicks(float seconds)
        {
            return (uint)(seconds * 30f);
        }
    }
}