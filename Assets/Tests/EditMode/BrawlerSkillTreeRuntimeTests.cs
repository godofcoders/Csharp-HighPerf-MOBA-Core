using System.Collections.Generic;
using System.Reflection;
using MOBA.Core.Definitions;
using MOBA.Core.Infrastructure;
using MOBA.Core.Simulation;
using NUnit.Framework;
using UnityEditor;

namespace MOBA.Tests.EditMode
{
    public class BrawlerSkillTreeRuntimeTests
    {
        private const string ColtDefinition =
            "Assets/Scriptables/Brawlers/colt/Colt_Definition.asset";
        private const string ByronDefinition =
            "Assets/Scriptables/Brawlers/byron/Byron_definition.asset";
        private const string BarleyDefinition =
            "Assets/Scriptables/Brawlers/Barley/Barley_BrawlerDefinition.asset";
        private const string JessieDefinition =
            "Assets/Scriptables/Brawlers/Jesse/Jesse_Definition.asset";

        [TestCase(ColtDefinition, "84 x 10", 840f, "DMG 84 x 10")]
        [TestCase(ByronDefinition, "210", 210f, "DMG 210   HEAL 350")]
        [TestCase(BarleyDefinition, "108/tick x 6", 648f, "DMG 108/tick x 6")]
        [TestCase(JessieDefinition, "156", 156f, "HP 3200   DMG 156")]
        public void DefaultSkillTreeSuper_ShowsAuthoredCombatStats(
            string brawlerPath,
            string expectedDamageText,
            float expectedTotalDamage,
            string expectedPayloadText)
        {
            BrawlerDefinition brawler = AssetDatabase.LoadAssetAtPath<BrawlerDefinition>(brawlerPath);
            Assert.That(brawler, Is.Not.Null, brawlerPath);
            Assert.That(brawler.SkillTree, Is.Not.Null, brawlerPath);
            Assert.That(BrawlerSkillTreeResolver.TryResolve(
                brawler,
                brawler.SkillTree,
                1,
                null,
                out ResolvedBrawlerBuild resolved,
                out string error), Is.True, error);
            Assert.That(resolved.SuperAbility, Is.Not.Null);

            Assert.That(
                InvokePreviewMethod<string>(
                    "ResolveAbilityDamageText",
                    resolved.SuperAbility,
                    0f,
                    1f),
                Is.EqualTo(expectedDamageText));
            Assert.That(
                InvokePreviewMethod<float>(
                    "ResolveAbilityDamageTotal",
                    resolved.SuperAbility,
                    0f,
                    1f),
                Is.EqualTo(expectedTotalDamage).Within(0.001f));
            Assert.That(
                InvokePreviewMethod<string>(
                    "ResolveAbilityPayloadText",
                    resolved.SuperAbility,
                    0f,
                    1f),
                Is.EqualTo(expectedPayloadText));
        }

        [TestCase(ColtDefinition, 1, 3000f, 720f, 4f)]
        [TestCase(ColtDefinition, 2, 3150f, 756f, 4f)]
        [TestCase(ColtDefinition, 11, 4500f, 1080f, 4f)]
        [TestCase(ByronDefinition, 1, 3000f, 660f, 4f)]
        [TestCase(ByronDefinition, 2, 3150f, 693f, 4f)]
        [TestCase(ByronDefinition, 11, 4500f, 990f, 4f)]
        [TestCase(BarleyDefinition, 1, 3600f, 840f, 5.6f)]
        [TestCase(BarleyDefinition, 11, 5400f, 1260f, 5.6f)]
        [TestCase(JessieDefinition, 1, 3200f, 720f, 4f)]
        [TestCase(JessieDefinition, 11, 4800f, 1080f, 4f)]
        public void PowerLevel_UpdatesDisplayedCombatStats(
            string brawlerPath,
            int powerLevel,
            float expectedHealth,
            float expectedDamage,
            float expectedMoveSpeed)
        {
            BrawlerDefinition brawler = AssetDatabase.LoadAssetAtPath<BrawlerDefinition>(brawlerPath);
            Assert.That(brawler, Is.Not.Null, brawlerPath);

            var state = new BrawlerState(brawler, TeamType.Neutral);
            state.SetPowerLevel(powerLevel, false);

            Assert.That(state.MaxHealth.Value, Is.EqualTo(expectedHealth).Within(0.001f));
            Assert.That(state.Damage.Value, Is.EqualTo(expectedDamage).Within(0.001f));
            Assert.That(state.MoveSpeed.Value, Is.EqualTo(expectedMoveSpeed).Within(0.001f));
        }

        [TestCase(
            ColtDefinition,
            "Assets/Scriptables/Brawlers/colt/Colt_SP_MagnumSpecial.asset",
            11,
            1080f,
            4f,
            1209.6f,
            4f)]
        [TestCase(
            ByronDefinition,
            "Assets/Scriptables/Brawlers/byron/Byron_SP_Malaise.asset",
            11,
            990f,
            4f,
            1089f,
            4f)]
        [TestCase(
            ByronDefinition,
            "Assets/Scriptables/Brawlers/byron/Byron_SP_Injection.asset",
            11,
            990f,
            4f,
            990f,
            4.4f)]
        public void EquippedStarPower_UpdatesDisplayedCombatStats(
            string brawlerPath,
            string passivePath,
            int powerLevel,
            float baseDamage,
            float baseMoveSpeed,
            float expectedDamage,
            float expectedMoveSpeed)
        {
            BrawlerDefinition brawler = AssetDatabase.LoadAssetAtPath<BrawlerDefinition>(brawlerPath);
            PassiveDefinition passive = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(passivePath);
            Assert.That(brawler, Is.Not.Null, brawlerPath);
            Assert.That(passive, Is.Not.Null, passivePath);

            var state = new BrawlerState(brawler, TeamType.Neutral);
            state.SetPowerLevel(powerLevel, false);
            Assert.That(state.Damage.Value, Is.EqualTo(baseDamage).Within(0.001f));
            Assert.That(state.MoveSpeed.Value, Is.EqualTo(baseMoveSpeed).Within(0.001f));

            state.SetPassiveLoadout(new[] { passive }, false);

            Assert.That(state.Damage.Value, Is.EqualTo(expectedDamage).Within(0.001f));
            Assert.That(state.MoveSpeed.Value, Is.EqualTo(expectedMoveSpeed).Within(0.001f));
        }

        [TestCase(ColtDefinition, "Assets/Scriptables/Brawlers/colt/Colt_SkillNode_FlameStep.asset", 0f, 0.04f, 0f)]
        [TestCase(ColtDefinition, "Assets/Scriptables/Brawlers/colt/Colt_SkillNode_ScorchMark.asset", 0f, 0f, 0.05f)]
        [TestCase(ColtDefinition, "Assets/Scriptables/Brawlers/colt/Colt_SkillNode_BattleTemper.asset", 150f, 0f, 0f)]
        [TestCase(ColtDefinition, "Assets/Scriptables/Brawlers/colt/Colt_SkillNode_PiercingRounds.asset", 0f, 0f, 0.05f)]
        [TestCase(ColtDefinition, "Assets/Scriptables/Brawlers/colt/Colt_SkillNode_OverheatedBarrel.asset", 0f, 0f, 0.06f)]
        [TestCase(ByronDefinition, "Assets/Scriptables/Brawlers/byron/Byron_SkillNode_SoothingCurrent.asset", 0f, 0.04f, 0f)]
        [TestCase(ByronDefinition, "Assets/Scriptables/Brawlers/byron/Byron_SkillNode_TidalGuard.asset", 180f, 0f, 0f)]
        [TestCase(ByronDefinition, "Assets/Scriptables/Brawlers/byron/Byron_SkillNode_ToxinFocus.asset", 0f, 0f, 0.04f)]
        [TestCase(ByronDefinition, "Assets/Scriptables/Brawlers/byron/Byron_SkillNode_LingeringVenom.asset", 0f, 0f, 0.05f)]
        [TestCase(ByronDefinition, "Assets/Scriptables/Brawlers/byron/Byron_SkillNode_DeepReserves.asset", 0f, 0f, 0.06f)]
        [TestCase(BarleyDefinition, "Assets/Scriptables/Brawlers/Barley/Barley_SkillNode_Barkskin.asset", 220f, 0f, 0f)]
        [TestCase(BarleyDefinition, "Assets/Scriptables/Brawlers/Barley/Barley_SkillNode_RootRunner.asset", 0f, 0.04f, 0f)]
        [TestCase(BarleyDefinition, "Assets/Scriptables/Brawlers/Barley/Barley_SkillNode_ToxicBloom.asset", 0f, 0f, 0.04f)]
        [TestCase(BarleyDefinition, "Assets/Scriptables/Brawlers/Barley/Barley_SkillNode_Regrowth.asset", 250f, 0f, 0f)]
        [TestCase(BarleyDefinition, "Assets/Scriptables/Brawlers/Barley/Barley_SkillNode_DeepFerment.asset", 0f, 0f, 0.05f)]
        [TestCase(BarleyDefinition, "Assets/Scriptables/Brawlers/Barley/Barley_SkillNode_WildGrowth.asset", 0f, 0f, 0.06f)]
        [TestCase(JessieDefinition, "Assets/Scriptables/Brawlers/Jesse/Jessie_SkillNode_ReinforcedFrame.asset", 200f, 0f, 0f)]
        [TestCase(JessieDefinition, "Assets/Scriptables/Brawlers/Jesse/Jessie_SkillNode_LiveWire.asset", 0f, 0.04f, 0f)]
        [TestCase(JessieDefinition, "Assets/Scriptables/Brawlers/Jesse/Jessie_SkillNode_ArcAmplifier.asset", 0f, 0f, 0.04f)]
        [TestCase(JessieDefinition, "Assets/Scriptables/Brawlers/Jesse/Jessie_SkillNode_FieldRepair.asset", 250f, 0f, 0f)]
        [TestCase(JessieDefinition, "Assets/Scriptables/Brawlers/Jesse/Jessie_SkillNode_ChainReaction.asset", 0f, 0f, 0.05f)]
        [TestCase(JessieDefinition, "Assets/Scriptables/Brawlers/Jesse/Jessie_SkillNode_FullCircuit.asset", 0f, 0f, 0.06f)]
        public void AuthoredStatNode_ChangesLiveStatsAndCleansUpWhenUnequipped(
            string brawlerPath,
            string nodePath,
            float expectedHealthBonus,
            float expectedMoveSpeedBonus,
            float expectedDamageBonus)
        {
            BrawlerDefinition brawler = AssetDatabase.LoadAssetAtPath<BrawlerDefinition>(brawlerPath);
            BrawlerSkillTreeNodeDefinition node =
                AssetDatabase.LoadAssetAtPath<BrawlerSkillTreeNodeDefinition>(nodePath);

            Assert.That(brawler, Is.Not.Null, brawlerPath);
            Assert.That(node, Is.Not.Null, nodePath);

            var state = new BrawlerState(brawler, TeamType.Blue);
            float baseHealth = state.MaxHealth.Value;
            float baseMoveSpeed = state.MoveSpeed.Value;
            float baseDamage = state.Damage.Value;

            state.SetPassiveLoadout(new PassiveDefinition[] { node }, false);

            Assert.That(state.MaxHealth.Value,
                Is.EqualTo(baseHealth + expectedHealthBonus).Within(0.001f));
            Assert.That(state.MoveSpeed.Value,
                Is.EqualTo(baseMoveSpeed * (1f + expectedMoveSpeedBonus)).Within(0.001f));
            Assert.That(state.Damage.Value,
                Is.EqualTo(baseDamage * (1f + expectedDamageBonus)).Within(0.001f));

            state.SetPassiveLoadout(null, false);

            Assert.That(state.MaxHealth.Value, Is.EqualTo(baseHealth).Within(0.001f));
            Assert.That(state.MoveSpeed.Value, Is.EqualTo(baseMoveSpeed).Within(0.001f));
            Assert.That(state.Damage.Value, Is.EqualTo(baseDamage).Within(0.001f));
        }

        [TestCase(ColtDefinition, "Assets/Scriptables/Brawlers/colt/Colt_SkillNode_QuickDraw.asset", 0.12f)]
        [TestCase(ByronDefinition, "Assets/Scriptables/Brawlers/byron/Byron_SkillNode_CarefulMixing.asset", 0.10f)]
        [TestCase(BarleyDefinition, "Assets/Scriptables/Brawlers/Barley/Barley_SkillNode_QuickMix.asset", 0.10f)]
        [TestCase(JessieDefinition, "Assets/Scriptables/Brawlers/Jesse/Jessie_SkillNode_RapidRelay.asset", 0.10f)]
        public void AuthoredAttackSpeedNode_ChangesCooldownScale(
            string brawlerPath,
            string nodePath,
            float expectedBonus)
        {
            BrawlerDefinition brawler = AssetDatabase.LoadAssetAtPath<BrawlerDefinition>(brawlerPath);
            BrawlerSkillTreeNodeDefinition node =
                AssetDatabase.LoadAssetAtPath<BrawlerSkillTreeNodeDefinition>(nodePath);
            Assert.That(brawler, Is.Not.Null, brawlerPath);
            Assert.That(node, Is.Not.Null, nodePath);

            var state = new BrawlerState(brawler, TeamType.Neutral);
            float baseAttackSpeed = state.AttackSpeed.Value;
            state.SetPassiveLoadout(new PassiveDefinition[] { node }, false);

            Assert.That(
                state.AttackSpeed.Value,
                Is.EqualTo(baseAttackSpeed * (1f + expectedBonus)).Within(0.001f));

            state.SetPassiveLoadout(null, false);
            Assert.That(state.AttackSpeed.Value, Is.EqualTo(baseAttackSpeed).Within(0.001f));
        }

        [TestCase("Assets/Scriptables/Brawlers/colt/Colt_SkillTree.asset")]
        [TestCase("Assets/Scriptables/Brawlers/byron/Byron_SkillTree.asset")]
        [TestCase("Assets/Scriptables/Brawlers/Barley/Barley_SkillTree.asset")]
        [TestCase("Assets/Scriptables/Brawlers/Jesse/Jessie_SkillTree.asset")]
        public void EveryAuthoredNode_HasARealGameplayEffect(string treePath)
        {
            BrawlerSkillTreeDefinition tree =
                AssetDatabase.LoadAssetAtPath<BrawlerSkillTreeDefinition>(treePath);

            Assert.That(tree, Is.Not.Null, treePath);
            Assert.That(tree.Validate(out string error), Is.True, error);

            foreach (BrawlerSkillTreeNodeDefinition node in tree.Nodes)
            {
                Assert.That(node, Is.Not.Null, treePath);
                Assert.That(HasGameplayEffect(node), Is.True,
                    $"{node.EffectiveDisplayName} is selectable but has no configured gameplay effect.");
            }
        }

        [Test]
        public void SelectedStatNode_ResolvesAsAnInstalledPassive()
        {
            BrawlerDefinition brawler = AssetDatabase.LoadAssetAtPath<BrawlerDefinition>(ColtDefinition);
            BrawlerSkillTreeNodeDefinition core = brawler.SkillTree.Nodes[0];
            var statNode = UnityEngine.ScriptableObject.CreateInstance<BrawlerSkillTreeNodeDefinition>();
            var tree = UnityEngine.ScriptableObject.CreateInstance<BrawlerSkillTreeDefinition>();

            try
            {
                statNode.NodeId = "test_runtime_stat";
                statNode.DisplayName = "Runtime Stat";
                statNode.NodeType = BrawlerSkillTreeNodeType.Stat;
                statNode.StartsUnlocked = true;
                statNode.DamageBonusPercent = 0.25f;
                tree.MaxActiveNodes = 2;
                tree.Nodes = new[] { core, statNode };

                var selected = new List<string> { statNode.EffectiveId };
                Assert.That(BrawlerSkillTreeResolver.TryResolve(
                    brawler,
                    tree,
                    11,
                    selected,
                    out ResolvedBrawlerBuild resolved,
                    out string error,
                    selected), Is.True, error);
                CollectionAssert.Contains(resolved.PassiveOptions, statNode);

                var state = new BrawlerState(brawler, TeamType.Blue);
                float baseDamage = state.Damage.Value;
                state.SetPassiveLoadout(resolved.PassiveOptions, false);

                Assert.That(state.Damage.Value, Is.EqualTo(baseDamage * 1.25f).Within(0.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(statNode);
                UnityEngine.Object.DestroyImmediate(tree);
            }
        }

        private static bool HasGameplayEffect(BrawlerSkillTreeNodeDefinition node)
        {
            return node.GrantedMainAttack != null ||
                node.GrantedSuper != null ||
                node.GrantedGadget != null ||
                node.GrantedPassive != null ||
                node.GrantedHypercharge != null ||
                node.GrantedNanopower != null ||
                node.BonusMaxHealth > 0f ||
                node.MoveSpeedBonusPercent > 0f ||
                node.DamageBonusPercent > 0f ||
                node.AttackSpeedBonusPercent > 0f;
        }

        private static T InvokePreviewMethod<T>(string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(BrawlerSelectScreen).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(null, arguments);
        }
    }
}
