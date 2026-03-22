using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "PassiveLoadoutRules", menuName = "MOBA/Loadouts/Passive Loadout Rules")]
    public class PassiveLoadoutRules : ScriptableObject
    {
        [Header("Category Limits")]
        public int MaxStarPowers = 1;
        public int MaxGears = 2;
        public int MaxTraits = 99;
        public int MaxMatchModifiers = 99;
        public int MaxTemporaryBuffs = 99;

        public int GetLimit(PassiveCategory category)
        {
            switch (category)
            {
                case PassiveCategory.StarPower:
                    return MaxStarPowers;
                case PassiveCategory.Gear:
                    return MaxGears;
                case PassiveCategory.Trait:
                    return MaxTraits;
                case PassiveCategory.MatchModifier:
                    return MaxMatchModifiers;
                case PassiveCategory.TemporaryBuff:
                    return MaxTemporaryBuffs;
                default:
                    return 99;
            }
        }
    }
}