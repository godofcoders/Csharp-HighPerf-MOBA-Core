using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIReactiveListener
    {
        private readonly BrawlerController _self;
        private readonly BrawlerAIProfile _profile;
        private readonly AITargetInfo _targetInfo;
        private readonly AIReactiveMemory _reactiveMemory;

        public AIReactiveListener(
            BrawlerController self,
            BrawlerAIProfile profile,
            AITargetInfo targetInfo,
            AIReactiveMemory reactiveMemory)
        {
            _self = self;
            _profile = profile;
            _targetInfo = targetInfo;
            _reactiveMemory = reactiveMemory;
            DamageEventBus.OnDamageApplied += OnDamageApplied;
        }

        private void OnDamageApplied(DamageResultContext result)
        {
            if (_self == null || _self.State == null || _self.State.IsDead)
                return;

            var damage = result.Damage;

            if (!SpatialEntityUtility.IsSameEntity(damage.Target, _self))
                return;

            if (!SpatialEntityUtility.IsAlive(damage.Attacker))
                return;

            if (damage.Attacker.Team == _self.Team)
                return;

            uint currentTick = ServiceProvider.TryGet<ISimulationClock>(out ISimulationClock clock)
                ? clock.CurrentTick
                : 0u;

            _targetInfo.Remember(damage.Attacker, currentTick);
            _reactiveMemory?.RecordDamage(
                damage.Attacker,
                damage.HitPosition,
                damage.Direction,
                result.FinalDamageApplied,
                _self.State.MaxHealth.Value,
                currentTick);

            if (damage.Attacker is BrawlerController attacker &&
                attacker.State != null)
            {
                float attackerHealthRatio =
                    attacker.State.CurrentHealth /
                    UnityEngine.Mathf.Max(1f, attacker.State.MaxHealth.Value);

                float damagePressure =
                    result.FinalDamageApplied /
                    UnityEngine.Mathf.Max(1f, _self.State.MaxHealth.Value);

                AIOpponentModel.RecordDamage(
                    _self.Team,
                    attacker.EntityID,
                    _self.EntityID,
                    attackerHealthRatio,
                    damagePressure,
                    currentTick);
            }

            if (_profile != null && _profile.LogReactiveEvents)
            {
                UnityEngine.Debug.Log(
                    $"[AIReactive-{_self.name}] " +
                    $"attacker={damage.Attacker.EntityID} " +
                    $"damage={result.FinalDamageApplied:0.0} " +
                    $"tick={currentTick}");
            }
        }

        public void Dispose()
        {
            DamageEventBus.OnDamageApplied -= OnDamageApplied;
        }
    }
}
