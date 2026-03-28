using MOBA.Core.Infrastructure;

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
                    request.Target.State.Heal(request.Magnitude);
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