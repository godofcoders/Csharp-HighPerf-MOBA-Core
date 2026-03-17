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
            if (ctx.Target == null)
                return;

            ctx.Target.TakeDamage(ctx.Damage);

            // 🔥 notify systems
            DamageEventBus.RaiseDamage(ctx);

            // 🔥 reward attacker
            if (ctx.Attacker != null)
            {
                ctx.Attacker.GrantSuperCharge(0.20f);
            }
        }
    }
}