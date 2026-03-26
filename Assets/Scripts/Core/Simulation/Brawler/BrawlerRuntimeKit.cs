using MOBA.Core.Definitions;

namespace MOBA.Core.Simulation
{
    public sealed class BrawlerRuntimeKit
    {
        public AbilityDefinition MainAttackDefinition { get; private set; }
        public AbilityDefinition SuperDefinition { get; private set; }
        public GadgetDefinition GadgetDefinition { get; private set; }
        public HyperchargeDefinition HyperchargeDefinition { get; private set; }

        public IAbilityLogic MainAttackLogic { get; private set; }
        public IAbilityLogic SuperLogic { get; private set; }
        public IAbilityLogic GadgetLogic { get; private set; }

        public void Clear()
        {
            MainAttackDefinition = null;
            SuperDefinition = null;
            GadgetDefinition = null;
            HyperchargeDefinition = null;

            MainAttackLogic = null;
            SuperLogic = null;
            GadgetLogic = null;
        }

        public void SetMainAttack(AbilityDefinition definition, IAbilityLogic logic)
        {
            MainAttackDefinition = definition;
            MainAttackLogic = logic;
        }

        public void SetSuper(AbilityDefinition definition, IAbilityLogic logic)
        {
            SuperDefinition = definition;
            SuperLogic = logic;
        }

        public void SetGadget(GadgetDefinition definition, IAbilityLogic logic)
        {
            GadgetDefinition = definition;
            GadgetLogic = logic;
        }

        public void SetHypercharge(HyperchargeDefinition definition)
        {
            HyperchargeDefinition = definition;
        }
    }
}