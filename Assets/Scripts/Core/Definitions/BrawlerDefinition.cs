using UnityEngine;

namespace MOBA.Core.Definitions
{
    [CreateAssetMenu(fileName = "NewBrawler", menuName = "MOBA/Brawler Definition")]
    public class BrawlerDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string BrawlerName;
        public GameObject ModelPrefab;

        [Header("Base Stats")]
        public float BaseHealth = 3000f;
        public float BaseMoveSpeed = 5.0f;
        public float BaseDamage = 500f;

        [Header("Abilities")]
        public AbilityDefinition MainAttack;
        public AbilityDefinition SuperAbility;

        [Header("Supplemental Systems")]
        public GadgetDefinition Gadget;
        public StarPowerDefinition StarPower;
        public HyperchargeDefinition Hypercharge;

        [Header("AI")]
        public BrawlerAIProfile AIProfile;
    }
}