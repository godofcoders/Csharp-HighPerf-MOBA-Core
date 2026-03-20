using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation
{
    public interface IDamageService
    {
        void ApplyDamage(in DamageContext ctx);
    }

    public class DamageService : IDamageService
    {
        private const float DefaultSuperChargePerHit = 0.20f;

        public void ApplyDamage(in DamageContext ctx)
        {
            if (ctx.Target == null)
                return;

            float workingDamage = ctx.Damage;
            float shieldBefore = 0f;
            float shieldAfter = 0f;

            bool wasAliveBefore = true;
            BrawlerController targetBrawler = ctx.Target as BrawlerController;

            if (targetBrawler != null && targetBrawler.State != null)
            {
                wasAliveBefore = !targetBrawler.State.IsDead;
                shieldBefore = targetBrawler.State.ShieldHealth;
            }

            if (ctx.Attacker != null && ctx.Attacker.State != null)
            {
                float outgoingLifesteal = ctx.Attacker.State.OutgoingDamageModifiers.GetLifestealPercent();
                workingDamage = ApplyOutgoingDamage(ctx.Attacker, workingDamage);

                if (targetBrawler != null && targetBrawler.State != null)
                {
                    float remainingShield = targetBrawler.State.ShieldHealth;
                    workingDamage = targetBrawler.State.IncomingDamageModifiers.ApplyIncoming(workingDamage, ref remainingShield);
                    shieldAfter = remainingShield;

                    if (shieldAfter != targetBrawler.State.ShieldHealth)
                    {
                        float newShieldValue = shieldAfter;
                        float oldShieldValue = targetBrawler.State.ShieldHealth;
                        float delta = oldShieldValue - newShieldValue;
                        if (delta > 0f)
                        {
                            targetBrawler.State.ClearShield();
                            if (newShieldValue > 0f)
                                targetBrawler.State.AddShield(newShieldValue);
                        }
                    }

                    ctx.Target.TakeDamage(workingDamage);

                    if (outgoingLifesteal > 0f && workingDamage > 0f)
                    {
                        ctx.Attacker.State.Heal(workingDamage * outgoingLifesteal);
                    }
                }
                else
                {
                    ctx.Target.TakeDamage(workingDamage);
                }
            }
            else
            {
                if (targetBrawler != null && targetBrawler.State != null)
                {
                    float remainingShield = targetBrawler.State.ShieldHealth;
                    workingDamage = targetBrawler.State.IncomingDamageModifiers.ApplyIncoming(workingDamage, ref remainingShield);
                    shieldAfter = remainingShield;

                    if (shieldAfter != targetBrawler.State.ShieldHealth)
                    {
                        targetBrawler.State.ClearShield();
                        if (shieldAfter > 0f)
                            targetBrawler.State.AddShield(shieldAfter);
                    }
                }

                ctx.Target.TakeDamage(workingDamage);
            }

            bool wasFatal = false;

            if (targetBrawler != null && targetBrawler.State != null)
            {
                wasFatal = wasAliveBefore && targetBrawler.State.IsDead;
            }

            var result = new DamageResultContext
            {
                Damage = ctx,
                WasFatal = wasFatal,
                FinalDamageApplied = workingDamage,
                ShieldAbsorbed = shieldBefore - shieldAfter
            };

            DamageEventBus.RaiseDamageApplied(result);

            if (ctx.Attacker != null)
            {
                ctx.Attacker.GrantSuperCharge(DefaultSuperChargePerHit);
            }

            var combatLog = ServiceProvider.Get<ICombatLogService>();
            var currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            combatLog.AddEntry(CombatLogEntry.CreateDamage(currentTick, result));
        }

        private float ApplyOutgoingDamage(BrawlerController attacker, float damage)
        {
            float result = damage;

            // Reuse the same incoming pipeline style for outgoing amplification later.
            // For now, handle only amplification/reduction from outgoing collection.
            float dummyShield = 0f;
            result = attacker.State.OutgoingDamageModifiers.ApplyIncoming(result, ref dummyShield);
            return result;
        }
    }
}