using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    /// <summary>
    /// Stabilizes AI intent selection.
    ///
    /// Utility scoring answers:
    ///     "What action looks best right now?"
    ///
    /// Commitment answers:
    ///     "Should I actually switch to that action right now?"
    ///
    /// This prevents twitchy behavior where bots rapidly flip between
    /// Approach / HoldRange / Reposition / Search / Objective because scores
    /// move by tiny amounts every tick.
    /// </summary>
    public sealed class AIActionCommitment
    {
        private readonly BrawlerAIProfile _profile;

        private AIActionScore _committedAction;
        private uint _committedSinceTick;
        private bool _hasCommittedAction;

        public AIActionCommitment(BrawlerAIProfile profile)
        {
            _profile = profile;
            _committedAction = new AIActionScore(AIActionType.None, 0f);
            _committedSinceTick = 0;
            _hasCommittedAction = false;
        }

        public AIActionScore SelectAction(
            IReadOnlyList<AIActionScore> scores,
            uint currentTick,
            string ownerName = null)
        {
            AIActionScore rawBest = GetBestAction(scores);
            AIActionScore currentScore = _hasCommittedAction
                ? FindScore(scores, _committedAction.ActionType)
                : new AIActionScore(AIActionType.None, 0f);

            if (!_hasCommittedAction)
                return Commit(rawBest, currentTick, ownerName, "first_action");

            if (rawBest.Score < _profile.MinimumCommittedActionScore)
                return Commit(rawBest, currentTick, ownerName, "raw_best_too_low_but_no_better_option");

            if (_committedAction.ActionType == rawBest.ActionType)
            {
                _committedAction = rawBest;
                return _committedAction;
            }

            if (!IsCurrentActionStillValid(currentScore))
                return Commit(rawBest, currentTick, ownerName, "current_invalid");

            if (IsEmergencyOverride(rawBest))
                return Commit(rawBest, currentTick, ownerName, "emergency_override");

            uint heldTicks = currentTick - _committedSinceTick;
            uint requiredHoldTicks = GetRequiredCommitmentTicks(_committedAction.ActionType);

            bool heldLongEnough = heldTicks >= requiredHoldTicks;
            bool newActionClearlyBetter =
                rawBest.Score >= currentScore.Score + _profile.ActionSwitchScoreMargin;

            if (newActionClearlyBetter && heldLongEnough)
                return Commit(rawBest, currentTick, ownerName, "better_after_commitment");

            if (newActionClearlyBetter && IsSoftSwitchAllowed(_committedAction.ActionType, rawBest.ActionType))
                return Commit(rawBest, currentTick, ownerName, "soft_switch_allowed");

            // Keep the old action, but update its score to the current score
            // from this tick. This keeps debug output honest.
            _committedAction = currentScore;
            return _committedAction;
        }

        public void Reset()
        {
            _committedAction = new AIActionScore(AIActionType.None, 0f);
            _committedSinceTick = 0;
            _hasCommittedAction = false;
        }

        private AIActionScore Commit(
            AIActionScore action,
            uint currentTick,
            string ownerName,
            string reason)
        {
            if (_profile != null && _profile.LogActionCommitment)
            {
                Debug.Log(
                    $"[AICommit-{ownerName ?? "AI"}] " +
                    $"{_committedAction.ActionType}({_committedAction.Score:0.0}) -> " +
                    $"{action.ActionType}({action.Score:0.0}) reason={reason}");
            }

            _committedAction = action;
            _committedSinceTick = currentTick;
            _hasCommittedAction = true;
            return _committedAction;
        }

        private AIActionScore GetBestAction(IReadOnlyList<AIActionScore> scores)
        {
            AIActionScore best = new AIActionScore(AIActionType.Wander, 0f);

            if (scores == null)
                return best;

            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i].Score > best.Score)
                    best = scores[i];
            }

            return best;
        }

        private AIActionScore FindScore(
            IReadOnlyList<AIActionScore> scores,
            AIActionType actionType)
        {
            if (scores == null)
                return new AIActionScore(actionType, 0f);

            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i].ActionType == actionType)
                    return scores[i];
            }

            return new AIActionScore(actionType, 0f);
        }

        private bool IsCurrentActionStillValid(AIActionScore currentScore)
        {
            if (currentScore.ActionType == AIActionType.None)
                return false;

            return currentScore.Score >= _profile.MinimumCommittedActionScore;
        }

        private bool IsEmergencyOverride(AIActionScore action)
        {
            if (action.Score < _profile.EmergencyOverrideScore)
                return false;

            switch (action.ActionType)
            {
                case AIActionType.Retreat:
                case AIActionType.UseSuper:
                case AIActionType.Peel:
                    return true;

                default:
                    return false;
            }
        }

        private uint GetRequiredCommitmentTicks(AIActionType actionType)
        {
            return IsCombatAction(actionType)
                ? _profile.CombatActionCommitmentTicks
                : _profile.NonCombatActionCommitmentTicks;
        }

        private bool IsCombatAction(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.Reposition:
                case AIActionType.Retreat:
                case AIActionType.UseSuper:
                case AIActionType.Peel:
                    return true;

                default:
                    return false;
            }
        }

        private bool IsSoftSwitchAllowed(
            AIActionType currentAction,
            AIActionType nextAction)
        {
            // These actions are part of the same combat family.
            // Switching between them should be easier than switching from
            // Objective to Retreat or Search to Approach.
            if (IsCombatAction(currentAction) && IsCombatAction(nextAction))
                return true;

            // Search and Objective are both map-control behaviors.
            if ((currentAction == AIActionType.Search && nextAction == AIActionType.Objective) ||
                (currentAction == AIActionType.Objective && nextAction == AIActionType.Search))
                return true;

            return false;
        }
    }
}