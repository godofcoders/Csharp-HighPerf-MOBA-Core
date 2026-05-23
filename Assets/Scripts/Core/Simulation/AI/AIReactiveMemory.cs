using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIReactiveMemory
    {
        private bool _hasDamage;
        private ISpatialEntity _lastAttacker;
        private int _lastAttackerId;
        private Vector3 _lastAttackerPosition;
        private Vector3 _lastHitPosition;
        private Vector3 _lastIncomingDirection;
        private uint _lastDamageTick;
        private float _lastDamageAmount;
        private float _recentDamagePressure;

        public int LastAttackerId => _lastAttackerId;
        public Vector3 LastAttackerPosition => _lastAttackerPosition;
        public Vector3 LastHitPosition => _lastHitPosition;
        public Vector3 LastIncomingDirection => _lastIncomingDirection;
        public uint LastDamageTick => _lastDamageTick;
        public float LastDamageAmount => _lastDamageAmount;

        public void RecordDamage(
            ISpatialEntity attacker,
            Vector3 hitPosition,
            Vector3 incomingDirection,
            float damageAmount,
            float maxHealth,
            uint currentTick)
        {
            if (!SpatialEntityUtility.IsAlive(attacker))
                return;

            _hasDamage = true;
            _lastAttacker = attacker;
            _lastAttackerId = attacker.EntityID;
            _lastAttackerPosition = attacker.Position;
            _lastHitPosition = hitPosition;
            _lastDamageTick = currentTick;
            _lastDamageAmount = Mathf.Max(0f, damageAmount);
            _lastIncomingDirection = ResolveIncomingDirection(
                incomingDirection,
                _lastAttackerPosition,
                hitPosition);

            float damageRatio = _lastDamageAmount / Mathf.Max(1f, maxHealth);
            _recentDamagePressure = Mathf.Clamp01((_recentDamagePressure * 0.45f) + damageRatio);
        }

        public bool HasRecentDamage(uint currentTick, uint memoryTicks)
        {
            if (!_hasDamage)
                return false;

            uint age = currentTick >= _lastDamageTick
                ? currentTick - _lastDamageTick
                : 0u;

            uint window = memoryTicks == 0u ? 1u : memoryTicks;
            return age <= window;
        }

        public float GetDamagePressure(uint currentTick, uint memoryTicks)
        {
            if (!HasRecentDamage(currentTick, memoryTicks))
                return 0f;

            uint age = currentTick >= _lastDamageTick
                ? currentTick - _lastDamageTick
                : 0u;

            uint window = memoryTicks == 0u ? 1u : memoryTicks;
            float decay = 1f - Mathf.Clamp01(age / (float)window);
            return Mathf.Clamp01(_recentDamagePressure * decay);
        }

        public bool TryGetRecentAttacker(uint currentTick, uint memoryTicks, out ISpatialEntity attacker)
        {
            attacker = null;

            if (!HasRecentDamage(currentTick, memoryTicks))
                return false;

            if (!SpatialEntityUtility.IsAlive(_lastAttacker))
                return false;

            attacker = _lastAttacker;
            return true;
        }

        public string GetDebugSummary(uint currentTick, uint memoryTicks)
        {
            if (!HasRecentDamage(currentTick, memoryTicks))
                return "Reactive=None";

            return
                $"Reactive=Damage " +
                $"attacker={_lastAttackerId} " +
                $"amount={_lastDamageAmount:0.0} " +
                $"pressure={GetDamagePressure(currentTick, memoryTicks):0.00} " +
                $"tick={_lastDamageTick}";
        }

        private static Vector3 ResolveIncomingDirection(
            Vector3 incomingDirection,
            Vector3 attackerPosition,
            Vector3 hitPosition)
        {
            incomingDirection.y = 0f;
            if (incomingDirection.sqrMagnitude > 0.001f)
                return incomingDirection.normalized;

            Vector3 fromAttacker = hitPosition - attackerPosition;
            fromAttacker.y = 0f;

            return fromAttacker.sqrMagnitude > 0.001f
                ? fromAttacker.normalized
                : Vector3.zero;
        }
    }
}
