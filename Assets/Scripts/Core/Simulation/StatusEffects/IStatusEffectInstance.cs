namespace MOBA.Core.Simulation
{
    public interface IStatusEffectInstance
    {
        StatusEffectType Type { get; }
        uint StartTick { get; }
        uint EndTick { get; }

        void Apply(IStatusTarget target, uint currentTick);
        void Tick(IStatusTarget target, uint currentTick);
        void Remove(IStatusTarget target, uint currentTick);

        bool CanMerge(StatusEffectContext context);
        void Merge(StatusEffectContext context, uint currentTick);
        bool IsExpired(uint currentTick);
    }
}