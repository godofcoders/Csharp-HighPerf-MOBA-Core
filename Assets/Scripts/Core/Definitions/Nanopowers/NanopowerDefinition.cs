using MOBA.Core.Simulation;
using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "Nanopower", menuName = "MOBA/Nanopowers/Nanopower")]
    public class NanopowerDefinition : PassiveDefinition
    {
        [Header("Nanopower Presentation")]
        public Color AccentColor = new Color(0.15f, 0.75f, 1f, 1f);

        [Header("Stat Effects")]
        [Tooltip("0.12 = +12% outgoing damage through the shared damage modifier pipeline.")]
        [Range(0f, 1f)] public float OutgoingDamageBonusPercent;

        [Tooltip("0.10 = +10% brawler base damage stat. This scales ability damage through DamageService.")]
        [Range(0f, 1f)] public float DamageStatBonusPercent;

        [Tooltip("0.10 = +10% movement speed.")]
        [Range(0f, 1f)] public float MoveSpeedBonusPercent;

        [Tooltip("Flat bonus max health while equipped.")]
        [Min(0f)] public float BonusMaxHealth;

        [Tooltip("0.08 = 8% incoming damage reduction.")]
        [Range(0f, 0.75f)] public float IncomingDamageReductionPercent;

        private void OnValidate()
        {
            Category = PassiveCategory.MatchModifier;
        }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(PassiveName))
                    return PassiveName;

                if (!string.IsNullOrEmpty(OptionName))
                    return OptionName;

                return name;
            }
        }

        public string DisplayDescription
        {
            get
            {
                if (!string.IsNullOrEmpty(Description))
                    return Description;

                return "Match-start nanopower.";
            }
        }

        public override void Install(PassiveInstallContext context)
        {
            if (context.State == null)
                return;

            if (DamageStatBonusPercent > 0f)
            {
                context.State.Damage.AddModifier(
                    new StatModifier(DamageStatBonusPercent, ModifierType.Multiplicative, context.SourceToken));
            }

            if (MoveSpeedBonusPercent > 0f)
            {
                context.State.MoveSpeed.AddModifier(
                    new StatModifier(MoveSpeedBonusPercent, ModifierType.Multiplicative, context.SourceToken));
            }

            if (BonusMaxHealth > 0f)
            {
                context.State.MaxHealth.AddModifier(
                    new StatModifier(BonusMaxHealth, ModifierType.Additive, context.SourceToken));
            }

            if (OutgoingDamageBonusPercent > 0f)
            {
                context.State.AddOutgoingDamageModifier(
                    new DamageModifier(DamageModifierType.PercentAmplification, OutgoingDamageBonusPercent, context.SourceToken));
            }

            if (IncomingDamageReductionPercent > 0f)
            {
                context.State.AddIncomingDamageModifier(
                    new DamageModifier(DamageModifierType.PercentReduction, IncomingDamageReductionPercent, context.SourceToken));
            }
        }
    }
}
