namespace MOBA.Core.Simulation
{
    public interface IAbilityLogic
    {
        AbilityExecutionResult Execute(IAbilityUser user, AbilityExecutionContext context);
        void Tick(uint currentTick);
    }
}