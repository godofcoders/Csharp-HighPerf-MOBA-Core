using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Simulation;

namespace MOBA.Core.Simulation.AI
{
    public enum AIOpponentDodgeBias
    {
        None,
        Left,
        Right
    }

    public readonly struct AIOpponentHabitSnapshot
    {
        public readonly int OpponentEntityId;
        public readonly uint LastUpdatedTick;
        public readonly Vector3 LastPosition;
        public readonly Vector3 EstimatedVelocity;
        public readonly float Aggression;
        public readonly float DodgeLeftBias;
        public readonly float DodgeRightBias;
        public readonly float DodgeConfidence;
        public readonly AIOpponentDodgeBias DodgeBias;
        public readonly int PreferredTargetEntityId;
        public readonly float TargetPreferenceConfidence;
        public readonly float LowHealthGreed;
        public readonly float ObjectiveNeglect;

        public AIOpponentHabitSnapshot(
            int opponentEntityId,
            uint lastUpdatedTick,
            Vector3 lastPosition,
            Vector3 estimatedVelocity,
            float aggression,
            float dodgeLeftBias,
            float dodgeRightBias,
            float dodgeConfidence,
            AIOpponentDodgeBias dodgeBias,
            int preferredTargetEntityId,
            float targetPreferenceConfidence,
            float lowHealthGreed,
            float objectiveNeglect)
        {
            OpponentEntityId = opponentEntityId;
            LastUpdatedTick = lastUpdatedTick;
            LastPosition = lastPosition;
            EstimatedVelocity = estimatedVelocity;
            Aggression = aggression;
            DodgeLeftBias = dodgeLeftBias;
            DodgeRightBias = dodgeRightBias;
            DodgeConfidence = dodgeConfidence;
            DodgeBias = dodgeBias;
            PreferredTargetEntityId = preferredTargetEntityId;
            TargetPreferenceConfidence = targetPreferenceConfidence;
            LowHealthGreed = lowHealthGreed;
            ObjectiveNeglect = objectiveNeglect;
        }

        public string GetDebugSummary()
        {
            if (OpponentEntityId == 0)
                return "Opponent=None";

            return
                $"Opponent={OpponentEntityId} " +
                $"Agg={Aggression:0.00} " +
                $"Dodge={DodgeBias}:{DodgeConfidence:0.00} " +
                $"Pref={PreferredTargetEntityId}:{TargetPreferenceConfidence:0.00} " +
                $"Greed={LowHealthGreed:0.00} " +
                $"ObjNeg={ObjectiveNeglect:0.00}";
        }
    }

    public static class AIOpponentModel
    {
        private const uint DefaultMaxAgeTicks = 360;
        private const float MovementSampleMinDistance = 0.08f;
        private const float ClosePressureDistance = 6f;
        private const float LowHealthGreedThreshold = 0.38f;
        private const int MaxTrackedOpponentsPerTeam = 12;

        private static readonly Dictionary<TeamType, Dictionary<int, OpponentRecord>> _recordsByTeam =
            new Dictionary<TeamType, Dictionary<int, OpponentRecord>>(4);

        private sealed class OpponentRecord
        {
            public int OpponentEntityId;
            public uint LastUpdatedTick;
            public Vector3 LastPosition;
            public Vector3 EstimatedVelocity;
            public bool HasPosition;
            public float Aggression;
            public float DodgeLeftBias;
            public float DodgeRightBias;
            public int PreferredTargetEntityId;
            public float TargetPreferenceConfidence;
            public float LowHealthGreed;
            public float ObjectiveNeglect;
        }

        public static void RecordMovementSample(
            TeamType observerTeam,
            int opponentEntityId,
            Vector3 observerPosition,
            Vector3 opponentPosition,
            float opponentHealthRatio,
            bool hasObjectivePoint,
            Vector3 objectivePoint,
            float objectiveRadius,
            uint currentTick)
        {
            if (observerTeam == TeamType.Neutral || opponentEntityId == 0)
                return;

            Dictionary<int, OpponentRecord> records = GetRecords(observerTeam);
            OpponentRecord record = GetOrCreateRecord(records, opponentEntityId);
            DecayRecord(record, currentTick);

            Vector3 velocity = Vector3.zero;
            float sampleWeight = 0f;
            if (record.HasPosition && currentTick > record.LastUpdatedTick)
            {
                uint tickDelta = currentTick - record.LastUpdatedTick;
                sampleWeight = Mathf.Clamp(tickDelta / 15f, 0.10f, 1f);
                float seconds = tickDelta * SimulationClock.TickDeltaTime;
                if (seconds > 0.001f)
                {
                    velocity = (opponentPosition - record.LastPosition) / seconds;
                    velocity.y = 0f;
                }
            }

            if (velocity.magnitude >= MovementSampleMinDistance)
            {
                RecordDodgeDirection(record, observerPosition, opponentPosition, velocity);
                RecordMovementAggression(record, observerPosition, opponentPosition, velocity, opponentHealthRatio);
            }

            RecordObjectiveAwareness(
                record,
                opponentPosition,
                hasObjectivePoint,
                objectivePoint,
                objectiveRadius,
                sampleWeight);

            record.LastPosition = opponentPosition;
            record.EstimatedVelocity = velocity;
            record.LastUpdatedTick = currentTick;
            record.HasPosition = true;

            TrimRecords(records, currentTick);
        }

        public static void RecordDamage(
            TeamType observerTeam,
            int opponentEntityId,
            int targetEntityId,
            float opponentHealthRatio,
            float normalizedDamagePressure,
            uint currentTick)
        {
            if (observerTeam == TeamType.Neutral || opponentEntityId == 0)
                return;

            Dictionary<int, OpponentRecord> records = GetRecords(observerTeam);
            OpponentRecord record = GetOrCreateRecord(records, opponentEntityId);
            DecayRecord(record, currentTick);

            float damagePressure = Mathf.Clamp01(normalizedDamagePressure);
            record.Aggression = Mathf.Clamp01(record.Aggression + 0.12f + damagePressure * 0.38f);

            if (targetEntityId != 0)
                RecordTargetPreference(record, targetEntityId);

            if (opponentHealthRatio <= LowHealthGreedThreshold)
                record.LowHealthGreed = Mathf.Clamp01(record.LowHealthGreed + 0.30f + damagePressure * 0.35f);

            record.LastUpdatedTick = currentTick;
            TrimRecords(records, currentTick);
        }

        public static bool TryGetSnapshot(
            TeamType observerTeam,
            int opponentEntityId,
            uint currentTick,
            uint maxAgeTicks,
            out AIOpponentHabitSnapshot snapshot)
        {
            snapshot = default;

            if (observerTeam == TeamType.Neutral || opponentEntityId == 0)
                return false;

            if (!_recordsByTeam.TryGetValue(observerTeam, out Dictionary<int, OpponentRecord> records) ||
                !records.TryGetValue(opponentEntityId, out OpponentRecord record))
            {
                return false;
            }

            uint maxAge = maxAgeTicks == 0u ? DefaultMaxAgeTicks : maxAgeTicks;
            if (currentTick >= record.LastUpdatedTick &&
                currentTick - record.LastUpdatedTick > maxAge)
            {
                return false;
            }

            snapshot = BuildSnapshot(record);
            return true;
        }

        public static float GetMaxObjectiveNeglect(
            TeamType observerTeam,
            uint currentTick,
            uint maxAgeTicks = DefaultMaxAgeTicks)
        {
            if (!_recordsByTeam.TryGetValue(observerTeam, out Dictionary<int, OpponentRecord> records))
                return 0f;

            float maxNeglect = 0f;
            foreach (var pair in records)
            {
                OpponentRecord record = pair.Value;
                if (currentTick >= record.LastUpdatedTick &&
                    currentTick - record.LastUpdatedTick > maxAgeTicks)
                {
                    continue;
                }

                maxNeglect = Mathf.Max(maxNeglect, record.ObjectiveNeglect);
            }

            return Mathf.Clamp01(maxNeglect);
        }

        public static string GetBestDebugSummary(
            TeamType observerTeam,
            uint currentTick,
            uint maxAgeTicks = DefaultMaxAgeTicks)
        {
            if (!_recordsByTeam.TryGetValue(observerTeam, out Dictionary<int, OpponentRecord> records))
                return "Opponent=None";

            OpponentRecord best = null;
            float bestScore = 0f;

            foreach (var pair in records)
            {
                OpponentRecord record = pair.Value;
                if (currentTick >= record.LastUpdatedTick &&
                    currentTick - record.LastUpdatedTick > maxAgeTicks)
                {
                    continue;
                }

                float score =
                    record.Aggression +
                    Mathf.Max(record.DodgeLeftBias, record.DodgeRightBias) +
                    record.TargetPreferenceConfidence +
                    record.LowHealthGreed +
                    record.ObjectiveNeglect;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = record;
                }
            }

            return best != null
                ? BuildSnapshot(best).GetDebugSummary()
                : "Opponent=None";
        }

        public static Vector3 ApplyDodgeHabitToVelocity(
            Vector3 shooterPosition,
            Vector3 targetPosition,
            Vector3 baseVelocity,
            AIOpponentHabitSnapshot snapshot,
            float strength = 1f)
        {
            if (snapshot.DodgeBias == AIOpponentDodgeBias.None ||
                snapshot.DodgeConfidence <= 0.05f)
            {
                return baseVelocity;
            }

            Vector3 toTarget = targetPosition - shooterPosition;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= 0.001f)
                return baseVelocity;

            Vector3 right = Vector3.Cross(Vector3.up, toTarget.normalized);
            float sign = snapshot.DodgeBias == AIOpponentDodgeBias.Right ? 1f : -1f;
            float habitSpeed = Mathf.Lerp(0.35f, 1.35f, Mathf.Clamp01(snapshot.DodgeConfidence));

            return baseVelocity + right * sign * habitSpeed * Mathf.Clamp01(strength);
        }

        public static void ResetForTests()
        {
            _recordsByTeam.Clear();
        }

        private static void RecordDodgeDirection(
            OpponentRecord record,
            Vector3 observerPosition,
            Vector3 opponentPosition,
            Vector3 velocity)
        {
            Vector3 sightline = opponentPosition - observerPosition;
            sightline.y = 0f;
            if (sightline.sqrMagnitude <= 0.001f)
                return;

            Vector3 right = Vector3.Cross(Vector3.up, sightline.normalized);
            Vector3 flatVelocity = velocity;
            flatVelocity.y = 0f;
            if (flatVelocity.sqrMagnitude <= 0.001f)
                return;

            float side = Vector3.Dot(flatVelocity.normalized, right);
            if (side > 0.28f)
            {
                record.DodgeRightBias = Mathf.Clamp01(record.DodgeRightBias + side * 0.18f);
                record.DodgeLeftBias *= 0.96f;
            }
            else if (side < -0.28f)
            {
                record.DodgeLeftBias = Mathf.Clamp01(record.DodgeLeftBias + -side * 0.18f);
                record.DodgeRightBias *= 0.96f;
            }
        }

        private static void RecordMovementAggression(
            OpponentRecord record,
            Vector3 observerPosition,
            Vector3 opponentPosition,
            Vector3 velocity,
            float opponentHealthRatio)
        {
            Vector3 toObserver = observerPosition - opponentPosition;
            toObserver.y = 0f;
            if (toObserver.sqrMagnitude <= 0.001f)
                return;

            Vector3 flatVelocity = velocity;
            flatVelocity.y = 0f;
            if (flatVelocity.sqrMagnitude <= 0.001f)
                return;

            float towardObserver = Vector3.Dot(flatVelocity.normalized, toObserver.normalized);
            float distance = toObserver.magnitude;

            if (towardObserver > 0.35f)
            {
                float closeBonus = distance <= ClosePressureDistance ? 0.06f : 0.02f;
                record.Aggression = Mathf.Clamp01(record.Aggression + closeBonus + towardObserver * 0.05f);

                if (opponentHealthRatio <= LowHealthGreedThreshold)
                    record.LowHealthGreed = Mathf.Clamp01(record.LowHealthGreed + 0.08f + towardObserver * 0.08f);
            }
            else if (towardObserver < -0.45f)
            {
                record.Aggression = Mathf.Clamp01(record.Aggression - 0.035f);
            }
        }

        private static void RecordObjectiveAwareness(
            OpponentRecord record,
            Vector3 opponentPosition,
            bool hasObjectivePoint,
            Vector3 objectivePoint,
            float objectiveRadius,
            float sampleWeight)
        {
            if (!hasObjectivePoint || sampleWeight <= 0f)
                return;

            objectiveRadius = Mathf.Max(1f, objectiveRadius);
            float distance = Vector3.Distance(opponentPosition, objectivePoint);

            if (distance > objectiveRadius * 1.35f)
                record.ObjectiveNeglect = Mathf.Clamp01(record.ObjectiveNeglect + 0.035f * sampleWeight);
            else if (distance <= objectiveRadius)
                record.ObjectiveNeglect = Mathf.Clamp01(record.ObjectiveNeglect - 0.075f * sampleWeight);
        }

        private static void RecordTargetPreference(
            OpponentRecord record,
            int targetEntityId)
        {
            if (record.PreferredTargetEntityId == 0 ||
                record.PreferredTargetEntityId == targetEntityId ||
                record.TargetPreferenceConfidence <= 0.20f)
            {
                record.PreferredTargetEntityId = targetEntityId;
                record.TargetPreferenceConfidence =
                    Mathf.Clamp01(record.TargetPreferenceConfidence + 0.22f);
                return;
            }

            record.TargetPreferenceConfidence = Mathf.Clamp01(record.TargetPreferenceConfidence - 0.16f);
        }

        private static AIOpponentHabitSnapshot BuildSnapshot(OpponentRecord record)
        {
            float left = Mathf.Clamp01(record.DodgeLeftBias);
            float right = Mathf.Clamp01(record.DodgeRightBias);
            float dodgeConfidence = Mathf.Clamp01(Mathf.Abs(right - left));
            AIOpponentDodgeBias dodgeBias = AIOpponentDodgeBias.None;

            if (dodgeConfidence >= 0.12f)
                dodgeBias = right > left ? AIOpponentDodgeBias.Right : AIOpponentDodgeBias.Left;

            return new AIOpponentHabitSnapshot(
                record.OpponentEntityId,
                record.LastUpdatedTick,
                record.LastPosition,
                record.EstimatedVelocity,
                Mathf.Clamp01(record.Aggression),
                left,
                right,
                dodgeConfidence,
                dodgeBias,
                record.PreferredTargetEntityId,
                Mathf.Clamp01(record.TargetPreferenceConfidence),
                Mathf.Clamp01(record.LowHealthGreed),
                Mathf.Clamp01(record.ObjectiveNeglect));
        }

        private static Dictionary<int, OpponentRecord> GetRecords(TeamType observerTeam)
        {
            if (!_recordsByTeam.TryGetValue(observerTeam, out Dictionary<int, OpponentRecord> records))
            {
                records = new Dictionary<int, OpponentRecord>(MaxTrackedOpponentsPerTeam);
                _recordsByTeam[observerTeam] = records;
            }

            return records;
        }

        private static OpponentRecord GetOrCreateRecord(
            Dictionary<int, OpponentRecord> records,
            int opponentEntityId)
        {
            if (!records.TryGetValue(opponentEntityId, out OpponentRecord record))
            {
                record = new OpponentRecord
                {
                    OpponentEntityId = opponentEntityId
                };
                records[opponentEntityId] = record;
            }

            return record;
        }

        private static void DecayRecord(OpponentRecord record, uint currentTick)
        {
            if (record.LastUpdatedTick == 0u || currentTick <= record.LastUpdatedTick)
                return;

            uint tickDelta = currentTick - record.LastUpdatedTick;
            float cappedTicks = Mathf.Min(600f, (float)tickDelta);
            float decay = Mathf.Pow(0.9975f, cappedTicks);

            record.Aggression *= decay;
            record.DodgeLeftBias *= decay;
            record.DodgeRightBias *= decay;
            record.TargetPreferenceConfidence *= decay;
            record.LowHealthGreed *= decay;
            record.ObjectiveNeglect *= Mathf.Pow(0.9985f, cappedTicks);
        }

        private static void TrimRecords(
            Dictionary<int, OpponentRecord> records,
            uint currentTick)
        {
            if (records.Count <= MaxTrackedOpponentsPerTeam)
                return;

            int oldestId = 0;
            uint oldestTick = uint.MaxValue;
            foreach (var pair in records)
            {
                uint tick = pair.Value.LastUpdatedTick;
                if (tick < oldestTick)
                {
                    oldestTick = tick;
                    oldestId = pair.Key;
                }
            }

            if (oldestId != 0 &&
                (currentTick >= oldestTick || records.Count > MaxTrackedOpponentsPerTeam + 2))
            {
                records.Remove(oldestId);
            }
        }
    }
}
