using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIHumanizationController
    {
        private const uint FakeOutSalt = 0xA341316Cu;
        private const uint PressureSalt = 0xC8013EA4u;
        private const uint ReactionSalt = 0xAD90777Du;
        private const uint JitterSalt = 0x7E95761Eu;

        private readonly BrawlerAIProfile _profile;
        private readonly uint _seed;

        private AIActionType _activeFakeOutAction = AIActionType.None;
        private uint _activeFakeOutUntilTick;
        private uint _nextFakeOutCheckTick;

        private bool _pressureMistakeActive;
        private uint _pressureMistakeUntilTick;
        private uint _nextPressureMistakeCheckTick;

        private string _debugSummary = "Human=init";

        public AIHumanizationController(BrawlerAIProfile profile, uint ownerEntityId)
        {
            _profile = profile;
            _seed = Mix(ownerEntityId == 0u ? 1u : ownerEntityId, 0x9E3779B9u);
        }

        public string DebugSummary => _debugSummary;

        public uint GetReactionJitterTicks(uint currentTick, bool hasLiveTarget)
        {
            if (!IsEnabled() || _profile.HumanizationReactionJitterTicks == 0u)
                return 0u;

            uint rhythmWindow = hasLiveTarget ? 6u : 14u;
            uint rhythmTick = rhythmWindow > 0u ? currentTick / rhythmWindow : currentTick;
            float unit = Stable01(_seed, rhythmTick, hasLiveTarget ? ReactionSalt : ReactionSalt ^ 0x85EBCA6Bu);
            return (uint)Mathf.RoundToInt(unit * _profile.HumanizationReactionJitterTicks);
        }

        public void ShapeActionScores(
            IList<AIActionScore> scores,
            uint currentTick,
            bool hasLiveTarget,
            float healthRatio,
            bool hasDanger)
        {
            if (!IsEnabled())
            {
                _debugSummary = "Human=disabled";
                return;
            }

            if (scores == null || scores.Count == 0)
            {
                _debugSummary = "Human=no_scores";
                return;
            }

            bool hasEmergency = HasEmergencyScore(scores);

            UpdateFakeOutWindow(currentTick, hasLiveTarget, hasDanger || hasEmergency);
            UpdatePressureMistakeWindow(currentTick, hasLiveTarget, healthRatio, hasDanger, hasEmergency);

            float jitterMagnitude = _profile.HumanizationActionScoreJitter;
            float pressurePenalty = _pressureMistakeActive
                ? _profile.HumanizationPressureMistakePenalty * GetPressurePersonalityMultiplier()
                : 0f;

            for (int i = 0; i < scores.Count; i++)
            {
                AIActionScore current = scores[i];
                float shaped = current.Score;

                if (!IsEmergencyProtected(current))
                {
                    float signed = StableSigned(_seed, currentTick / 5u, JitterSalt ^ (uint)current.ActionType);
                    shaped += signed * jitterMagnitude;

                    if (_pressureMistakeActive)
                    {
                        if (IsOffensiveAction(current.ActionType))
                            shaped -= pressurePenalty;
                        else if (IsOverDefensiveExpression(current.ActionType))
                            shaped += pressurePenalty * 0.40f;
                    }
                }

                if (_activeFakeOutAction == current.ActionType &&
                    currentTick < _activeFakeOutUntilTick &&
                    !hasEmergency)
                {
                    shaped += _profile.HumanizationFakeOutScoreBonus *
                              Mathf.Max(0f, _profile.HumanizationPersonalityExpression);
                }

                scores[i] = new AIActionScore(current.ActionType, Mathf.Max(0f, shaped));
            }

            _debugSummary =
                $"Human=fake:{_activeFakeOutAction}->{_activeFakeOutUntilTick} " +
                $"pressure={_pressureMistakeActive}->{_pressureMistakeUntilTick} " +
                $"jitter={jitterMagnitude:0.0}";

            if (_profile.LogHumanization && currentTick % 30u == 0u)
                Debug.Log($"[AIHuman] {_debugSummary}");
        }

        public void Reset()
        {
            _activeFakeOutAction = AIActionType.None;
            _activeFakeOutUntilTick = 0u;
            _nextFakeOutCheckTick = 0u;
            _pressureMistakeActive = false;
            _pressureMistakeUntilTick = 0u;
            _nextPressureMistakeCheckTick = 0u;
            _debugSummary = "Human=reset";
        }

        private bool IsEnabled()
        {
            return _profile != null && _profile.EnableHumanization;
        }

        private void UpdateFakeOutWindow(uint currentTick, bool hasLiveTarget, bool suppressFakeOut)
        {
            if (currentTick < _activeFakeOutUntilTick)
                return;

            _activeFakeOutAction = AIActionType.None;

            if (currentTick < _nextFakeOutCheckTick)
                return;

            _nextFakeOutCheckTick =
                currentTick +
                _profile.HumanizationFakeOutCooldownTicks +
                HashRange(_seed, currentTick, FakeOutSalt, 0u, 12u);

            if (!hasLiveTarget || suppressFakeOut || _profile.HumanizationFakeOutChance <= 0f)
                return;

            float roll = Stable01(_seed, currentTick, FakeOutSalt);
            if (roll > _profile.HumanizationFakeOutChance)
                return;

            _activeFakeOutAction = SelectFakeOutAction(currentTick);
            _activeFakeOutUntilTick = currentTick + _profile.HumanizationFakeOutDurationTicks;
        }

        private void UpdatePressureMistakeWindow(
            uint currentTick,
            bool hasLiveTarget,
            float healthRatio,
            bool hasDanger,
            bool hasEmergency)
        {
            if (currentTick < _pressureMistakeUntilTick)
                return;

            _pressureMistakeActive = false;

            if (currentTick < _nextPressureMistakeCheckTick)
                return;

            _nextPressureMistakeCheckTick =
                currentTick +
                _profile.HumanizationPressureMistakeCooldownTicks +
                HashRange(_seed, currentTick, PressureSalt, 0u, 16u);

            if (!hasLiveTarget ||
                hasEmergency ||
                _profile.HumanizationPressureMistakeChance <= 0f)
            {
                return;
            }

            float threshold = Mathf.Max(0.05f, _profile.HumanizationPressureHealthThreshold);
            bool underPressure = hasDanger || healthRatio <= threshold;
            if (!underPressure)
                return;

            float pressure = hasDanger
                ? 1f
                : 1f - Mathf.Clamp01(healthRatio / threshold);
            float chance = _profile.HumanizationPressureMistakeChance *
                           Mathf.Lerp(0.75f, 1.45f, pressure);

            if (Stable01(_seed, currentTick, PressureSalt) > chance)
                return;

            _pressureMistakeActive = true;
            _pressureMistakeUntilTick = currentTick + _profile.HumanizationPressureMistakeDurationTicks;
        }

        private AIActionType SelectFakeOutAction(uint currentTick)
        {
            switch (_profile.Personality)
            {
                case AIPersonalityType.Aggressive:
                    return Stable01(_seed, currentTick, FakeOutSalt ^ 0x27D4EB2Du) < 0.65f
                        ? AIActionType.Approach
                        : AIActionType.Reposition;

                case AIPersonalityType.Cautious:
                    return Stable01(_seed, currentTick, FakeOutSalt ^ 0x165667B1u) < 0.60f
                        ? AIActionType.HoldRange
                        : AIActionType.Reposition;

                case AIPersonalityType.TeamPlayer:
                    return Stable01(_seed, currentTick, FakeOutSalt ^ 0xD3A2646Cu) < 0.55f
                        ? AIActionType.Peel
                        : AIActionType.Reposition;

                case AIPersonalityType.Balanced:
                default:
                    return AIActionType.Reposition;
            }
        }

        private float GetPressurePersonalityMultiplier()
        {
            switch (_profile.Personality)
            {
                case AIPersonalityType.Aggressive:
                    return 1.15f;
                case AIPersonalityType.Cautious:
                    return 0.90f;
                case AIPersonalityType.TeamPlayer:
                    return 0.85f;
                default:
                    return 1f;
            }
        }

        private bool HasEmergencyScore(IList<AIActionScore> scores)
        {
            float emergencyFloor = Mathf.Max(45f, _profile.EmergencyOverrideScore * 0.72f);
            for (int i = 0; i < scores.Count; i++)
            {
                if (IsEmergencyAction(scores[i].ActionType) && scores[i].Score >= emergencyFloor)
                    return true;
            }

            return false;
        }

        private bool IsEmergencyProtected(AIActionScore score)
        {
            return IsEmergencyAction(score.ActionType) &&
                   score.Score >= Mathf.Max(45f, _profile.EmergencyOverrideScore * 0.60f);
        }

        private static bool IsEmergencyAction(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Retreat:
                case AIActionType.Evade:
                case AIActionType.UseSuper:
                case AIActionType.Peel:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsOffensiveAction(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Approach:
                case AIActionType.HoldRange:
                case AIActionType.UseSuper:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsOverDefensiveExpression(AIActionType actionType)
        {
            switch (actionType)
            {
                case AIActionType.Retreat:
                case AIActionType.Reposition:
                    return true;
                default:
                    return false;
            }
        }

        private static uint HashRange(uint seed, uint tick, uint salt, uint minInclusive, uint maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;

            uint span = maxExclusive - minInclusive;
            return minInclusive + (Mix(seed ^ tick, salt) % span);
        }

        private static float Stable01(uint seed, uint tick, uint salt)
        {
            uint value = Mix(seed ^ tick, salt);
            return (value & 0x00FFFFFFu) / 16777215f;
        }

        private static float StableSigned(uint seed, uint tick, uint salt)
        {
            return Stable01(seed, tick, salt) * 2f - 1f;
        }

        private static uint Mix(uint value, uint salt)
        {
            unchecked
            {
                value ^= salt + 0x9E3779B9u + (value << 6) + (value >> 2);
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }
    }
}
