using UnityEngine;
using MOBA.Core.Simulation;
using MOBA.Core.Simulation.AI;

namespace MOBA.Core.Infrastructure
{
    public sealed class AIValidationGauntletScenarioRunner : SimulationEntity
    {
        protected override TickPhase Phase => TickPhase.PostTick;

        [SerializeField] private AIValidationGauntletScenarioType _scenario =
            AIValidationGauntletScenarioType.RetreatSafety;
        [SerializeField] private bool _beginOnEnable = true;
        [SerializeField] private uint _durationTicks = 300;
        [SerializeField] private bool _logResult = true;

        private bool _running;
        private uint _startTick;

        protected override void OnEnable()
        {
            base.OnEnable();
            _running = false;
        }

        protected override void OnDisable()
        {
            if (_running)
                EndScenario(_startTick + _durationTicks);

            base.OnDisable();
        }

        public override void Tick(uint currentTick)
        {
            if (!_running && _beginOnEnable)
            {
                BeginScenario(currentTick);
                return;
            }

            if (_running && currentTick - _startTick > _durationTicks)
                EndScenario(currentTick);
        }

        public void BeginScenario(uint currentTick)
        {
            AIValidationGauntlet.BeginScenario(_scenario, currentTick);
            _startTick = currentTick;
            _running = true;
        }

        public AIValidationGauntletResult EndScenario(uint currentTick)
        {
            AIValidationGauntletResult result =
                AIValidationGauntlet.EndScenario(currentTick);
            _running = false;

            if (_logResult)
            {
                Debug.Log(
                    $"[AIGauntlet] scenario={result.ScenarioType} " +
                    $"status={result.Status} " +
                    $"reason={result.Reason} " +
                    $"frames={result.FrameCount} " +
                    $"bots={result.BotDecisionCount} " +
                    $"expected={result.ExpectedActionRatio:0%} " +
                    $"ability={result.AbilitySignalCount} " +
                    $"recovery={result.FailureRecoverySignalCount} " +
                    $"gem={result.GemIntentSignalCount} " +
                    $"objective={result.ObjectiveIntentSignalCount} " +
                    $"stop={result.TacticalStopSignalCount}");
            }

            return result;
        }
    }
}
