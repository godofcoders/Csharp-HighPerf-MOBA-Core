using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIObjectiveSlotCommitment
    {
        private const uint DefaultCommitmentTicks = 24u;
        private const uint DefensiveCommitmentTicks = 30u;
        private const uint StaleCommitmentResetTicks = 120u;

        private AIObjectiveSlotRole _committedRole;
        private AIObjectiveType _objectiveType;
        private string _objectiveName;
        private uint _committedSinceTick;
        private uint _lastEvaluatedTick;
        private AIObjectiveSlotRole _lastDesiredRole;
        private uint _lastHoldTicks;
        private uint _lastRequiredTicks;
        private string _lastReason = "none";
        private bool _lastWasHolding;
        private bool _hasCommitment;

        public AIObjectiveSlotRole CommittedRole => _committedRole;
        public string LastDebugSummary
        {
            get
            {
                if (!_hasCommitment)
                    return "SlotCommit=None";

                if (_lastWasHolding)
                {
                    return
                        $"SlotCommit={_committedRole} Desired={_lastDesiredRole} " +
                        $"Hold={_lastHoldTicks}/{_lastRequiredTicks} Reason={_lastReason}";
                }

                return
                    $"SlotCommit={_committedRole} Desired={_lastDesiredRole} " +
                    $"Reason={_lastReason}";
            }
        }

        public AIObjectiveSlotRole SelectRole(
            AIObjectiveCandidate objective,
            BrawlerArchetype archetype,
            uint currentTick,
            out AIObjectiveSlotRole desiredRole)
        {
            desiredRole = AIObjectiveSlotUtility.GetObjectiveSlotRole(
                archetype,
                objective);

            if (!_hasCommitment)
                return Commit(objective, desiredRole, currentTick, "first");

            if (IsObjectiveChanged(objective))
                return Commit(objective, desiredRole, currentTick, "objective_changed");

            if (TicksSince(currentTick, _lastEvaluatedTick) > StaleCommitmentResetTicks)
                return Commit(objective, desiredRole, currentTick, "stale");

            if (_committedRole == desiredRole)
            {
                _lastEvaluatedTick = currentTick;
                UpdateDebugState(desiredRole, "same", 0u, 0u, false);
                return _committedRole;
            }

            if (AllowsUrgentSwitch(_committedRole, desiredRole, objective.ControlState))
                return Commit(objective, desiredRole, currentTick, "urgent");

            uint heldTicks = TicksSince(currentTick, _committedSinceTick);
            uint requiredTicks = GetRequiredCommitmentTicks(
                _committedRole,
                desiredRole);

            if (heldTicks >= requiredTicks)
                return Commit(objective, desiredRole, currentTick, "held");

            _lastEvaluatedTick = currentTick;
            UpdateDebugState(desiredRole, "holding", heldTicks, requiredTicks, true);
            return _committedRole;
        }

        public void Reset()
        {
            _committedRole = AIObjectiveSlotRole.Default;
            _objectiveType = AIObjectiveType.None;
            _objectiveName = null;
            _committedSinceTick = 0u;
            _lastEvaluatedTick = 0u;
            _lastDesiredRole = AIObjectiveSlotRole.Default;
            _lastHoldTicks = 0u;
            _lastRequiredTicks = 0u;
            _lastReason = "none";
            _lastWasHolding = false;
            _hasCommitment = false;
        }

        private AIObjectiveSlotRole Commit(
            AIObjectiveCandidate objective,
            AIObjectiveSlotRole role,
            uint currentTick,
            string reason)
        {
            _committedRole = role;
            _objectiveType = objective.ObjectiveType;
            _objectiveName = objective.Name;
            _committedSinceTick = currentTick;
            _lastEvaluatedTick = currentTick;
            _hasCommitment = true;
            UpdateDebugState(role, reason, 0u, 0u, false);
            return _committedRole;
        }

        private void UpdateDebugState(
            AIObjectiveSlotRole desiredRole,
            string reason,
            uint holdTicks,
            uint requiredTicks,
            bool wasHolding)
        {
            _lastDesiredRole = desiredRole;
            _lastReason = reason;
            _lastHoldTicks = holdTicks;
            _lastRequiredTicks = requiredTicks;
            _lastWasHolding = wasHolding;
        }

        private bool IsObjectiveChanged(AIObjectiveCandidate objective)
        {
            if (objective.ObjectiveType != _objectiveType)
                return true;

            return !string.Equals(_objectiveName, objective.Name);
        }

        private static bool AllowsUrgentSwitch(
            AIObjectiveSlotRole committedRole,
            AIObjectiveSlotRole desiredRole,
            AIObjectiveControlState controlState)
        {
            if (desiredRole == AIObjectiveSlotRole.Breaker)
                return true;

            if (desiredRole == AIObjectiveSlotRole.Flank &&
                controlState == AIObjectiveControlState.EnemyControlled)
            {
                return true;
            }

            if (desiredRole == AIObjectiveSlotRole.Contest &&
                controlState == AIObjectiveControlState.Contested)
            {
                return committedRole == AIObjectiveSlotRole.Anchor ||
                       committedRole == AIObjectiveSlotRole.Perimeter;
            }

            return false;
        }

        private static uint GetRequiredCommitmentTicks(
            AIObjectiveSlotRole committedRole,
            AIObjectiveSlotRole desiredRole)
        {
            if (committedRole == AIObjectiveSlotRole.Anchor ||
                committedRole == AIObjectiveSlotRole.Perimeter)
            {
                return DefensiveCommitmentTicks;
            }

            if (desiredRole == AIObjectiveSlotRole.Anchor ||
                desiredRole == AIObjectiveSlotRole.Perimeter)
            {
                return DefensiveCommitmentTicks;
            }

            return DefaultCommitmentTicks;
        }

        private static uint TicksSince(uint currentTick, uint sinceTick)
        {
            return currentTick >= sinceTick
                ? currentTick - sinceTick
                : 0u;
        }
    }
}
