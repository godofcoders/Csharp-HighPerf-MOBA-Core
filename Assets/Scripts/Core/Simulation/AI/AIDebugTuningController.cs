using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public sealed class AIDebugTuningController : MonoBehaviour
    {
        [Header("Runtime Override")]
        [SerializeField] private bool _enableRuntimeOverrides = true;
        [SerializeField] private AITuningCatalog _catalogOverride;
        [SerializeField] private bool _clearOnDisable = true;

        [Header("Skill")]
        [SerializeField, Range(-12, 24)] private int _reactionDelayOffsetTicks;
        [SerializeField, Range(-8f, 12f)] private float _aimErrorOffsetDegrees;
        [SerializeField, Range(0.25f, 3f)] private float _senseIntervalMultiplier = 1f;
        [SerializeField, Range(0.25f, 3f)] private float _attackCadenceMultiplier = 1f;
        [SerializeField, Range(0.25f, 3f)] private float _abilityCooldownMultiplier = 1f;

        [Header("Behavior")]
        [SerializeField, Range(0.25f, 3f)] private float _aggressionMultiplier = 1f;
        [SerializeField, Range(0.25f, 3f)] private float _safetyMultiplier = 1f;
        [SerializeField, Range(0.25f, 3f)] private float _teamplayMultiplier = 1f;
        [SerializeField, Range(0.25f, 3f)] private float _objectiveMultiplier = 1f;

        [Header("Movement / Map")]
        [SerializeField, Range(0.25f, 3f)] private float _tacticalMovementMultiplier = 1f;
        [SerializeField, Range(0.25f, 3f)] private float _dangerAvoidanceMultiplier = 1f;
        [SerializeField, Range(0.25f, 3f)] private float _mapSafetyMultiplier = 1f;

        [Header("Humanization")]
        [SerializeField, Range(0.25f, 3f)] private float _humanizationMultiplier = 1f;

        [Header("Debug Flags")]
        [SerializeField] private bool _overrideDebugFlags;
        [SerializeField] private bool _enableValidationTelemetry = true;
        [SerializeField] private bool _enableDebugSnapshots = true;
        [SerializeField, Range(1, 60)] private int _debugSnapshotIntervalTicks = 5;
        [SerializeField] private bool _logDecisionTicks;
        [SerializeField] private bool _logTacticalMovement;
        [SerializeField] private bool _logMapIntelligence;
        [SerializeField] private bool _logDangerAvoidance;
        [SerializeField] private bool _logFailureRecovery;
        [SerializeField] private bool _logHumanization;
        [SerializeField] private bool _logBudgetWarnings;

        private string _lastAppliedKey;

        private void OnEnable()
        {
            ApplyIfChanged(force: true);
        }

        private void Update()
        {
            ApplyIfChanged(force: false);
        }

        private void OnDisable()
        {
            if (_clearOnDisable)
            {
                AITuningRuntimeOverrides.Clear();
                _lastAppliedKey = null;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                ApplyIfChanged(force: false);
        }
#endif

        private void ApplyIfChanged(bool force)
        {
            string key = BuildStateKey();
            if (!force && key == _lastAppliedKey)
                return;

            _lastAppliedKey = key;

            if (!_enableRuntimeOverrides && _catalogOverride == null)
            {
                AITuningRuntimeOverrides.Clear();
                return;
            }

            AITuningRuntimeOverrides.Set(
                _catalogOverride,
                _enableRuntimeOverrides,
                BuildModifierSet());
        }

        private AITuningModifierSet BuildModifierSet()
        {
            return new AITuningModifierSet
            {
                ReactionDelayOffsetTicks = _reactionDelayOffsetTicks,
                AimErrorOffsetDegrees = _aimErrorOffsetDegrees,
                SenseIntervalMultiplier = _senseIntervalMultiplier,
                AttackCadenceMultiplier = _attackCadenceMultiplier,
                AbilityCooldownMultiplier = _abilityCooldownMultiplier,
                AggressionMultiplier = _aggressionMultiplier,
                SafetyMultiplier = _safetyMultiplier,
                TeamplayMultiplier = _teamplayMultiplier,
                ObjectiveMultiplier = _objectiveMultiplier,
                TacticalMovementMultiplier = _tacticalMovementMultiplier,
                DangerAvoidanceMultiplier = _dangerAvoidanceMultiplier,
                MapSafetyMultiplier = _mapSafetyMultiplier,
                HumanizationMultiplier = _humanizationMultiplier,
                OverrideDebugFlags = _overrideDebugFlags,
                EnableValidationTelemetry = _enableValidationTelemetry,
                EnableDebugSnapshots = _enableDebugSnapshots,
                DebugSnapshotIntervalTicks = (uint)_debugSnapshotIntervalTicks,
                LogDecisionTicks = _logDecisionTicks,
                LogTacticalMovement = _logTacticalMovement,
                LogMapIntelligence = _logMapIntelligence,
                LogDangerAvoidance = _logDangerAvoidance,
                LogFailureRecovery = _logFailureRecovery,
                LogHumanization = _logHumanization,
                LogBudgetWarnings = _logBudgetWarnings
            };
        }

        private string BuildStateKey()
        {
            int catalogId = _catalogOverride != null ? _catalogOverride.GetInstanceID() : 0;
            return
                $"{_enableRuntimeOverrides}|{catalogId}|{_reactionDelayOffsetTicks}|" +
                $"{_aimErrorOffsetDegrees:0.###}|{_senseIntervalMultiplier:0.###}|" +
                $"{_attackCadenceMultiplier:0.###}|{_abilityCooldownMultiplier:0.###}|" +
                $"{_aggressionMultiplier:0.###}|{_safetyMultiplier:0.###}|" +
                $"{_teamplayMultiplier:0.###}|{_objectiveMultiplier:0.###}|" +
                $"{_tacticalMovementMultiplier:0.###}|{_dangerAvoidanceMultiplier:0.###}|" +
                $"{_mapSafetyMultiplier:0.###}|{_humanizationMultiplier:0.###}|" +
                $"{_overrideDebugFlags}|{_enableValidationTelemetry}|{_enableDebugSnapshots}|" +
                $"{_debugSnapshotIntervalTicks}|{_logDecisionTicks}|{_logTacticalMovement}|" +
                $"{_logMapIntelligence}|{_logDangerAvoidance}|{_logFailureRecovery}|" +
                $"{_logHumanization}|{_logBudgetWarnings}";
        }
    }
}
