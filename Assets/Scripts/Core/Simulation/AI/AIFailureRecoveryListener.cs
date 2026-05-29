using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIFailureRecoveryListener
    {
        private readonly BrawlerController _self;
        private readonly BrawlerAIProfile _profile;
        private readonly AIFailureRecoveryMemory _memory;

        public AIFailureRecoveryListener(
            BrawlerController self,
            BrawlerAIProfile profile,
            AIFailureRecoveryMemory memory)
        {
            _self = self;
            _profile = profile;
            _memory = memory;
            AbilityEventBus.OnAbilityEvent += OnAbilityEvent;
        }

        private void OnAbilityEvent(AbilityExecutionEvent evt)
        {
            if (_self == null || _self.State == null || _self.State.IsDead)
                return;

            if (evt.Source != _self)
                return;

            if (evt.EventType != AbilityEventType.CastSucceeded &&
                evt.EventType != AbilityEventType.CastFailed)
            {
                return;
            }

            uint previousRecoveryTick = _memory != null ? _memory.LastRecoveryTick : 0u;

            _memory?.RecordAbilityResult(
                evt.SlotType,
                evt.EventType == AbilityEventType.CastSucceeded,
                evt.Tick,
                _profile);

            if (evt.EventType == AbilityEventType.CastFailed &&
                _memory != null &&
                _memory.LastRecoveryTick == evt.Tick &&
                previousRecoveryTick != evt.Tick &&
                _memory.LastRecoveryReason == AIFailureRecoveryReason.FailedCast)
            {
                AIReportCardTracker.RecordFailureRecovery(
                    _self.EntityID,
                    AIFailureRecoveryReason.FailedCast,
                    evt.Tick);
            }
        }

        public void Dispose()
        {
            AbilityEventBus.OnAbilityEvent -= OnAbilityEvent;
        }
    }
}
