using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public readonly struct AIIntentValidationResult
    {
        public readonly bool IsValid;
        public readonly Vector3 ResolvedDestination;
        public readonly string Reason;

        public AIIntentValidationResult(
            bool isValid,
            Vector3 resolvedDestination,
            string reason)
        {
            IsValid = isValid;
            ResolvedDestination = resolvedDestination;
            Reason = string.IsNullOrEmpty(reason) ? "none" : reason;
        }

        public static AIIntentValidationResult Valid(
            Vector3 resolvedDestination,
            string reason = "valid")
        {
            return new AIIntentValidationResult(true, resolvedDestination, reason);
        }

        public static AIIntentValidationResult Invalid(
            Vector3 resolvedDestination,
            string reason)
        {
            return new AIIntentValidationResult(false, resolvedDestination, reason);
        }
    }

    public static class AIIntentValidationUtility
    {
        public static AIIntentValidationResult ValidateGemPickupIntent(
            in AIGemPickupDecision decision,
            Vector3 selfPosition,
            BrawlerAIProfile profile,
            AStarSolver pathfinder)
        {
            if (!decision.HasPickup)
                return AIIntentValidationResult.Invalid(decision.Position, "gem_missing");

            if (!decision.ShouldPickup)
                return AIIntentValidationResult.Invalid(decision.Position, "gem_hold");

            if (!IsFinite(decision.Position))
                return AIIntentValidationResult.Invalid(selfPosition, "gem_position_invalid");

            float searchRadius = profile != null
                ? Mathf.Max(0.1f, profile.GemPickupSearchRadius)
                : Mathf.Max(0.1f, decision.Distance);

            Vector3 delta = decision.Position - selfPosition;
            delta.y = 0f;
            if (delta.sqrMagnitude > searchRadius * searchRadius * 1.44f)
                return AIIntentValidationResult.Invalid(decision.Position, "gem_outside_search");

            return IsWalkable(pathfinder, decision.Position)
                ? AIIntentValidationResult.Valid(decision.Position, "gem_valid")
                : AIIntentValidationResult.Invalid(decision.Position, "gem_unwalkable");
        }

        public static AIIntentValidationResult ValidateObjectiveIntent(
            in AIObjectiveCandidate objective,
            BrawlerAIProfile profile,
            AStarSolver pathfinder)
        {
            if (objective.ObjectiveType == AIObjectiveType.None)
                return AIIntentValidationResult.Invalid(objective.Position, "objective_none");

            if (!IsFinite(objective.Position))
                return AIIntentValidationResult.Invalid(Vector3.zero, "objective_position_invalid");

            if (objective.Radius <= 0f)
                return AIIntentValidationResult.Invalid(objective.Position, "objective_radius_invalid");

            Vector3 resolved = AIMapNavigationUtility.ResolveBudgetSafeDestination(
                pathfinder,
                profile,
                objective.Position);

            return IsWalkable(pathfinder, resolved)
                ? AIIntentValidationResult.Valid(resolved, "objective_valid")
                : AIIntentValidationResult.Invalid(resolved, "objective_unwalkable");
        }

        private static bool IsWalkable(AStarSolver pathfinder, Vector3 position)
        {
            if (pathfinder == null)
                return true;

            return pathfinder.IsWalkable(pathfinder.GetGridCoords(position));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
