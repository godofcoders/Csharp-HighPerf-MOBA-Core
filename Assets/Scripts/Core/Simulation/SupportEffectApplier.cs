using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Simulation
{
    public static class SupportEffectApplier
    {
        public static bool Apply(in SupportEffectRequest request)
        {
            if (request.Target == null || request.Target.State == null)
                return false;

            switch (request.EffectType)
            {
                case SupportEffectType.Heal:
                    float beforeHealth = request.Target.State.CurrentHealth;
                    request.Target.State.Heal(request.Magnitude, request.Source, true);
                    float healingDone = request.Target.State.CurrentHealth - beforeHealth;
                    if (healingDone > 0f)
                    {
                        AIReportCardTracker.RecordHealingDone(
                            request.Source,
                            request.Target,
                            healingDone,
                            false,
                            AIReportCardTracker.GetCurrentTickOrZero());
                    }
                    return true;

                case SupportEffectType.MoveSpeedBuff:
                    {
                        if (request.DurationSeconds <= 0f)
                            return false;

                        object source = request.SourceToken ?? (object)request.Source;

                        var modifier = new MovementModifier(
                            MovementModifierType.SpeedMultiplier,
                            request.Magnitude,
                            source);

                        request.Target.State.AddIncomingMovementModifier(modifier);

                        return true;
                    }

                case SupportEffectType.Shield:
                    request.Target.State.AddShield(request.Magnitude);
                    return true;

                default:
                    return false;
            }
        }

    }
}
