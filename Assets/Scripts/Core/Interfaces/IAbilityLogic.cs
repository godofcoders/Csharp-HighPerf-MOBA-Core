namespace MOBA.Core.Simulation
{
    public interface IAbilityLogic
    {
        // Execute is called when the brawler uses the ability
        void Execute(IAbilityUser user, AbilityContext context);

        // Tick allows for abilities that have duration (like a beam or a poison)
        void Tick(uint currentTick);
    }
}