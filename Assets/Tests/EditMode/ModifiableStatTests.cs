using NUnit.Framework;
using MOBA.Core.Simulation;

namespace MOBA.Tests.EditMode
{
    // Unit tests for ModifiableStat — the (BaseValue, modifier-list) pair
    // that every primary stat (MaxHealth, Damage, MoveSpeed) is built on.
    //
    // The modifier-ordering invariant tested here is the single most
    // important piece of combat math in the whole codebase: additive
    // modifiers apply before multiplicative ones. Every balance number
    // — every star-power tune, every hypercharge buff, every gadget effect
    // — assumes this order. Changing it silently would mean every existing
    // design spreadsheet is wrong.
    public class ModifiableStatTests
    {
        [Test]
        public void Value_EqualsBaseValue_WhenNoModifiers()
        {
            ModifiableStat stat = new ModifiableStat(100f);

            Assert.AreEqual(100f, stat.Value);
            Assert.AreEqual(100f, stat.BaseValue);
        }

        [Test]
        public void AdditiveModifier_AddsToBase()
        {
            ModifiableStat stat = new ModifiableStat(100f);
            stat.AddModifier(new StatModifier(20f, ModifierType.Additive));

            Assert.AreEqual(120f, stat.Value);
        }

        [Test]
        public void MultiplicativeModifier_MultipliesBase()
        {
            // Convention: multiplicative modifier of 0.25 means +25%, not x0.25.
            // Formula: (Base + Add) * (1 + Mult).
            ModifiableStat stat = new ModifiableStat(100f);
            stat.AddModifier(new StatModifier(0.25f, ModifierType.Multiplicative));

            Assert.AreEqual(125f, stat.Value);
        }

        [Test]
        public void ModifierOrder_AdditiveBeforeMultiplicative()
        {
            // The critical invariant. 100 base + 50 flat → 150, then +20% → 180.
            // If order flipped, we'd get 100 * 1.2 = 120, then +50 = 170.
            // Every damage-math decision downstream depends on this ordering.
            ModifiableStat stat = new ModifiableStat(100f);
            stat.AddModifier(new StatModifier(50f, ModifierType.Additive));
            stat.AddModifier(new StatModifier(0.2f, ModifierType.Multiplicative));

            Assert.AreEqual(180f, stat.Value);
        }

        [Test]
        public void MultipleAdditiveModifiers_Stack()
        {
            ModifiableStat stat = new ModifiableStat(100f);
            stat.AddModifier(new StatModifier(10f, ModifierType.Additive));
            stat.AddModifier(new StatModifier(30f, ModifierType.Additive));

            Assert.AreEqual(140f, stat.Value);
        }

        [Test]
        public void MultipleMultiplicativeModifiers_StackAdditively_NotCompounding()
        {
            // Two +20% modifiers = +40%, NOT (1.2 * 1.2 - 1) = +44%.
            // Matches Brawl Stars' additive-percentage convention: a 90%
            // slow plus a 15% slow is capped at 105%, not 91.5%. Keeps
            // designer math simple.
            ModifiableStat stat = new ModifiableStat(100f);
            stat.AddModifier(new StatModifier(0.2f, ModifierType.Multiplicative));
            stat.AddModifier(new StatModifier(0.2f, ModifierType.Multiplicative));

            Assert.AreEqual(140f, stat.Value);
        }

        [Test]
        public void RemoveModifiersFromSource_RemovesOnlyThatSource()
        {
            object hypercharge = "hypercharge";
            object starPower = "star_power";

            ModifiableStat stat = new ModifiableStat(100f);
            stat.AddModifier(new StatModifier(20f, ModifierType.Additive, hypercharge));
            stat.AddModifier(new StatModifier(30f, ModifierType.Additive, starPower));
            Assert.AreEqual(150f, stat.Value);

            stat.RemoveModifiersFromSource(hypercharge);

            Assert.AreEqual(130f, stat.Value);
        }

        [Test]
        public void SetBaseValue_RecalculatesWithExistingModifiers()
        {
            // Used when power level changes recalculate base stats but
            // want to preserve currently-active buffs (e.g., star-power
            // stat bumps still apply on top of the new base).
            ModifiableStat stat = new ModifiableStat(100f);
            stat.AddModifier(new StatModifier(50f, ModifierType.Additive));
            Assert.AreEqual(150f, stat.Value);

            stat.SetBaseValue(200f);

            Assert.AreEqual(250f, stat.Value);
        }
    }
}
