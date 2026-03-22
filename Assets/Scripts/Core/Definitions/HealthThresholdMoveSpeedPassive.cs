using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "HealthThresholdMoveSpeedPassive", menuName = "MOBA/Passives/Health Threshold Move Speed")]
    public class HealthThresholdMoveSpeedPassive : PassiveDefinition
    {
        [Range(0f, 1f)]
        public float HealthThreshold = 0.4f;

        [Tooltip("0.20 = +20% move speed")]
        public float MoveSpeedMultiplier = 0.20f;

        private void OnValidate()
        {
            if (Category == PassiveCategory.TemporaryBuff)
                Category = PassiveCategory.Trait;

            AllowedSlotTypes = new[] { PassiveSlotType.Trait };
        }

        public override IPassiveRuntime CreateRuntime(PassiveInstallContext context)
        {
            return new Runtime(this, context.SourceToken);
        }

        private sealed class Runtime : IPassiveRuntime
        {
            private readonly HealthThresholdMoveSpeedPassive _definition;
            private bool _isActive;

            public PassiveDefinition Definition => _definition;
            public object SourceToken { get; }

            public Runtime(HealthThresholdMoveSpeedPassive definition, object sourceToken)
            {
                _definition = definition;
                SourceToken = sourceToken;
            }

            public void OnInstalled(BrawlerState state)
            {
                _isActive = false;
                Evaluate(state);
            }

            public void Tick(BrawlerState state, uint currentTick)
            {
                Evaluate(state);
            }

            public void OnUninstalled(BrawlerState state)
            {
                if (_isActive)
                {
                    state.MoveSpeed.RemoveModifiersFromSource(SourceToken);
                    _isActive = false;
                }
            }

            private void Evaluate(BrawlerState state)
            {
                if (state == null || state.MaxHealth.Value <= 0f)
                    return;

                float healthRatio = state.CurrentHealth / state.MaxHealth.Value;
                bool shouldBeActive = healthRatio <= _definition.HealthThreshold;

                if (shouldBeActive && !_isActive)
                {
                    state.MoveSpeed.AddModifier(
                        new StatModifier(_definition.MoveSpeedMultiplier, ModifierType.Multiplicative, SourceToken));
                    _isActive = true;
                }
                else if (!shouldBeActive && _isActive)
                {
                    state.MoveSpeed.RemoveModifiersFromSource(SourceToken);
                    _isActive = false;
                }
            }
        }
    }
}