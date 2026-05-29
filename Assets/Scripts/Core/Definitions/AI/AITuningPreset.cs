using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    [CreateAssetMenu(fileName = "AITuningPreset", menuName = "MOBA/AI/Tuning Preset")]
    public sealed class AITuningPreset : ScriptableObject
    {
        [Header("Match")]
        public bool Enabled = true;
        public bool AppliesToAnyDifficulty;
        public AIDifficultyLevel Difficulty = AIDifficultyLevel.Normal;
        public bool AppliesToAnyPersonality;
        public AIPersonalityType Personality = AIPersonalityType.Balanced;
        public int Priority;

        [TextArea(2, 5)]
        public string DesignerNotes;

        [Header("Modifiers")]
        public AITuningModifierSet Modifiers;

        public bool Matches(
            AIDifficultyLevel difficulty,
            AIPersonalityType personality)
        {
            if (!Enabled)
                return false;

            bool difficultyMatches = AppliesToAnyDifficulty || Difficulty == difficulty;
            bool personalityMatches = AppliesToAnyPersonality || Personality == personality;
            return difficultyMatches && personalityMatches;
        }

        public int GetMatchScore(
            AIDifficultyLevel difficulty,
            AIPersonalityType personality)
        {
            if (!Matches(difficulty, personality))
                return int.MinValue;

            int score = Priority;
            score += AppliesToAnyDifficulty ? 0 : 100;
            score += AppliesToAnyPersonality ? 0 : 25;
            return score;
        }

        public void ApplyTo(BrawlerAIProfile profile)
        {
            if (!Enabled || profile == null)
                return;

            Modifiers.ApplyTo(profile);
        }

        public string GetDebugSummary()
        {
            return Modifiers.GetDebugSummary(name);
        }
    }
}
