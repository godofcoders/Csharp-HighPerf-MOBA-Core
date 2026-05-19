namespace MOBA.Core.Simulation.AI
{
    public enum AITacticalMovementIntent
    {
        None,

        // Stay generally where we are.
        HoldPosition,

        // Move sideways around the target while staying in range.
        Strafe,

        // Move away from the target while keeping attack pressure.
        Kite,

        // Move toward the target to reach useful range.
        CloseGap,

        // Move to a better side angle.
        RepositionAngle,

        // Back away hard because survival is more important.
        EmergencyRetreat,

        // Move toward a teammate / safer formation point.
        Regroup
    }
}