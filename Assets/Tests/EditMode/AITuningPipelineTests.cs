using System.Collections.Generic;
using MOBA.Core.Definitions;
using MOBA.Core.Simulation.AI;
using NUnit.Framework;
using UnityEngine;

namespace MOBA.Tests.EditMode
{
    public class AITuningPipelineTests
    {
        private readonly List<Object> _objects = new List<Object>(8);

        [SetUp]
        public void SetUp()
        {
            AITuningRuntimeOverrides.ResetForTests();
            AITuningCatalogProvider.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] != null)
                    Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
            AITuningRuntimeOverrides.ResetForTests();
            AITuningCatalogProvider.ResetForTests();
        }

        [Test]
        public void Catalog_AppliesGlobalAndBestMatchingPreset()
        {
            BrawlerAIProfile profile = CreateProfile();
            AITuningCatalog catalog = CreateCatalog();
            catalog.GlobalModifiers = new AITuningModifierSet
            {
                ObjectiveMultiplier = 1.25f
            };

            AITuningPreset broadHard = CreatePreset(
                AIDifficultyLevel.Hard,
                AIPersonalityType.Balanced,
                anyPersonality: true,
                priority: 0,
                new AITuningModifierSet
                {
                    AggressionMultiplier = 1.20f,
                    SenseIntervalMultiplier = 0.80f
                });

            AITuningPreset exactHardAggressive = CreatePreset(
                AIDifficultyLevel.Hard,
                AIPersonalityType.Aggressive,
                anyPersonality: false,
                priority: 0,
                new AITuningModifierSet
                {
                    AggressionMultiplier = 1.50f,
                    AttackCadenceMultiplier = 0.75f
                });

            catalog.Presets = new[] { broadHard, exactHardAggressive };

            AIProfileTuningUtility.ApplyRuntimeTuning(
                profile,
                AIDifficultyLevel.Hard,
                AIPersonalityType.Aggressive,
                catalog);

            Assert.Greater(profile.ObjectiveWeight, 1f);
            Assert.Greater(profile.MacroActionBiasWeight, 1f);
            Assert.Greater(profile.ApproachWeight, 1.5f);
            Assert.Less((int)profile.AttackCadenceTicks, 10);
            Assert.AreSame(
                exactHardAggressive,
                ResolvePreset(catalog, AIDifficultyLevel.Hard, AIPersonalityType.Aggressive));
        }

        [Test]
        public void RuntimeOverrides_ApplyAfterCatalogAndCanDriveDebugFlags()
        {
            BrawlerAIProfile profile = CreateProfile();
            AITuningRuntimeOverrides.Set(
                catalogOverride: null,
                hasModifierOverrides: true,
                modifiers: new AITuningModifierSet
                {
                    ReactionDelayOffsetTicks = 4,
                    AimErrorOffsetDegrees = 2f,
                    TeamplayMultiplier = 1.5f,
                    OverrideDebugFlags = true,
                    EnableValidationTelemetry = false,
                    EnableDebugSnapshots = true,
                    DebugSnapshotIntervalTicks = 12,
                    LogTacticalMovement = true
                });

            AIProfileTuningUtility.ApplyRuntimeTuning(
                profile,
                AIDifficultyLevel.Normal,
                AIPersonalityType.TeamPlayer,
                null);

            Assert.AreEqual(7u, profile.ReactionDelayTicks);
            Assert.GreaterOrEqual(profile.AimErrorDegrees, 4f);
            Assert.Greater(profile.TeamRoleCoordinationWeight, 1.5f);
            Assert.IsFalse(profile.EnableValidationTelemetry);
            Assert.IsTrue(profile.EnableDebugSnapshots);
            Assert.AreEqual(12u, profile.DebugSnapshotIntervalTicks);
            Assert.IsTrue(profile.LogTacticalMovement);
        }

        [Test]
        public void RebuildRuntimeTuning_CopiesFromSourceBeforeApplyingModifiers()
        {
            BrawlerAIProfile source = CreateProfile();
            BrawlerAIProfile runtime = CreateProfile();
            AITuningCatalog catalog = CreateCatalog();
            catalog.GlobalModifiers = new AITuningModifierSet
            {
                AggressionMultiplier = 1.5f
            };

            AIProfileTuningUtility.RebuildRuntimeTuning(
                source,
                runtime,
                AIDifficultyLevel.Normal,
                AIPersonalityType.Balanced,
                catalog);
            float firstApproach = runtime.ApproachWeight;

            AIProfileTuningUtility.RebuildRuntimeTuning(
                source,
                runtime,
                AIDifficultyLevel.Normal,
                AIPersonalityType.Balanced,
                catalog);

            Assert.AreEqual(firstApproach, runtime.ApproachWeight);
        }

        private BrawlerAIProfile CreateProfile()
        {
            BrawlerAIProfile profile = ScriptableObject.CreateInstance<BrawlerAIProfile>();
            profile.ApplyArchetypeDefaults(BrawlerArchetype.Fighter);
            _objects.Add(profile);
            return profile;
        }

        private AITuningCatalog CreateCatalog()
        {
            AITuningCatalog catalog = ScriptableObject.CreateInstance<AITuningCatalog>();
            _objects.Add(catalog);
            return catalog;
        }

        private AITuningPreset CreatePreset(
            AIDifficultyLevel difficulty,
            AIPersonalityType personality,
            bool anyPersonality,
            int priority,
            AITuningModifierSet modifiers)
        {
            AITuningPreset preset = ScriptableObject.CreateInstance<AITuningPreset>();
            preset.Difficulty = difficulty;
            preset.Personality = personality;
            preset.AppliesToAnyPersonality = anyPersonality;
            preset.Priority = priority;
            preset.Modifiers = modifiers;
            _objects.Add(preset);
            return preset;
        }

        private static AITuningPreset ResolvePreset(
            AITuningCatalog catalog,
            AIDifficultyLevel difficulty,
            AIPersonalityType personality)
        {
            catalog.TryGetPreset(difficulty, personality, out AITuningPreset preset);
            return preset;
        }
    }
}
