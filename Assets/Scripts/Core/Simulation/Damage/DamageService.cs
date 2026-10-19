using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation.AI;
using UnityEngine;

namespace MOBA.Core.Simulation
{
    public interface IDamageService
    {
        void ApplyDamage(in DamageContext ctx);
    }

    public class DamageService : IDamageService
    {
        public void ApplyDamage(in DamageContext ctx)
        {
            if (!MatchStateUtility.IsCombatResolutionOpen())
                return;

            if (!SpatialEntityUtility.IsAlive(ctx.Target))
                return;

            BreakableObjectController targetBreakable = ctx.Target as BreakableObjectController;
            if (targetBreakable != null && !targetBreakable.CanReceiveDamage(ctx))
                return;

            float workingDamage = ctx.Damage;
            float healthBefore = 0f;
            float shieldBefore = 0f;
            float shieldAfter = 0f;

            bool wasAliveBefore = true;
            BrawlerController targetBrawler = ctx.Target as BrawlerController;

            if (targetBrawler != null && targetBrawler.State != null)
            {
                wasAliveBefore = !targetBrawler.State.IsDead;
                shieldBefore = targetBrawler.State.ShieldHealth;
                healthBefore = targetBrawler.State.CurrentHealth;
            }
            else if (targetBreakable != null)
            {
                wasAliveBefore = !targetBreakable.IsDestroyed;
            }

            // Kill-credit: stamp the current attacker before TakeDamage can
            // fire OnDeath, so final-hit stats/logs do not read stale credit.
            if (targetBrawler != null && targetBrawler.State != null)
                targetBrawler.State.LastAttacker = ctx.Attacker;

            if (ctx.Attacker != null && ctx.Attacker.State != null)
            {
                float outgoingLifesteal = ctx.Attacker.State.OutgoingDamageModifiers.GetLifestealPercent();
                workingDamage = ApplyAttackerDamageStatScale(ctx.Attacker, workingDamage);
                workingDamage = ApplyOutgoingDamage(ctx.Attacker, workingDamage);

                if (targetBrawler != null && targetBrawler.State != null)
                {
                    float remainingShield = targetBrawler.State.ShieldHealth;
                    workingDamage = ApplyIncomingWithShield(targetBrawler.State, workingDamage, ref remainingShield);
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
                        float beforeHeal = ctx.Attacker.State.CurrentHealth;
                        ctx.Attacker.State.Heal(workingDamage * outgoingLifesteal, ctx.Attacker, true);
                        float healingDone = ctx.Attacker.State.CurrentHealth - beforeHeal;
                        if (healingDone > 0f)
                        {
                            AIReportCardTracker.RecordHealingDone(
                                ctx.Attacker,
                                ctx.Attacker,
                                healingDone,
                                ctx.IsSuper,
                                AIReportCardTracker.GetCurrentTickOrZero());
                        }
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
                    workingDamage = ApplyIncomingWithShield(targetBrawler.State, workingDamage, ref remainingShield);
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
            else if (targetBreakable != null)
            {
                wasFatal = wasAliveBefore && targetBreakable.IsDestroyed;
            }

            var result = new DamageResultContext
            {
                Damage = ctx,
                WasFatal = wasFatal,
                FinalDamageApplied = workingDamage,
                ShieldAbsorbed = Mathf.Max(0f, shieldBefore - shieldAfter),
                HealthDamageApplied = targetBrawler != null && targetBrawler.State != null
                    ? Mathf.Max(0f, healthBefore - targetBrawler.State.CurrentHealth) : 0f
            };

            DamageEventBus.RaiseDamageApplied(result);

            // Data-driven super-charge. The old hardcoded 0.20f flat grant
            // is replaced by a push to the attacker's SuperChargeSources
            // (see BrawlerLoadout.NotifyDamageDealt). Brawlers with a
            // DamageDealtChargeSource configured grant per-damage; those
            // without one simply don't charge on damage — keeping the
            // single "one grant per landed hit" semantics while letting
            // per-brawler tuning live on ScriptableObject assets.
            if (ctx.Attacker != null && ctx.Attacker.State != null && workingDamage > 0f)
            {
                BrawlerState victimState = targetBrawler != null ? targetBrawler.State : null;
                ctx.Attacker.State.NotifyDamageDealt(workingDamage, victimState);
            }

            var combatLog = ServiceProvider.Get<ICombatLogService>();
            var currentTick = ServiceProvider.Get<ISimulationClock>().CurrentTick;
            combatLog.AddEntry(CombatLogEntry.CreateDamage(currentTick, result));

            if (targetBreakable != null && wasFatal)
                combatLog.AddEntry(CombatLogEntry.CreateBreakableDestroyed(currentTick, result));
        }

        private static float ApplyIncomingWithShield(BrawlerState target, float damage, ref float remainingShield)
        {
            float result = target.IncomingDamageModifiers.ApplyIncoming(damage, ref remainingShield);
            // AddShield grants a numeric pool (including support shields). A
            // separately-authored Shield modifier must not be required to use it.
            // If a modifier already absorbed damage, only the remainder reaches here.
            float absorbed = Mathf.Min(Mathf.Max(0f, result), Mathf.Max(0f, remainingShield));
            remainingShield -= absorbed;
            return result - absorbed;
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

        private float ApplyAttackerDamageStatScale(BrawlerController attacker, float damage)
        {
            if (attacker == null ||
                attacker.State == null ||
                attacker.Definition == null ||
                damage <= 0f)
            {
                return damage;
            }

            float authoredBaseDamage = Mathf.Max(1f, attacker.Definition.BaseDamage);
            float runtimeDamage = Mathf.Max(0f, attacker.State.Damage.Value);
            if (runtimeDamage <= 0f)
                return damage;

            return damage * (runtimeDamage / authoredBaseDamage);
        }

    }
}
