using System.Collections.Generic;
using UnityEngine;
using MOBA.Core.Infrastructure;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIDangerMemory
    {
        private readonly List<GameplayThreatInfo> _threatBuffer = new List<GameplayThreatInfo>(16);

        private bool _hasDanger;
        private float _pressure;
        private Vector3 _avoidanceDirection;
        private Vector3 _threatPosition;
        private GameplayThreatInfo _primaryThreat;
        private uint _lastThreatTick;
        private string _debugSummary = "Danger=None";
        private IProjectileThreatProvider _projectileProvider;
        private IAreaHazardThreatProvider _hazardProvider;

        public bool HasDanger => _hasDanger;
        public float Pressure => _pressure;
        public Vector3 AvoidanceDirection => _avoidanceDirection;
        public Vector3 ThreatPosition => _threatPosition;
        public GameplayThreatInfo PrimaryThreat => _primaryThreat;
        public uint LastThreatTick => _lastThreatTick;

        public void Refresh(
            BrawlerController self,
            BrawlerAIProfile profile,
            uint currentTick)
        {
            if (self == null || profile == null || self.State == null || self.State.IsDead)
            {
                Clear();
                return;
            }

            Vector3 selfPosition = self.Position;
            TeamType selfTeam = self.Team;

            _threatBuffer.Clear();

            IProjectileThreatProvider projectileProvider = GetProjectileProvider();
            if (projectileProvider != null)
            {
                projectileProvider.AppendProjectileThreatsNonAlloc(
                    selfPosition,
                    selfTeam,
                    profile.DangerScanRadius,
                    _threatBuffer);
            }

            IAreaHazardThreatProvider hazardProvider = GetHazardProvider();
            if (hazardProvider != null)
            {
                hazardProvider.AppendAreaHazardThreatsNonAlloc(
                    selfPosition,
                    selfTeam,
                    profile.DangerScanRadius,
                    _threatBuffer);
            }

            EvaluateThreats(
                selfPosition,
                self.CollisionRadius,
                self.State.MaxHealth.Value,
                _threatBuffer,
                profile,
                currentTick);

            if (profile.LogDangerAvoidance && _hasDanger)
                Debug.Log($"[AIDanger-{self.name}] {_debugSummary}");
        }

        public void EvaluateThreats(
            Vector3 selfPosition,
            float collisionRadius,
            float maxHealth,
            IList<GameplayThreatInfo> threats,
            BrawlerAIProfile profile,
            uint currentTick)
        {
            if (threats == null || threats.Count == 0)
            {
                Clear();
                return;
            }

            float bestScore = 0f;
            Vector3 weightedAvoidance = Vector3.zero;
            GameplayThreatInfo bestThreat = default;

            for (int i = 0; i < threats.Count; i++)
            {
                GameplayThreatInfo threat = threats[i];
                if (threat.Damage <= 0f)
                    continue;

                float score = ScoreThreat(
                    selfPosition,
                    collisionRadius,
                    maxHealth,
                    threat,
                    profile);

                if (score <= 0f)
                    continue;

                Vector3 avoidDirection = ResolveAvoidanceDirection(selfPosition, threat);
                if (avoidDirection.sqrMagnitude > 0.001f)
                    weightedAvoidance += avoidDirection.normalized * score;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestThreat = threat;
                }
            }

            if (bestScore <= 0f)
            {
                Clear();
                return;
            }

            _hasDanger = true;
            _pressure = Mathf.Clamp01(bestScore);
            _primaryThreat = bestThreat;
            _threatPosition = bestThreat.Position;
            _lastThreatTick = currentTick;

            if (weightedAvoidance.sqrMagnitude > 0.001f)
            {
                weightedAvoidance.y = 0f;
                _avoidanceDirection = weightedAvoidance.normalized;
            }
            else
            {
                _avoidanceDirection = ResolveAvoidanceDirection(selfPosition, bestThreat);
            }

            _debugSummary =
                $"Danger={GetThreatLabel(bestThreat)} " +
                $"p={_pressure:0.00} " +
                $"dmg={bestThreat.Damage:0.0} " +
                $"tti={bestThreat.TimeToImpact:0.00} " +
                $"pos=({bestThreat.Position.x:0.0},{bestThreat.Position.y:0.0},{bestThreat.Position.z:0.0})";
        }

        public Vector3 GetEvadeDestination(Vector3 selfPosition, float evadeDistance)
        {
            if (!_hasDanger || _avoidanceDirection.sqrMagnitude <= 0.001f)
                return selfPosition;

            Vector3 destination = selfPosition + _avoidanceDirection.normalized * Mathf.Max(0.25f, evadeDistance);
            destination.y = selfPosition.y;
            return destination;
        }

        public string GetDebugSummary()
        {
            return _debugSummary;
        }

        private void Clear()
        {
            _hasDanger = false;
            _pressure = 0f;
            _avoidanceDirection = Vector3.zero;
            _threatPosition = Vector3.zero;
            _primaryThreat = default;
            _debugSummary = "Danger=None";
        }

        private static float ScoreThreat(
            Vector3 selfPosition,
            float collisionRadius,
            float maxHealth,
            GameplayThreatInfo threat,
            BrawlerAIProfile profile)
        {
            float personalSpace = profile != null ? profile.DangerPersonalSpace : 0.55f;
            float reactionTime = profile != null ? profile.DangerReactionTimeSeconds : 0.75f;

            Vector3 delta = selfPosition - threat.Position;
            delta.y = 0f;

            float threatRadius = Mathf.Max(0.1f, threat.Radius + collisionRadius + personalSpace);
            float closeness = 1f - Mathf.Clamp01(delta.magnitude / threatRadius);

            float timeUrgency = threat.TimeToImpact <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(threat.TimeToImpact / Mathf.Max(0.1f, reactionTime));

            float damageUrgency = Mathf.Clamp01(threat.Damage / Mathf.Max(1f, maxHealth));

            float score =
                (closeness * 0.58f) +
                (timeUrgency * 0.32f) +
                (damageUrgency * 0.10f);

            if (threat.IsSuper)
                score *= 1.15f;

            return Mathf.Clamp01(score);
        }

        private static Vector3 ResolveAvoidanceDirection(
            Vector3 selfPosition,
            GameplayThreatInfo threat)
        {
            Vector3 direction = threat.Direction;
            direction.y = 0f;

            Vector3 away = selfPosition - threat.Position;
            away.y = 0f;

            if (threat.IsProjectile && direction.sqrMagnitude > 0.001f)
            {
                direction.Normalize();
                Vector3 side = new Vector3(direction.z, 0f, -direction.x);

                if (away.sqrMagnitude > 0.001f && Vector3.Dot(side, away) < 0f)
                    side = -side;

                return side.sqrMagnitude > 0.001f ? side.normalized : Vector3.zero;
            }

            if (away.sqrMagnitude > 0.001f)
                return away.normalized;

            if (direction.sqrMagnitude > 0.001f)
                return -direction.normalized;

            return Vector3.zero;
        }

        private static bool IsProviderAlive<T>(T provider) where T : class
        {
            if (provider == null)
                return false;

            Object unityObject = provider as Object;
            return ReferenceEquals(unityObject, null) || unityObject != null;
        }

        private IProjectileThreatProvider GetProjectileProvider()
        {
            if (!IsProviderAlive(_projectileProvider))
                ServiceProvider.TryGet(out _projectileProvider);

            return IsProviderAlive(_projectileProvider) ? _projectileProvider : null;
        }

        private IAreaHazardThreatProvider GetHazardProvider()
        {
            if (!IsProviderAlive(_hazardProvider))
                ServiceProvider.TryGet(out _hazardProvider);

            return IsProviderAlive(_hazardProvider) ? _hazardProvider : null;
        }

        private static string GetThreatLabel(GameplayThreatInfo threat)
        {
            if (threat.IsAreaHazard)
                return "Hazard";

            if (threat.IsProjectile)
                return "Projectile";

            return "Threat";
        }
    }
}
