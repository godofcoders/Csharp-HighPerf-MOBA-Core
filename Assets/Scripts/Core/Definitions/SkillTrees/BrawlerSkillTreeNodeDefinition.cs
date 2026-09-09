using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    public enum BrawlerSkillTreeNodeType
    {
        Core = 0,
        Stat = 1,
        MainAttack = 2,
        Super = 3,
        Gadget = 4,
        StarPower = 5,
        Hypercharge = 6,
        Nanopower = 7
    }

    [CreateAssetMenu(fileName = "SkillTreeNode", menuName = "MOBA/Skill Trees/Node")]
    public sealed class BrawlerSkillTreeNodeDefinition : PassiveDefinition
    {
        [Header("Skill Tree Identity")]
        public string NodeId;
        public string DisplayName;
        [TextArea] public string DisplayDescription;
        public BrawlerSkillTreeNodeType NodeType;
        public Color AccentColor = new Color(1f, 0.45f, 0.1f, 1f);

        [Header("Unlock Rules")]
        [Min(1)] public int UnlockPowerLevel = 1;
        public bool StartsUnlocked;
        public bool ActiveByDefault;
        public string[] PrerequisiteNodeIds;
        public string[] MutuallyExclusiveNodeIds;

        [Header("Granted Content")]
        public AbilityDefinition GrantedMainAttack;
        public AbilityDefinition GrantedSuper;
        public GadgetDefinition GrantedGadget;
        public PassiveDefinition GrantedPassive;
        public HyperchargeDefinition GrantedHypercharge;
        public NanopowerDefinition GrantedNanopower;

        [Header("Stat Rewards")]
        [Min(0f)] public float BonusMaxHealth;
        [Range(0f, 1f)] public float MoveSpeedBonusPercent;
        [Range(0f, 1f)] public float DamageBonusPercent;
        [Range(0f, 1f)] public float AttackSpeedBonusPercent;

        public string EffectiveId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(NodeId))
                    return NodeId;

                return name;
            }
        }

        public string EffectiveDisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayName))
                    return DisplayName;

                if (!string.IsNullOrWhiteSpace(OptionName))
                    return OptionName;

                return name;
            }
        }

        public override void Install(PassiveInstallContext context)
        {
            if (context.State == null)
                return;

            if (BonusMaxHealth > 0f)
            {
                context.State.MaxHealth.AddModifier(
                    new StatModifier(BonusMaxHealth, ModifierType.Additive, context.SourceToken));
            }

            if (MoveSpeedBonusPercent > 0f)
            {
                context.State.MoveSpeed.AddModifier(
                    new StatModifier(MoveSpeedBonusPercent, ModifierType.Multiplicative, context.SourceToken));
            }

            if (DamageBonusPercent > 0f)
            {
                context.State.Damage.AddModifier(
                    new StatModifier(DamageBonusPercent, ModifierType.Multiplicative, context.SourceToken));
            }

            if (AttackSpeedBonusPercent > 0f)
            {
                context.State.AttackSpeed.AddModifier(
                    new StatModifier(AttackSpeedBonusPercent, ModifierType.Multiplicative, context.SourceToken));
            }

            // Nanopowers are now tree rewards. Reusing the same install token
            // keeps their modifiers removable with the node on respawn/build swap.
            GrantedNanopower?.Install(context);
        }

        private void OnValidate()
        {
            Category = PassiveCategory.MatchModifier;
            PassiveName = EffectiveDisplayName;
        }
    }
}
