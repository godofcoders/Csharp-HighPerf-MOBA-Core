using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Simulation
{
    public sealed class CombatEventRouter
    {
        private const uint AssistWindowTicks = 180;
        private const uint ThreatForgetTicks = 240;

        public CombatEventRouter()
        {
            DamageEventBus.OnDamageApplied += OnDamageApplied;
        }

        private void OnDamageApplied(DamageResultContext result)
        {
            var damage = result.Damage;
            if (damage.Target is not BrawlerController victim ||
                victim.State == null)
                return;

            if (damage.Attacker == null)
                return;

            uint currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            int attackerId = damage.Attacker.EntityID;

            victim.State.ThreatTracker.AddThreat(attackerId, damage.Damage, currentTick);
            victim.State.AssistTracker.RecordHit(attackerId, damage.Damage, currentTick);

            victim.State.ThreatTracker.ClearExpired(currentTick, ThreatForgetTicks);

            if (result.WasFatal)
            {
                var assists = victim.State.AssistTracker.GetAssistContributors(currentTick, AssistWindowTicks, attackerId);

                var combatLog = ServiceProvider.Get<ICombatLogService>();
                combatLog.AddEntry(CombatLogEntry.CreateKill(currentTick, attackerId, victim.EntityID));

                for (int i = 0; i < assists.Count; i++)
                {
                    combatLog.AddEntry(CombatLogEntry.CreateAssist(currentTick, assists[i], victim.EntityID));
                }

                ListPool<int>.Release(assists);
            }
        }

        public void Dispose()
        {
            DamageEventBus.OnDamageApplied -= OnDamageApplied;
        }
    }
}