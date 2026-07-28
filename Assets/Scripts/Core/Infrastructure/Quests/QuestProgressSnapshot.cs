using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public struct QuestProgressSnapshot
    {
        public QuestDefinition Definition;
        public int CurrentValue;
        public int TargetValue;
        public bool IsComplete;

        public float NormalizedProgress =>
            TargetValue > 0 ? Mathf.Clamp01(CurrentValue / (float)TargetValue) : 1f;

        public string ProgressLabel => $"{CurrentValue}/{TargetValue}";

        public QuestProgressSnapshot(QuestDefinition definition, int currentValue)
        {
            Definition = definition;
            TargetValue = definition != null ? definition.TargetValue : 1;
            CurrentValue = Mathf.Clamp(currentValue, 0, TargetValue);
            IsComplete = CurrentValue >= TargetValue;
        }
    }
}
